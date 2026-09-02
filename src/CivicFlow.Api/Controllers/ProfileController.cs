using CivicFlow.Api.Common;
using CivicFlow.Domain.Entities;
using CivicFlow.Infrastructure.Identity;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CivicFlow.Api.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public sealed class ProfileController(
    UserManager<ApplicationUser> users,
    ApplicationDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ProfileResponse>> Get()
    {
        var user = await CurrentUser();
        return user is null ? NotFound() : Ok(await Project(user));
    }

    [HttpPut]
    public async Task<ActionResult<ProfileResponse>> Update(UpdateProfileRequest request)
    {
        var user = await CurrentUser();
        if (user is null) return NotFound();
        var fullName = NormaliseName(request.FullName);
        if (fullName.Length is < 2 or > 150)
            return FieldError(nameof(request.FullName), "Full name must be between 2 and 150 characters.");
        if (string.IsNullOrWhiteSpace(request.Version) || request.Version != user.ConcurrencyStamp)
            return Conflict(new { message = "Your profile changed since it was loaded. Refresh and try again." });

        var split = fullName.IndexOf(' ');
        var firstName = split < 0 ? fullName : fullName[..split];
        var lastName = split < 0 ? string.Empty : fullName[(split + 1)..];
        if (user.FirstName == firstName && user.LastName == lastName) return Ok(await Project(user));

        user.FirstName = firstName;
        user.LastName = lastName;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            if (result.Errors.Any(x => x.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
                return Conflict(new { message = "Your profile changed since it was loaded. Refresh and try again." });
            return IdentityErrors(nameof(request.FullName), result.Errors);
        }
        AddAudit(user.Id, "ProfileUpdated", "Updated own profile name.");
        await db.SaveChangesAsync();
        return Ok(await Project(user));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await CurrentUser();
        if (user is null) return NotFound();
        if (string.IsNullOrWhiteSpace(request.CurrentPassword))
            return FieldError(nameof(request.CurrentPassword), "Current password is required.");
        if (string.IsNullOrWhiteSpace(request.NewPassword))
            return FieldError(nameof(request.NewPassword), "New password is required.");
        if (!await users.CheckPasswordAsync(user, request.CurrentPassword))
            return FieldError(nameof(request.CurrentPassword), "Current password is incorrect.");
        if (await users.CheckPasswordAsync(user, request.NewPassword))
            return FieldError(nameof(request.NewPassword), "New password must be different from your current password.");

        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded) return IdentityErrors(nameof(request.NewPassword), result.Errors);

        var now = DateTimeOffset.UtcNow;
        var activeTokens = await db.RefreshTokens.Where(x => x.UserId == user.Id && x.RevokedAtUtc == null).ToListAsync();
        foreach (var token in activeTokens) token.RevokedAtUtc = now;
        AddAudit(user.Id, "PasswordChanged", "Changed own account password and revoked active sessions.");
        await db.SaveChangesAsync();
        if (transaction is not null) await transaction.CommitAsync();
        return NoContent();
    }

    private async Task<ApplicationUser?> CurrentUser() => await users.FindByIdAsync(User.UserId().ToString());

    private async Task<ProfileResponse> Project(ApplicationUser user) => new(
        user.Id, user.FirstName, user.LastName, $"{user.FirstName} {user.LastName}".Trim(), user.Email ?? string.Empty,
        await users.GetRolesAsync(user), user.IsActive, user.ConcurrencyStamp ?? string.Empty);

    private void AddAudit(Guid actorId, string type, string message) =>
        db.CaseActivities.Add(new CaseActivity(Guid.Empty, actorId, type, message, false, DateTimeOffset.UtcNow));

    private ActionResult FieldError(string field, string message) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = [message] }));

    private ActionResult IdentityErrors(string field, IEnumerable<IdentityError> errors) =>
        ValidationProblem(new ValidationProblemDetails(new Dictionary<string, string[]> { [field] = errors.Select(x => x.Description).ToArray() }));

    private static string NormaliseName(string? value) =>
        string.Join(' ', (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

public sealed record UpdateProfileRequest(string FullName, string Version);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record ProfileResponse(Guid Id, string FirstName, string LastName, string FullName, string Email, IList<string> Roles, bool IsActive, string Version);
