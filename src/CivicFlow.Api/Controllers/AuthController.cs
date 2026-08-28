using CivicFlow.Api.Auth;
using CivicFlow.Api.Common;
using CivicFlow.Infrastructure.Identity;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CivicFlow.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ApplicationDbContext db,
    TokenService tokens,
    IOptions<JwtOptions> jwtOptions) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(), UserName = email, Email = email,
            FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return ValidationProblem(new ValidationProblemDetails(
            result.Errors.GroupBy(x => x.Code).ToDictionary(x => x.Key, x => x.Select(e => e.Description).ToArray())));
        await users.AddToRoleAsync(user, CivicFlowRoles.Resident);
        return Ok(await IssueTokens(user));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || !(await signIn.CheckPasswordSignInAsync(user, request.Password, true)).Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });
        return Ok(await IssueTokens(user));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        var hash = TokenService.HashRefreshToken(request.RefreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash);
        if (stored is null || stored.RevokedAtUtc is not null || stored.ExpiresAtUtc <= DateTimeOffset.UtcNow)
            return Unauthorized(new { message = "Refresh token is invalid or expired." });
        stored.RevokedAtUtc = DateTimeOffset.UtcNow;
        var user = await users.FindByIdAsync(stored.UserId.ToString());
        if (user is null || !user.IsActive) return Unauthorized();
        return Ok(await IssueTokens(user));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        var hash = TokenService.HashRefreshToken(request.RefreshToken);
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(x => x.TokenHash == hash && x.UserId == User.UserId());
        if (stored is not null) { stored.RevokedAtUtc = DateTimeOffset.UtcNow; await db.SaveChangesAsync(); }
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserResponse>> Me()
    {
        var user = await users.FindByIdAsync(User.UserId().ToString());
        if (user is null) return NotFound();
        return Ok(new UserResponse(user.Id, user.Email!, user.FirstName, user.LastName, await users.GetRolesAsync(user)));
    }

    private async Task<AuthResponse> IssueTokens(ApplicationUser user)
    {
        var issued = await tokens.CreateAsync(user);
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, TokenHash = TokenService.HashRefreshToken(issued.RefreshToken),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(jwtOptions.Value.RefreshTokenDays)
        });
        await db.SaveChangesAsync();
        return new AuthResponse(issued.AccessToken, issued.RefreshToken, issued.ExpiresAt,
            new UserResponse(user.Id, user.Email!, user.FirstName, user.LastName, await users.GetRolesAsync(user)));
    }
}

public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record UserResponse(Guid Id, string Email, string FirstName, string LastName, IList<string> Roles);
public sealed record AuthResponse(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt, UserResponse User);
