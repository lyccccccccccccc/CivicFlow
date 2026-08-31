using System.Text;
using CivicFlow.Api.Common;
using CivicFlow.Domain.Enums;
using CivicFlow.Infrastructure.Identity;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CivicFlow.Api.Controllers;

[ApiController]
[Route("api")]
[Authorize]
public sealed class OperationsController(ApplicationDbContext db) : ControllerBase
{
    private const string StaffRoles = CivicFlowRoles.CaseOfficer + "," + CivicFlowRoles.TeamManager + "," + CivicFlowRoles.SystemAdministrator;
    private const string ManagerRoles = CivicFlowRoles.TeamManager + "," + CivicFlowRoles.SystemAdministrator;

    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications()
    {
        var query = db.UserNotifications.AsNoTracking().Where(x => x.UserId == User.UserId());
        // Fail closed for legacy generic updates and notices outside the user's current role.
        string[] allowed = User.IsInRole(CivicFlowRoles.Resident)
            ? ["Message from service officer", "The service team is waiting for your reply", "Resolved", "Closed", "Reopened", "Rejected"]
            : ["Case assigned", "Case reopened", "Message from resident", "SLA at risk", "SLA overdue"];
        return Ok(await query.Where(x => allowed.Contains(x.Title)).OrderByDescending(x => x.CreatedAtUtc).Take(50)
            .Select(x => new { x.Id, x.ServiceRequestId, x.Title, x.Message, x.ReadAtUtc, x.CreatedAtUtc }).ToListAsync());
    }

    [HttpPost("notifications/{id:guid}/read")]
    public async Task<IActionResult> Read(Guid id)
    {
        var item = await db.UserNotifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == User.UserId());
        if (item is null) return NotFound();
        item.MarkRead(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("dashboard")]
    [Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> Dashboard([FromQuery] CaseFilterParameters filters)
    {
        var now = DateTimeOffset.UtcNow;
        var query = CaseQuery.Apply(db.ServiceRequests.AsNoTracking(), filters, now);
        if (User.IsInRole(CivicFlowRoles.CaseOfficer)) query = query.Where(x => x.AssignedOfficerId == User.UserId());

        var open = await query.CountAsync(x => x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected && x.Status != ServiceRequestStatus.Resolved);
        var unassigned = await query.CountAsync(x => x.AssignedOfficerId == null && x.Status != ServiceRequestStatus.Resolved && x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected);
        var atRisk = await query.CountAsync(x => x.ResolutionDueAtUtc >= now && x.ResolutionDueAtUtc <= now.AddHours(24) && x.Status != ServiceRequestStatus.Resolved && x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected);
        var overdue = await query.CountAsync(x => x.ResolutionDueAtUtc < now && x.Status != ServiceRequestStatus.Resolved && x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected);
        var waiting = await query.CountAsync(x => x.Status == ServiceRequestStatus.WaitingForResident);
        var resolved = await query.CountAsync(x => x.Status == ServiceRequestStatus.Resolved);
        var byStatus = await query.GroupBy(x => x.Status).Select(x => new ChartRow(x.Key.ToString(), x.Count())).ToListAsync();
        var byPriority = await query.GroupBy(x => x.Priority).Select(x => new ChartRow(x.Key.ToString(), x.Count())).ToListAsync();
        var byCategory = await query.GroupBy(x => x.ServiceCategoryId).Select(x => new { Id = x.Key, Count = x.Count() }).ToListAsync();
        var categoryNames = await db.ServiceCategories.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.Name);
        var categoryChart = byCategory.Select(x => new ChartRow(categoryNames.GetValueOrDefault(x.Id, "Unknown"), x.Count)).ToList();
        var workloadRows = await query.Where(CaseQuery.ActiveWorkload)
            .GroupBy(x => x.AssignedOfficerId!.Value).Select(x => new { Id = x.Key, Count = x.Count() }).ToListAsync();
        var officerIds = workloadRows.Select(x => x.Id).ToList();
        var officerNames = await db.Users.AsNoTracking().Where(x => officerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.FirstName + " " + x.LastName);
        var workload = workloadRows.Select(x => new ChartRow(officerNames.GetValueOrDefault(x.Id, "Unknown"), x.Count)).ToList();
        var riskRows = await query.Where(x => x.ResolutionDueAtUtc != null && x.ResolutionDueAtUtc <= now.AddHours(24) &&
                x.Status != ServiceRequestStatus.Resolved && x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected)
            .OrderBy(x => x.ResolutionDueAtUtc).Take(10)
            .Select(x => new
            {
                x.Id, x.ReferenceNumber, x.Title, x.Priority, x.Status, x.ResolutionDueAtUtc,
                CategoryName = db.ServiceCategories.Where(c => c.Id == x.ServiceCategoryId).Select(c => c.Name).First()
            }).ToListAsync();
        return Ok(new
        {
            Open = open, Unassigned = unassigned, AtRisk = atRisk, Overdue = overdue,
            WaitingForResident = waiting, Resolved = resolved,
            ByStatus = byStatus, ByPriority = byPriority, ByCategory = categoryChart,
            OfficerWorkload = workload,
            ActiveWorkload = workload.Sum(x => x.Count),
            ActiveWorkloadDefinition = "Assigned cases excluding Resolved, Closed and Rejected",
            SlaCases = riskRows.Select(x => new { x.Id, x.ReferenceNumber, x.Title, Priority = x.Priority.ToString(), Status = x.Status.ToString(), x.ResolutionDueAtUtc, x.CategoryName, SlaState = x.ResolutionDueAtUtc < now ? "Overdue" : "AtRisk" })
        });
    }

    [HttpGet("reports/cases.csv")]
    [Authorize(Roles = ManagerRoles)]
    public async Task<IActionResult> ExportCases([FromQuery] CaseFilterParameters filters)
    {
        var filtered = CaseQuery.Apply(db.ServiceRequests.AsNoTracking(), filters, DateTimeOffset.UtcNow);
        if (await filtered.CountAsync() > 5000)
            return BadRequest(new { message = "The export exceeds 5000 cases. Narrow the filters; no partial report has been generated." });
        var activeIds = filtered.Where(CaseQuery.ActiveWorkload).Select(x => x.Id);
        var rows = await filtered
            .OrderByDescending(x => x.SubmittedAtUtc).Take(5000)
            .Select(x => new
            {
                x.Id, x.ReferenceNumber, x.Title, x.Status, x.Priority, x.SubmittedAtUtc, x.ResolutionDueAtUtc,
                IsActiveWorkload = activeIds.Contains(x.Id),
                Category = db.ServiceCategories.Where(c => c.Id == x.ServiceCategoryId).Select(c => c.Name).First(),
                Officer = db.Users.Where(u => u.Id == x.AssignedOfficerId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault()
            }).ToListAsync();
        var csv = new StringBuilder("Reference,Title,Category,Status,Priority,Officer,Submitted UTC,Resolution Due UTC,Active workload\r\n");
        foreach (var item in rows)
            csv.AppendLine(string.Join(',', Escape(item.ReferenceNumber), Escape(item.Title), Escape(item.Category), item.Status, item.Priority,
                Escape(item.Officer ?? string.Empty), item.SubmittedAtUtc.ToString("O"), item.ResolutionDueAtUtc?.ToString("O") ?? string.Empty, item.IsActiveWorkload ? "1" : "0"));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"civicflow-cases-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

public sealed record ChartRow(string Label, int Count);
