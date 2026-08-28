namespace CivicFlow.Domain.Enums;

public enum ServiceRequestStatus
{
    Submitted = 1,
    Triaged = 2,
    Assigned = 3,
    InProgress = 4,
    WaitingForResident = 5,
    Resolved = 6,
    Closed = 7,
    Reopened = 8,
    Rejected = 9
}
