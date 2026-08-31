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
    private const string Managers = CivicFlowRoles.TeamManager + "," + CivicFlowRoles.SystemAdministrator;

    [HttpGet("categories")]
    [AllowAnonymous]
    public async Task<IActionResult> Categories([FromQuery] bool includeInactive = false)
    {
        var canSeeInactive = includeInactive && User.Identity is { IsAuthenticated: true } && !User.IsInRole(CivicFlowRoles.Resident);
        return Ok(await db.ServiceCategories.AsNoTracking().Where(x => x.IsActive || canSeeInactive).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.Description, x.FirstResponseHours, x.ResolutionHours, x.IsActive }).ToListAsync());
    }

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
        item.ApplyInitialSla(category.FirstResponseHours, category.ResolutionHours);
        if (request.Latitude.HasValue && request.Longitude.HasValue)
            item.SetLocation(request.Latitude.Value, request.Longitude.Value, now);
        db.ServiceRequests.Add(item);
        AddActivity(item.Id, "Submitted", "Request submitted by resident.", true);
        await db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = item.Id }, new { item.Id, item.ReferenceNumber });
    }

    [HttpGet("cases")]
    [Authorize]
    public async Task<IActionResult> List([FromQuery] CaseListQuery request)
    {
        var now = DateTimeOffset.UtcNow;
        var query = db.ServiceRequests.AsNoTracking().AsQueryable();
        if (User.IsInRole(CivicFlowRoles.Resident)) query = query.Where(x => x.ResidentId == User.UserId());
        else if (request.Mine) query = query.Where(x => x.AssignedOfficerId == User.UserId());
        query = CaseQuery.Apply(query, request, now);

        var totalCount = await query.CountAsync();
        query = ApplySort(query, request.SortBy, request.SortDirection);
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 5, 100);
        var rows = await query.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(x => new
            {
                Item = x,
                CategoryName = db.ServiceCategories.Where(c => c.Id == x.ServiceCategoryId).Select(c => c.Name).First(),
                AssignedOfficerName = db.Users.Where(u => u.Id == x.AssignedOfficerId)
                    .Select(u => u.FirstName + " " + u.LastName).FirstOrDefault()
            }).ToListAsync();
        var items = rows.Select(x => CaseListItem.From(x.Item, x.CategoryName, x.AssignedOfficerName, now)).ToList();
        return Ok(new PagedResponse<CaseListItem>(items, page, pageSize, totalCount,
            (int)Math.Ceiling(totalCount / (double)pageSize)));
    }

    [HttpGet("cases/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Get(Guid id)
    {
        var item = await db.ServiceRequests.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        if (User.IsInRole(CivicFlowRoles.Resident) && item.ResidentId != User.UserId()) return Forbid();
        var category = await db.ServiceCategories.AsNoTracking().SingleAsync(x => x.Id == item.ServiceCategoryId);
        var officer = item.AssignedOfficerId.HasValue
            ? await db.Users.AsNoTracking().Where(x => x.Id == item.AssignedOfficerId)
                .Select(x => new { x.Id, Name = x.FirstName + " " + x.LastName, x.Email }).SingleOrDefaultAsync()
            : null;
        var activities = await db.CaseActivities.AsNoTracking()
            .Where(x => x.ServiceRequestId == id && (!User.IsInRole(CivicFlowRoles.Resident) || x.IsPublic))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToListAsync();
        return Ok(new
        {
            Case = CaseListItem.From(item, category.Name, officer?.Name, DateTimeOffset.UtcNow, true),
            Category = new { category.Id, category.Name, category.FirstResponseHours, category.ResolutionHours },
            AssignedOfficer = officer,
            Activities = ActivityFeed.Project(activities, User.IsInRole(CivicFlowRoles.Resident), item.ResidentId)
        });
    }

    [HttpPost("cases/{id:guid}/triage")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> Triage(Guid id, TriageRequest request)
    {
        var item = await db.ServiceRequests.FindAsync(id);
        if (item is null) return NotFound();
        var category = await db.ServiceCategories.FindAsync(item.ServiceCategoryId);
        var now = DateTimeOffset.UtcNow;
        var firstDue = request.FirstResponseDueAtUtc ?? item.SubmittedAtUtc.AddHours(category!.FirstResponseHours);
        var resolutionDue = request.ResolutionDueAtUtc ?? item.SubmittedAtUtc.AddHours(category!.ResolutionHours);
        var oldPriority = item.Priority;
        var oldFirstDue = item.FirstResponseDueAtUtc;
        var oldResolutionDue = item.ResolutionDueAtUtc;
        var wasSubmitted = item.Status == ServiceRequestStatus.Submitted;
        item.UpdateTriage(request.Priority, firstDue, resolutionDue, now);
        if (wasSubmitted) AddActivity(item.Id, "Triaged", "SLA targets applied.", false);
        if (oldPriority != request.Priority)
            AddActivity(item.Id, "PriorityChanged", $"Priority changed from {oldPriority} to {request.Priority}.", false);
        if (oldFirstDue != firstDue || oldResolutionDue != resolutionDue)
            AddActivity(item.Id, "SlaChanged", $"First response due {firstDue:u}; resolution due {resolutionDue:u}; baseline {item.SubmittedAtUtc:u}.", false);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("cases/{id:guid}/assign")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> Assign(Guid id, AssignRequest request)
    {
        var officer = await users.FindByIdAsync(request.OfficerId.ToString());
        if (officer is null || !officer.IsActive || !await users.IsInRoleAsync(officer, CivicFlowRoles.CaseOfficer))
            return BadRequest(new { message = "The selected user is not an active case officer." });
        var item = await db.ServiceRequests.FindAsync(id);
        if (item is null) return NotFound();
        var previousId = item.AssignedOfficerId;
        if (previousId == request.OfficerId) return NoContent();
        var previous = previousId.HasValue
            ? await db.Users.Where(x => x.Id == previousId).Select(x => x.FirstName + " " + x.LastName).FirstOrDefaultAsync()
            : null;
        item.Assign(request.OfficerId, DateTimeOffset.UtcNow);
        var type = previousId.HasValue ? "Reassigned" : "Assigned";
        var message = previousId.HasValue
            ? $"Reassigned from {previous ?? "unknown officer"} to {officer.FirstName} {officer.LastName}."
            : $"Assigned to {officer.FirstName} {officer.LastName}.";
        var activity = AddActivity(item.Id, type, message, false);
        Notify(officer.Id, item, activity, "Case assigned", $"{item.ReferenceNumber}: {item.Title}");
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
        if (User.IsInRole(CivicFlowRoles.CaseOfficer) && item.AssignedOfficerId != User.UserId()) return Forbid();
        if (request.Status == item.Status) return NoContent();
        if (request.Status is ServiceRequestStatus.Resolved or ServiceRequestStatus.Reopened or ServiceRequestStatus.Rejected &&
            (request.Note?.Trim().Length is not >= 10 or > 1800))
            return BadRequest(new { message = "A public resolution summary or reason of 10–1800 characters is required." });
        var previousStatus = item.Status;
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
        var note = string.IsNullOrWhiteSpace(request.Note) ? string.Empty : $" {request.Note.Trim()}";
        var type = item.Status == ServiceRequestStatus.InProgress && previousStatus == ServiceRequestStatus.WaitingForResident ? "WorkResumed" : item.Status.ToString();
        var label = type switch { "InProgress" => "Work is in progress", "WorkResumed" => "Work has resumed", "WaitingForResident" => "The service team is waiting for your reply", _ => type };
        var activity = AddActivity(item.Id, type, $"{label}.{note}", true);
        if (item.Status is ServiceRequestStatus.WaitingForResident or ServiceRequestStatus.Resolved or ServiceRequestStatus.Closed or ServiceRequestStatus.Reopened or ServiceRequestStatus.Rejected)
            Notify(item.ResidentId, item, activity, label, $"{item.ReferenceNumber}: {label}.{note}");
        if (item.Status == ServiceRequestStatus.Reopened && item.AssignedOfficerId.HasValue)
            Notify(item.AssignedOfficerId.Value, item, activity, "Case reopened", $"{item.ReferenceNumber}: {request.Note?.Trim()}");
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
        var key = Request.Headers["Idempotency-Key"].ToString();
        if (key.Length > 100) return BadRequest(new { message = "Idempotency key is too long." });
        var operationKey = string.IsNullOrWhiteSpace(key) ? null : $"{id}:{key}";
        if (request.Internal && User.IsInRole(CivicFlowRoles.Resident))
            return BadRequest(new { message = "Residents can only send public replies." });
        if (operationKey != null && await db.CaseActivities.AnyAsync(x => x.ActorId == User.UserId() && x.OperationKey == operationKey)) return NoContent();
        var isPublic = User.IsInRole(CivicFlowRoles.Resident) || !request.Internal;
        var activity = AddActivity(item.Id, isPublic ? "Comment" : "InternalNote", request.Message, isPublic, operationKey);
        if (User.IsInRole(CivicFlowRoles.Resident) && item.Status == ServiceRequestStatus.WaitingForResident)
        {
            item.ResumeAfterResidentReply(DateTimeOffset.UtcNow);
            AddActivity(item.Id, "ResidentReplied", "Resident replied. Work has resumed.", true);
        }
        if (isPublic && User.IsInRole(CivicFlowRoles.Resident) && item.AssignedOfficerId.HasValue)
            Notify(item.AssignedOfficerId.Value, item, activity, "Message from resident", $"{item.ReferenceNumber}: The resident sent a reply.");
        else if (isPublic && !User.IsInRole(CivicFlowRoles.Resident))
            Notify(item.ResidentId, item, activity, "Message from service officer", $"{item.ReferenceNumber}: The service team sent a message.");
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("officers")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> Officers()
    {
        var role = await db.Roles.SingleAsync(x => x.Name == CivicFlowRoles.CaseOfficer);
        return Ok(await (from user in db.Users join userRole in db.UserRoles on user.Id equals userRole.UserId
            where userRole.RoleId == role.Id && user.IsActive
            orderby user.FirstName, user.LastName
            select new { user.Id, user.FirstName, user.LastName, user.Email }).ToListAsync());
    }

    private IQueryable<ServiceRequest> ApplySort(IQueryable<ServiceRequest> query, string? sortBy, string? direction)
    {
        var descending = string.Equals(direction, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.Trim().ToLowerInvariant(), descending) switch
        {
            ("reference", false) => query.OrderBy(x => x.ReferenceNumber),
            ("reference", true) => query.OrderByDescending(x => x.ReferenceNumber),
            ("title", false) => query.OrderBy(x => x.Title),
            ("title", true) => query.OrderByDescending(x => x.Title),
            ("category", false) => query.OrderBy(x => db.ServiceCategories.Where(c => c.Id == x.ServiceCategoryId).Select(c => c.Name).First()),
            ("category", true) => query.OrderByDescending(x => db.ServiceCategories.Where(c => c.Id == x.ServiceCategoryId).Select(c => c.Name).First()),
            ("priority", false) => query.OrderBy(x => x.Priority == CasePriority.Critical ? 4 : x.Priority == CasePriority.High ? 3 : x.Priority == CasePriority.Medium ? 2 : 1),
            ("priority", true) => query.OrderByDescending(x => x.Priority == CasePriority.Critical ? 4 : x.Priority == CasePriority.High ? 3 : x.Priority == CasePriority.Medium ? 2 : 1),
            ("status", false) => query.OrderBy(x => x.Status),
            ("status", true) => query.OrderByDescending(x => x.Status),
            ("officer", false) => query.OrderBy(x => db.Users.Where(u => u.Id == x.AssignedOfficerId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault()),
            ("officer", true) => query.OrderByDescending(x => db.Users.Where(u => u.Id == x.AssignedOfficerId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault()),
            ("due", false) => query.OrderBy(x => x.ResolutionDueAtUtc),
            ("due", true) => query.OrderByDescending(x => x.ResolutionDueAtUtc),
            ("updated", false) => query.OrderBy(x => x.UpdatedAtUtc ?? x.CreatedAtUtc),
            ("updated", true) => query.OrderByDescending(x => x.UpdatedAtUtc ?? x.CreatedAtUtc),
            ("submitted", false) => query.OrderBy(x => x.SubmittedAtUtc),
            _ => query.OrderByDescending(x => x.SubmittedAtUtc)
        };
    }

    private CaseActivity AddActivity(Guid caseId, string type, string message, bool isPublic, string? operationKey = null)
    {
        var activity = new CaseActivity(caseId, User.UserId(), type, message, isPublic, DateTimeOffset.UtcNow, operationKey);
        db.CaseActivities.Add(activity);
        return activity;
    }

    private void Notify(Guid recipient, ServiceRequest item, CaseActivity activity, string title, string message)
    {
        if (recipient == User.UserId()) return;
        db.UserNotifications.Add(new(recipient, item.Id, title, message.Length > 1000 ? message[..997] + "..." : message, DateTimeOffset.UtcNow, activity.Id.ToString()));
    }
}

public sealed class CaseListQuery : CaseFilterParameters
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }
    public bool Mine { get; init; }
}

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);
public sealed record CreateCaseRequest([Required] Guid CategoryId, [Required, MaxLength(160)] string Title,
    [Required, MaxLength(4000)] string Description, [Required, MaxLength(300)] string Address,
    decimal? Latitude, decimal? Longitude);
public sealed record TriageRequest(CasePriority Priority, DateTimeOffset? FirstResponseDueAtUtc, DateTimeOffset? ResolutionDueAtUtc);
public sealed record AssignRequest(Guid OfficerId);
public sealed record ChangeStatusRequest(ServiceRequestStatus Status, [MaxLength(1800)] string? Note);
public sealed record CommentRequest([Required, MaxLength(2000)] string Message, bool Internal = false);
public sealed record CaseListItem(
    Guid Id, string ReferenceNumber, string Title, string Description, string Address,
    Guid ServiceCategoryId, string CategoryName, string Status, string Priority,
    Guid? AssignedOfficerId, string? AssignedOfficerName, DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? FirstResponseDueAtUtc, DateTimeOffset? ResolutionDueAtUtc,
    DateTimeOffset? UpdatedAtUtc, string SlaState)
{
    public static CaseListItem From(ServiceRequest item, string categoryName, string? officerName,
        DateTimeOffset now, bool includeDetails = false) => new(item.Id, item.ReferenceNumber, item.Title,
        includeDetails ? item.Description : string.Empty, includeDetails ? item.Address : string.Empty,
        item.ServiceCategoryId, categoryName, item.Status.ToString(), item.Priority.ToString(),
        item.AssignedOfficerId, officerName, item.SubmittedAtUtc, item.FirstResponseDueAtUtc,
        item.ResolutionDueAtUtc, item.UpdatedAtUtc, CaseQuery.SlaState(item, now));
}
