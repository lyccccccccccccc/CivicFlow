using System.ComponentModel.DataAnnotations;
using CivicFlow.Api.Common;
using CivicFlow.Domain.Entities;
using CivicFlow.Domain.Enums;
using CivicFlow.Infrastructure.Identity;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CivicFlow.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class CasesController(ApplicationDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    private const string StaffRoles = CivicFlowRoles.CaseOfficer + "," + CivicFlowRoles.TeamManager + "," + CivicFlowRoles.SystemAdministrator;

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> Categories() => Ok(await db.ServiceCategories.AsNoTracking()
        .Where(x => x.IsActive).OrderBy(x => x.Name)
        .Select(x => new { x.Id, x.Name, x.Description, x.FirstResponseHours, x.ResolutionHours }).ToListAsync());

    [HttpPost("cases")]
    [Authorize(Roles = CivicFlowRoles.Resident)]
    public async Task<IActionResult> Create(CreateCaseRequest request)
    {
        var category = await db.ServiceCategories.FindAsync(request.CategoryId);
        if (category is null || !category.IsActive) return BadRequest(new { message = "Select an active service category." });
        var now = DateTimeOffset.UtcNow;
        var reference = $"CF-{now:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var item = ServiceRequest.Create(reference, User.UserId(), request.CategoryId,
            request.Title, request.Description, request.Address, now);
        if (request.Latitude.HasValue && request.Longitude.HasValue)
            item.SetLocation(request.Latitude.Value, request.Longitude.Value, now);
        db.ServiceRequests.Add(item);
        db.CaseActivities.Add(new CaseActivity(item.Id, User.UserId(), "Submitted", "Request submitted by resident.", true, now));
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = item.Id }, new { item.Id, item.ReferenceNumber });
    }

    [HttpGet("cases")]
    [Authorize]
    public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? search, [FromQuery] bool mine = false)
    {
        var userId = User.UserId();
        var isResident = User.IsInRole(CivicFlowRoles.Resident);
        var query = db.ServiceRequests.AsNoTracking();
        if (isResident) query = query.Where(x => x.ResidentId == userId);
        else if (mine) query = query.Where(x => x.AssignedOfficerId == userId);
        if (Enum.TryParse<ServiceRequestStatus>(status, true, out var parsed)) query = query.Where(x => x.Status == parsed);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(x => x.ReferenceNumber.Contains(term) || x.Title.Contains(term) || x.Address.Contains(term));
        }
        var rows = await query.OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc).Take(200)
            .Select(x => new CaseListItem(x.Id, x.ReferenceNumber, x.Title, x.Status.ToString(), x.Priority.ToString(),
                x.SubmittedAtUtc, x.ResolutionDueAtUtc, x.AssignedOfficerId)).ToListAsync();
        return Ok(rows);
    }

    [HttpGet("cases/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Get(Guid id)
    {
        var item = await db.ServiceRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        if (User.IsInRole(CivicFlowRoles.Resident) && item.ResidentId != User.UserId()) return Forbid();
        var category = await db.ServiceCategories.AsNoTracking().SingleAsync(x => x.Id == item.ServiceCategoryId);
        var activities = await db.CaseActivities.AsNoTracking()
            .Where(x => x.ServiceRequestId == id && (!User.IsInRole(CivicFlowRoles.Resident) || x.IsPublic))
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new { x.Id, x.Type, x.Message, x.IsPublic, x.CreatedAtUtc, x.ActorId }).ToListAsync();
        return Ok(new { Case = item, Category = new { category.Id, category.Name }, Activities = activities });
    }

    [HttpPost("cases/{id:guid}/triage")]
    [Authorize(Roles = StaffRoles)]
    public async Task<IActionResult> Triage(Guid id, TriageRequest request)
    {
        var item = await db.ServiceRequests.FindAsync(id);
        if (item is null) return NotFound();
        var category = await db.ServiceCategories.FindAsync(item.ServiceCategoryId);
        var now = DateTimeOffset.UtcNow;
        item.Triage(request.Priority, now.AddHours(category!.FirstResponseHours), now.AddHours(category.ResolutionHours), now);
        AddActivity(item, "Triaged", $"Priority set to {request.Priority}; SLA targets applied.", true);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("cases/{id:guid}/assign")]
    [Authorize(Roles = CivicFlowRoles.TeamManager + "," + CivicFlowRoles.SystemAdministrator)]
    public async Task<IActionResult> Assign(Guid id, AssignRequest request)
    {
        var officer = await users.FindByIdAsync(request.OfficerId.ToString());
        if (officer is null || !await users.IsInRoleAsync(officer, CivicFlowRoles.CaseOfficer))
            return BadRequest(new { message = "The selected user is not a case officer." });
        var item = await db.ServiceRequests.FindAsync(id);
        if (item is null) return NotFound();
        item.Assign(request.OfficerId, DateTimeOffset.UtcNow);
        AddActivity(item, "Assigned", $"Assigned to {officer.FirstName} {officer.LastName}.", true);
        db.UserNotifications.Add(new UserNotification(officer.Id, item.Id, "Case assigned", $"{item.ReferenceNumber}: {item.Title}", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("cases/{id:guid}/status")]
    [Authorize]
    public async Task<IActionResult> ChangeStatus(Guid id, ChangeStatusRequest request)
    {
        var item = await db.ServiceRequests.FindAsync(id);
        if (item is null) return NotFound();
        var resident = User.IsInRole(CivicFlowRoles.Resident);
        if (resident && item.ResidentId != User.UserId()) return Forbid();
        if (resident && request.Status != ServiceRequestStatus.Reopened) return Forbid();
        if (!resident && User.IsInRole(CivicFlowRoles.CaseOfficer) && item.AssignedOfficerId != User.UserId()) return Forbid();
        var now = DateTimeOffset.UtcNow;
        switch (request.Status)
        {
            case ServiceRequestStatus.InProgress when item.Status == ServiceRequestStatus.WaitingForResident: item.ResumeAfterResidentReply(now); break;
            case ServiceRequestStatus.InProgress: item.StartProgress(now); break;
            case ServiceRequestStatus.WaitingForResident: item.WaitForResident(now); break;
            case ServiceRequestStatus.Resolved: item.Resolve(now); break;
            case ServiceRequestStatus.Closed: item.Close(now); break;
            case ServiceRequestStatus.Reopened: item.Reopen(now); break;
            case ServiceRequestStatus.Rejected: item.Reject(now); break;
            default: return BadRequest(new { message = "Unsupported status transition." });
        }
        AddActivity(item, "StatusChanged", $"Status changed to {item.Status}. {request.Note}".Trim(), true);
        db.UserNotifications.Add(new UserNotification(item.ResidentId, item.Id, "Request updated", $"{item.ReferenceNumber} is now {item.Status}.", now));
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("cases/{id:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(Guid id, CommentRequest request)
    {
        var item = await db.ServiceRequests.FindAsync(id);
        if (item is null) return NotFound();
        if (User.IsInRole(CivicFlowRoles.Resident) && item.ResidentId != User.UserId()) return Forbid();
        if (User.IsInRole(CivicFlowRoles.CaseOfficer) && item.AssignedOfficerId != User.UserId()) return Forbid();
        var isPublic = User.IsInRole(CivicFlowRoles.Resident) || !request.Internal;
        AddActivity(item, isPublic ? "Comment" : "InternalNote", request.Message, isPublic);
        if (User.IsInRole(CivicFlowRoles.Resident) && item.Status == ServiceRequestStatus.WaitingForResident)
            item.ResumeAfterResidentReply(DateTimeOffset.UtcNow);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("officers")]
    [Authorize(Roles = CivicFlowRoles.TeamManager + "," + CivicFlowRoles.SystemAdministrator)]
    public async Task<IActionResult> Officers()
    {
        var role = await db.Roles.SingleAsync(x => x.Name == CivicFlowRoles.CaseOfficer);
        return Ok(await (from user in db.Users join userRole in db.UserRoles on user.Id equals userRole.UserId
            where userRole.RoleId == role.Id && user.IsActive select new { user.Id, user.FirstName, user.LastName, user.Email }).ToListAsync());
    }

    private void AddActivity(ServiceRequest item, string type, string message, bool isPublic) =>
        db.CaseActivities.Add(new CaseActivity(item.Id, User.UserId(), type, message, isPublic, DateTimeOffset.UtcNow));
}

public sealed record CreateCaseRequest([Required] Guid CategoryId, [Required, MaxLength(160)] string Title,
    [Required, MaxLength(4000)] string Description, [Required, MaxLength(300)] string Address,
    decimal? Latitude, decimal? Longitude);
public sealed record TriageRequest(CasePriority Priority);
public sealed record AssignRequest(Guid OfficerId);
public sealed record ChangeStatusRequest(ServiceRequestStatus Status, string? Note);
public sealed record CommentRequest([Required, MaxLength(2000)] string Message, bool Internal = false);
public sealed record CaseListItem(Guid Id, string ReferenceNumber, string Title, string Status, string Priority,
    DateTimeOffset SubmittedAtUtc, DateTimeOffset? ResolutionDueAtUtc, Guid? AssignedOfficerId);
