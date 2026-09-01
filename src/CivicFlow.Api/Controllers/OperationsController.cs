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
        var activeSla = query.Where(x => x.Status != ServiceRequestStatus.Resolved && x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected);
        var atRisk = await CaseQuery.OverallAtRisk(activeSla, now).CountAsync();
        var overdue = await CaseQuery.OverallOverdue(activeSla, now).CountAsync();
        var firstResponseBreached = await query.CountAsync(x => x.FirstResponseWasBreached == true);
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
        var riskRows = await CaseQuery.OverallOverdue(activeSla, now).Concat(CaseQuery.OverallAtRisk(activeSla, now))
            .Distinct()
            .OrderBy(x => x.FirstResponseCompletedAtUtc == null && x.FirstResponseDueAtUtc < x.ResolutionDueAtUtc
                ? x.FirstResponseDueAtUtc : x.ResolutionDueAtUtc)
            .Take(10)
            .Select(x => new
            {
                Item = x,
                CategoryName = db.ServiceCategories.Where(c => c.Id == x.ServiceCategoryId).Select(c => c.Name).First()
            }).ToListAsync();
        return Ok(new
        {
            Open = open, Unassigned = unassigned, AtRisk = atRisk, Overdue = overdue,
            FirstResponseBreached = firstResponseBreached,
            WaitingForResident = waiting, Resolved = resolved,
            ByStatus = byStatus, ByPriority = byPriority, ByCategory = categoryChart,
            OfficerWorkload = workload,
            ActiveWorkload = workload.Sum(x => x.Count),
            ActiveWorkloadDefinition = "Assigned cases excluding Resolved, Closed and Rejected",
            SlaCases = riskRows.Select(x => new { x.Item.Id, x.Item.ReferenceNumber, x.Item.Title, Priority = x.Item.Priority.ToString(), Status = x.Item.Status.ToString(),
                x.Item.FirstResponseDueAtUtc, x.Item.FirstResponseCompletedAtUtc, x.Item.ResolutionDueAtUtc, x.CategoryName,
                FirstResponseSlaState = SlaCalculator.FirstResponseState(x.Item, now), ResolutionSlaState = SlaCalculator.ResolutionState(x.Item, now),
                SlaState = SlaCalculator.OverallState(x.Item, now), NextSlaDueAtUtc = SlaCalculator.NextDue(x.Item), NextSlaTarget = SlaCalculator.NextTarget(x.Item) })
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
                Item = x,
                IsActiveWorkload = activeIds.Contains(x.Id),
                Category = db.ServiceCategories.Where(c => c.Id == x.ServiceCategoryId).Select(c => c.Name).First(),
                Officer = db.Users.Where(u => u.Id == x.AssignedOfficerId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault()
            }).ToListAsync();
        var csv = new StringBuilder("Reference,Title,Category,Status,Priority,Officer,Submitted UTC,First Response Due UTC,First Response Completed UTC,First Response SLA,Resolution Due UTC,Resolution SLA,Overall SLA,Active workload\r\n");
        foreach (var item in rows)
            csv.AppendLine(string.Join(',', Escape(item.Item.ReferenceNumber), Escape(item.Item.Title), Escape(item.Category), item.Item.Status, item.Item.Priority,
                Escape(item.Officer ?? string.Empty), item.Item.SubmittedAtUtc.ToString("O"), item.Item.FirstResponseDueAtUtc?.ToString("O") ?? string.Empty,
                item.Item.FirstResponseCompletedAtUtc?.ToString("O") ?? string.Empty, SlaCalculator.FirstResponseState(item.Item, DateTimeOffset.UtcNow),
                item.Item.ResolutionDueAtUtc?.ToString("O") ?? string.Empty, SlaCalculator.ResolutionState(item.Item, DateTimeOffset.UtcNow),
                SlaCalculator.OverallState(item.Item, DateTimeOffset.UtcNow), item.IsActiveWorkload ? "1" : "0"));
        return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"civicflow-cases-{DateTime.UtcNow:yyyyMMdd}.csv");
    }

    private static string Escape(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}

public sealed record ChartRow(string Label, int Count);
