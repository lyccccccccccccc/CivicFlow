using CivicFlow.Api.Common;
using CivicFlow.Domain.Entities;
using CivicFlow.Domain.Enums;
using CivicFlow.Infrastructure.Identity;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace CivicFlow.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class OperationsController(ApplicationDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications() => Ok(await db.UserNotifications.AsNoTracking()
        .Where(x => x.UserId == User.UserId()).OrderByDescending(x => x.CreatedAtUtc).Take(50).ToListAsync());

    [HttpPost("notifications/{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id)
    {
        var item = await db.UserNotifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.UserId());
        if (item is null) return NotFound();
        item.MarkRead(DateTimeOffset.UtcNow); await db.SaveChangesAsync(); return NoContent();
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = CivicFlowRoles.CaseOfficer + "," + CivicFlowRoles.TeamManager + "," + CivicFlowRoles.SystemAdministrator)]
    public async Task<IActionResult> Dashboard()
    {
        var now = DateTimeOffset.UtcNow;
        var open = await db.ServiceRequests.CountAsync(x => x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected);
        var overdue = await db.ServiceRequests.CountAsync(x => x.ResolutionDueAtUtc < now && x.Status != ServiceRequestStatus.Resolved && x.Status != ServiceRequestStatus.Closed);
        var resolved30 = await db.ServiceRequests.CountAsync(x => x.ResolvedAtUtc >= now.AddDays(-30));
        var unassigned = await db.ServiceRequests.CountAsync(x => x.AssignedOfficerId == null && x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected);
        var byStatus = await db.ServiceRequests.GroupBy(x => x.Status).Select(x => new { Status = x.Key.ToString(), Count = x.Count() }).ToListAsync();
        return Ok(new { Open = open, Overdue = overdue, ResolvedLast30Days = resolved30, Unassigned = unassigned, ByStatus = byStatus });
    }

    [HttpGet("reports/cases.csv")]
    [Authorize(Roles = CivicFlowRoles.TeamManager + "," + CivicFlowRoles.SystemAdministrator)]
    public async Task<IActionResult> ExportCases()
    {
        var rows = await db.ServiceRequests.AsNoTracking().OrderByDescending(x => x.SubmittedAtUtc).Take(5000).ToListAsync();
        var csv = new StringBuilder("Reference,Title,Status,Priority,Submitted UTC,Resolution Due UTC\r\n");
        foreach (var item in rows)
            csv.AppendLine(string.Join(',', Escape(item.ReferenceNumber), Escape(item.Title), item.Status, item.Priority,
                item.SubmittedAtUtc.ToString("O"), item.ResolutionDueAtUtc?.ToString("O") ?? string.Empty));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"civicflow-cases-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    [HttpGet("admin/users")]
    [Authorize(Roles = CivicFlowRoles.SystemAdministrator)]
    public async Task<IActionResult> Users()
    {
        var results = new List<object>();
        foreach (var user in await db.Users.AsNoTracking().OrderBy(x => x.LastName).ToListAsync())
            results.Add(new { user.Id, user.FirstName, user.LastName, user.Email, user.IsActive, user.CreatedAtUtc, Roles = await users.GetRolesAsync(user) });
        return Ok(results);
    }

    [HttpPut("admin/users/{id:guid}/role")]
    [Authorize(Roles = CivicFlowRoles.SystemAdministrator)]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeRoleRequest request)
    {
        if (!CivicFlowRoles.All.Contains(request.Role)) return BadRequest(new { message = "Unknown CivicFlow role." });
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        var existing = await users.GetRolesAsync(user);
        if (existing.Count > 0) await users.RemoveFromRolesAsync(user, existing);
        var result = await users.AddToRoleAsync(user, request.Role);
        return result.Succeeded ? NoContent() : BadRequest(result.Errors);
    }

    [HttpPut("admin/users/{id:guid}/active")]
    [Authorize(Roles = CivicFlowRoles.SystemAdministrator)]
    public async Task<IActionResult> ChangeActive(Guid id, ChangeActiveRequest request)
    {
        var user = await users.FindByIdAsync(id.ToString());
        if (user is null) return NotFound();
        user.IsActive = request.IsActive;
        await users.UpdateAsync(user);
        return NoContent();
    }

    [HttpPost("admin/categories")]
    [Authorize(Roles = CivicFlowRoles.SystemAdministrator)]
    public async Task<IActionResult> CreateCategory(CategoryRequest request)
    {
        var item = new ServiceCategory(request.Name, request.Description, request.FirstResponseHours, request.ResolutionHours, DateTimeOffset.UtcNow);
        db.ServiceCategories.Add(item); await db.SaveChangesAsync(); return Ok(new { item.Id });
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

public sealed record CategoryRequest(string Name, string Description, int FirstResponseHours, int ResolutionHours);
public sealed record ChangeRoleRequest(string Role);
public sealed record ChangeActiveRequest(bool IsActive);
