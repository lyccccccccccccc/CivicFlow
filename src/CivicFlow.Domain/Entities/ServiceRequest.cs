using CivicFlow.Domain.Common;
using CivicFlow.Domain.Enums;
using CivicFlow.Domain.Exceptions;

namespace CivicFlow.Domain.Entities;

public sealed class ServiceRequest : BaseEntity
{
    private ServiceRequest()
    {
    }

    private ServiceRequest(
        string referenceNumber,
        Guid residentId,
        Guid categoryId,
        string title,
        string description,
        string address,
        DateTimeOffset submittedAtUtc)
    {
        Id = Guid.NewGuid();
        ReferenceNumber = referenceNumber;
        ResidentId = residentId;
        ServiceCategoryId = categoryId;
        Title = title;
        Description = description;
        Address = address;
        Priority = CasePriority.Medium;
        Status = ServiceRequestStatus.Submitted;
        SubmittedAtUtc = submittedAtUtc;
        CreatedAtUtc = submittedAtUtc;
    }

    public string ReferenceNumber { get; private set; } = string.Empty;

    public Guid ResidentId { get; private set; }

    public Guid ServiceCategoryId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Address { get; private set; } = string.Empty;

    public decimal? Latitude { get; private set; }

    public decimal? Longitude { get; private set; }

    public CasePriority Priority { get; private set; }

    public ServiceRequestStatus Status { get; private set; }

    public Guid? AssignedOfficerId { get; private set; }

    public DateTimeOffset SubmittedAtUtc { get; private set; }

    public DateTimeOffset? FirstResponseDueAtUtc { get; private set; }

    public DateTimeOffset? ResolutionDueAtUtc { get; private set; }

    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    public DateTimeOffset? ClosedAtUtc { get; private set; }

    public static ServiceRequest Create(
        string referenceNumber,
        Guid residentId,
        Guid categoryId,
        string title,
        string description,
        string address,
        DateTimeOffset submittedAtUtc)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
        {
            throw new ArgumentException("Reference number is required.", nameof(referenceNumber));
        }

        if (residentId == Guid.Empty || categoryId == Guid.Empty)
        {
            throw new ArgumentException("Resident and category identifiers are required.");
        }

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Title and description are required.");
        }

        return new ServiceRequest(
            referenceNumber.Trim(),
            residentId,
            categoryId,
            title.Trim(),
            description.Trim(),
            address.Trim(),
            submittedAtUtc);
    }

    public void SetLocation(decimal latitude, decimal longitude, DateTimeOffset updatedAtUtc)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), "Coordinates are outside valid ranges.");
        }

        Latitude = latitude;
        Longitude = longitude;
        MarkUpdated(updatedAtUtc);
    }

    public void Triage(
        CasePriority priority,
        DateTimeOffset firstResponseDueAtUtc,
        DateTimeOffset resolutionDueAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        UpdateTriage(priority, firstResponseDueAtUtc, resolutionDueAtUtc, updatedAtUtc);
    }

    public void UpdateTriage(
        CasePriority priority,
        DateTimeOffset firstResponseDueAtUtc,
        DateTimeOffset resolutionDueAtUtc,
        DateTimeOffset updatedAtUtc)
    {
        if (Status is ServiceRequestStatus.Resolved or ServiceRequestStatus.Closed or ServiceRequestStatus.Rejected)
            throw new DomainRuleException($"Triage cannot be changed while the request is {Status}.");
        if (firstResponseDueAtUtc <= SubmittedAtUtc || resolutionDueAtUtc <= firstResponseDueAtUtc)
            throw new DomainRuleException("SLA due dates must follow the submission time in sequence.");

        Priority = priority;
        FirstResponseDueAtUtc = firstResponseDueAtUtc;
        ResolutionDueAtUtc = resolutionDueAtUtc;
        if (Status == ServiceRequestStatus.Submitted) Status = ServiceRequestStatus.Triaged;
        MarkUpdated(updatedAtUtc);
    }

    public void Assign(Guid officerId, DateTimeOffset updatedAtUtc)
    {
        if (Status is ServiceRequestStatus.Submitted or ServiceRequestStatus.Resolved or
            ServiceRequestStatus.Closed or ServiceRequestStatus.Rejected)
        {
            throw InvalidTransition(ServiceRequestStatus.Assigned);
        }

        if (officerId == Guid.Empty)
        {
            throw new ArgumentException("Officer identifier is required.", nameof(officerId));
        }

        AssignedOfficerId = officerId;
        if (Status is ServiceRequestStatus.Triaged or ServiceRequestStatus.Assigned)
            Status = ServiceRequestStatus.Assigned;
        MarkUpdated(updatedAtUtc);
    }

    public void StartProgress(DateTimeOffset updatedAtUtc)
    {
        if (Status is not (ServiceRequestStatus.Assigned or ServiceRequestStatus.Reopened))
        {
            throw InvalidTransition(ServiceRequestStatus.InProgress);
        }

        Status = ServiceRequestStatus.InProgress;
        MarkUpdated(updatedAtUtc);
    }

    public void WaitForResident(DateTimeOffset updatedAtUtc)
    {
        EnsureStatus(ServiceRequestStatus.InProgress);
        Status = ServiceRequestStatus.WaitingForResident;
        MarkUpdated(updatedAtUtc);
    }

    public void ResumeAfterResidentReply(DateTimeOffset updatedAtUtc)
    {
        EnsureStatus(ServiceRequestStatus.WaitingForResident);
        Status = ServiceRequestStatus.InProgress;
        MarkUpdated(updatedAtUtc);
    }

    public void Resolve(DateTimeOffset resolvedAtUtc)
    {
        EnsureStatus(ServiceRequestStatus.InProgress);
        Status = ServiceRequestStatus.Resolved;
        ResolvedAtUtc = resolvedAtUtc;
        MarkUpdated(resolvedAtUtc);
    }

    public void Close(DateTimeOffset closedAtUtc)
    {
        EnsureStatus(ServiceRequestStatus.Resolved);
        Status = ServiceRequestStatus.Closed;
        ClosedAtUtc = closedAtUtc;
        MarkUpdated(closedAtUtc);
    }

    public void Reopen(DateTimeOffset reopenedAtUtc)
    {
        EnsureStatus(ServiceRequestStatus.Resolved);
        Status = ServiceRequestStatus.Reopened;
        ResolvedAtUtc = null;
        MarkUpdated(reopenedAtUtc);
    }

    public void Reject(DateTimeOffset rejectedAtUtc)
    {
        if (Status is not (ServiceRequestStatus.Submitted or ServiceRequestStatus.Triaged))
        {
            throw InvalidTransition(ServiceRequestStatus.Rejected);
        }

        Status = ServiceRequestStatus.Rejected;
        MarkUpdated(rejectedAtUtc);
    }

    private void EnsureStatus(ServiceRequestStatus requiredStatus)
    {
        if (Status != requiredStatus)
        {
            throw new DomainRuleException(
                $"This action requires status {requiredStatus}, but the request is {Status}.");
        }
    }

    private DomainRuleException InvalidTransition(ServiceRequestStatus targetStatus)
    {
        return new DomainRuleException($"A service request cannot move from {Status} to {targetStatus}.");
    }
}
