using CivicFlow.Domain.Entities;
using CivicFlow.Domain.Enums;

namespace CivicFlow.Api.Common;

public class CaseFilterParameters
{
    public string? Search { get; init; }
    public CasePriority? Priority { get; init; }
    public ServiceRequestStatus? Status { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? OfficerId { get; init; }
    public bool Unassigned { get; init; }
    public string? SlaState { get; init; }
    public DateTimeOffset? SubmittedFrom { get; init; }
    public DateTimeOffset? SubmittedTo { get; init; }
    public DateTimeOffset? DueFrom { get; init; }
    public DateTimeOffset? DueTo { get; init; }
    public string? QuickView { get; init; }
}

public static class CaseQuery
{
    public static IQueryable<ServiceRequest> Apply(
        IQueryable<ServiceRequest> query,
        CaseFilterParameters filters,
        DateTimeOffset now)
    {
        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var term = filters.Search.Trim();
            query = query.Where(x => x.ReferenceNumber.Contains(term) ||
                x.Title.Contains(term) || x.Address.Contains(term));
        }
        if (filters.Priority.HasValue) query = query.Where(x => x.Priority == filters.Priority);
        if (filters.Status.HasValue) query = query.Where(x => x.Status == filters.Status);
        if (filters.CategoryId.HasValue) query = query.Where(x => x.ServiceCategoryId == filters.CategoryId);
        if (filters.OfficerId.HasValue) query = query.Where(x => x.AssignedOfficerId == filters.OfficerId);
        if (filters.Unassigned) query = query.Where(x => x.AssignedOfficerId == null);
        if (filters.SubmittedFrom.HasValue) query = query.Where(x => x.SubmittedAtUtc >= filters.SubmittedFrom);
        if (filters.SubmittedTo.HasValue) query = query.Where(x => x.SubmittedAtUtc < filters.SubmittedTo.Value.AddDays(1));
        if (filters.DueFrom.HasValue) query = query.Where(x => x.ResolutionDueAtUtc >= filters.DueFrom);
        if (filters.DueTo.HasValue) query = query.Where(x => x.ResolutionDueAtUtc < filters.DueTo.Value.AddDays(1));

        var active = query.Where(x => x.Status != ServiceRequestStatus.Resolved &&
            x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected);
        query = filters.SlaState?.Trim().ToLowerInvariant() switch
        {
            "overdue" => active.Where(x => x.ResolutionDueAtUtc != null && x.ResolutionDueAtUtc < now),
            "atrisk" or "at-risk" => active.Where(x => x.ResolutionDueAtUtc >= now && x.ResolutionDueAtUtc <= now.AddHours(24)),
            "ontrack" or "on-track" => active.Where(x => x.ResolutionDueAtUtc > now.AddHours(24)),
            "nosla" or "no-sla" => query.Where(x => x.ResolutionDueAtUtc == null),
            "complete" => query.Where(x => x.Status == ServiceRequestStatus.Resolved || x.Status == ServiceRequestStatus.Closed),
            _ => query
        };

        query = filters.QuickView?.Trim().ToLowerInvariant() switch
        {
            "today" => query.Where(x => x.ResolutionDueAtUtc >= now.Date && x.ResolutionDueAtUtc < now.Date.AddDays(1)),
            "overdue" => query.Where(x => x.ResolutionDueAtUtc < now && x.Status != ServiceRequestStatus.Resolved && x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected),
            "waiting" => query.Where(x => x.Status == ServiceRequestStatus.WaitingForResident),
            "recent" => query.Where(x => (x.UpdatedAtUtc ?? x.CreatedAtUtc) >= now.AddDays(-7)),
            "open" => query.Where(x => x.Status != ServiceRequestStatus.Resolved && x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected),
            _ => query
        };
        return query;
    }

    public static string SlaState(ServiceRequest item, DateTimeOffset now)
    {
        if (item.Status is ServiceRequestStatus.Resolved or ServiceRequestStatus.Closed) return "Complete";
        if (item.ResolutionDueAtUtc is null) return "NoSla";
        if (item.ResolutionDueAtUtc < now) return "Overdue";
        return item.ResolutionDueAtUtc <= now.AddHours(24) ? "AtRisk" : "OnTrack";
    }
}
