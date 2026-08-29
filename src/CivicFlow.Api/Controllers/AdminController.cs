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
[Route("api/admin")]
[Authorize(Roles = CivicFlowRoles.SystemAdministrator)]
public sealed class AdminController(ApplicationDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var results = new List<object>();
        foreach (var user in await db.Users.AsNoTracking().OrderBy(x => x.LastName).ToListAsync())
            results.Add(new { user.Id, user.FirstName, user.LastName, user.Email, user.IsActive, user.CreatedAtUtc, Roles = await users.GetRolesAsync(user) });
        return Ok(results);
    }

    [HttpPut("users/{id:guid}/role")]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeRoleRequest request)
    {
        if (!CivicFlowRoles.All.Contains(request.Role)) return BadRequest(new { message = "Unknown CivicFlow role." });
        if (id == User.UserId()) return BadRequest(new { message = "You cannot change your own administrator role." });
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        var existing = await users.GetRolesAsync(user);
        if (existing.Count == 1 && existing.Contains(request.Role)) return NoContent();
        if (!existing.Contains(request.Role))
        {
            var addResult = await users.AddToRoleAsync(user, request.Role);
            if (!addResult.Succeeded) return BadRequest(addResult.Errors);
        }
        var oldRoles = existing.Where(x => x != request.Role).ToList();
        if (oldRoles.Count > 0)
        {
            var removeResult = await users.RemoveFromRolesAsync(user, oldRoles);
            if (!removeResult.Succeeded) return BadRequest(removeResult.Errors);
        }
        AddAudit("UserRoleChanged", $"Changed {user.Email} role from {string.Join(',', existing)} to {request.Role}.");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("users/{id:guid}/active")]
    public async Task<IActionResult> ChangeActive(Guid id, ChangeActiveRequest request)
    {
        if (id == User.UserId() && !request.IsActive) return BadRequest(new { message = "You cannot disable your own account." });
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        user.IsActive = request.IsActive;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded) return BadRequest(result.Errors);
        AddAudit("UserStatusChanged", $"Set {user.Email} active state to {request.IsActive}.");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("categories")]
    public async Task<IActionResult> Categories() => Ok(await db.ServiceCategories.AsNoTracking().OrderBy(x => x.Name).ToListAsync());

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(CategoryRequest request)
    {
        if (await db.ServiceCategories.AnyAsync(x => x.Name == request.Name.Trim()))
            return Conflict(new { message = "A category with that name already exists." });
        var item = new ServiceCategory(request.Name, request.Description, request.FirstResponseHours, request.ResolutionHours, DateTimeOffset.UtcNow);
        db.ServiceCategories.Add(item);
        AddAudit("CategoryCreated", $"Created category {item.Name}.");
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Categories), new { item.Id });
    }

    [HttpPut("categories/{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, CategoryRequest request)
    {
        var item = await db.ServiceCategories.FindAsync(id);
        if (item is null) return NotFound();
        if (await db.ServiceCategories.AnyAsync(x => x.Id != id && x.Name == request.Name.Trim()))
            return Conflict(new { message = "A category with that name already exists." });
        var before = $"{item.Name} ({item.FirstResponseHours}/{item.ResolutionHours}h)";
        item.Update(request.Name, request.Description, request.FirstResponseHours, request.ResolutionHours, DateTimeOffset.UtcNow);
        AddAudit("CategoryUpdated", $"Updated category {before} to {item.Name} ({item.FirstResponseHours}/{item.ResolutionHours}h).");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("categories/{id:guid}/active")]
    public async Task<IActionResult> ChangeCategoryActive(Guid id, ChangeActiveRequest request)
    {
        var item = await db.ServiceCategories.FindAsync(id);
        if (item is null) return NotFound();
        item.SetActive(request.IsActive, DateTimeOffset.UtcNow);
        AddAudit("CategoryStatusChanged", $"Set category {item.Name} active state to {request.IsActive}.");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> AuditLogs([FromQuery] AuditLogQuery request)
    {
        var query = db.CaseActivities.AsNoTracking().AsQueryable();
        if (request.UserId.HasValue) query = query.Where(x => x.ActorId == request.UserId);
        if (!string.IsNullOrWhiteSpace(request.Action)) query = query.Where(x => x.Type.Contains(request.Action.Trim()));
        if (request.From.HasValue) query = query.Where(x => x.CreatedAtUtc >= request.From);
        if (request.To.HasValue) query = query.Where(x => x.CreatedAtUtc < request.To);
        if (!string.IsNullOrWhiteSpace(request.Case))
        {
            var term = request.Case.Trim();
            var ids = db.ServiceRequests.Where(x => x.ReferenceNumber.Contains(term) || x.Title.Contains(term)).Select(x => x.Id);
            query = query.Where(x => ids.Contains(x.ServiceRequestId));
        }
        var total = await query.CountAsync();
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 10, 100);
        var rows = await query.OrderByDescending(x => x.CreatedAtUtc).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                x.Id, Action = x.Type, x.Message, x.CreatedAtUtc, x.ServiceRequestId, x.ActorId,
                UserName = db.Users.Where(u => u.Id == x.ActorId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                UserEmail = db.Users.Where(u => u.Id == x.ActorId).Select(u => u.Email).FirstOrDefault(),
                ReferenceNumber = db.ServiceRequests.Where(c => c.Id == x.ServiceRequestId).Select(c => c.ReferenceNumber).FirstOrDefault()
            }).ToListAsync();
        return Ok(new PagedResponse<object>(rows.Cast<object>().ToList(), page, pageSize, total,
            (int)Math.Ceiling(total / (double)pageSize)));
    }

    private void AddAudit(string type, string message) =>
        db.CaseActivities.Add(new CaseActivity(Guid.Empty, User.UserId(), type, message, false, DateTimeOffset.UtcNow));
}

public sealed record CategoryRequest(string Name, string Description, int FirstResponseHours, int ResolutionHours);
public sealed record ChangeRoleRequest(string Role);
public sealed record ChangeActiveRequest(bool IsActive);
public sealed class AuditLogQuery
{
    public Guid? UserId { get; init; }
    public string? Action { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? Case { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}
