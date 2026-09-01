using CivicFlow.Domain.Entities;
using CivicFlow.Domain.Enums;
using CivicFlow.Domain.Exceptions;

namespace CivicFlow.UnitTests.Domain;

public sealed class ServiceRequestTests
{
    private static readonly DateTimeOffset SubmittedAt =
        new(2026, 8, 27, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_SetsInitialStatusToSubmitted()
    {
        var request = CreateRequest();

        Assert.Equal(ServiceRequestStatus.Submitted, request.Status);
        Assert.Equal(CasePriority.Medium, request.Priority);
        Assert.Null(request.AssignedOfficerId);
    }

    [Fact]
    public void Triage_SetsPriorityAndSlaDeadlines()
    {
        var request = CreateRequest();
        var firstResponseDue = SubmittedAt.AddHours(4);
        var resolutionDue = SubmittedAt.AddHours(24);

        request.Triage(
            CasePriority.High,
            firstResponseDue,
            resolutionDue,
            SubmittedAt.AddMinutes(15));

        Assert.Equal(ServiceRequestStatus.Triaged, request.Status);
        Assert.Equal(CasePriority.High, request.Priority);
        Assert.Equal(firstResponseDue, request.FirstResponseDueAtUtc);
        Assert.Equal(resolutionDue, request.ResolutionDueAtUtc);
    }

    [Fact]
    public void Assign_BeforeTriage_ThrowsDomainRuleException()
    {
        var request = CreateRequest();

        var exception = Assert.Throws<DomainRuleException>(() =>
            request.Assign(Guid.NewGuid(), SubmittedAt.AddMinutes(5)));

        Assert.Contains("Submitted", exception.Message);
        Assert.Contains("Assigned", exception.Message);
    }

    [Fact]
    public void Resolve_FromInProgress_SetsResolvedTimestamp()
    {
        var request = CreateRequest();
        request.Triage(
            CasePriority.High,
            SubmittedAt.AddHours(4),
            SubmittedAt.AddHours(24),
            SubmittedAt.AddMinutes(5));
        request.Assign(Guid.NewGuid(), SubmittedAt.AddMinutes(10));
        request.StartProgress(SubmittedAt.AddMinutes(15));
        var resolvedAt = SubmittedAt.AddHours(3);

        request.Resolve(resolvedAt);

        Assert.Equal(ServiceRequestStatus.Resolved, request.Status);
        Assert.Equal(resolvedAt, request.ResolvedAtUtc);
    }

    [Fact]
    public void Reopen_FromResolved_CanReturnToInProgress()
    {
        var request = CreateRequest();
        request.Triage(
            CasePriority.Medium,
            SubmittedAt.AddHours(8),
            SubmittedAt.AddHours(48),
            SubmittedAt.AddMinutes(5));
        request.Assign(Guid.NewGuid(), SubmittedAt.AddMinutes(10));
        request.StartProgress(SubmittedAt.AddMinutes(15));
        request.Resolve(SubmittedAt.AddHours(2));

        request.Reopen(SubmittedAt.AddHours(3));
        request.StartProgress(SubmittedAt.AddHours(3).AddMinutes(5));

        Assert.Equal(ServiceRequestStatus.InProgress, request.Status);
        Assert.Null(request.ResolvedAtUtc);
    }

    [Fact]
    public void UpdateTriage_AfterAssignment_ChangesTargetsWithoutRegressingStatus()
    {
        var request = CreateRequest();
        request.Triage(CasePriority.Medium, SubmittedAt.AddHours(8), SubmittedAt.AddHours(48), SubmittedAt.AddMinutes(5));
        request.Assign(Guid.NewGuid(), SubmittedAt.AddMinutes(10));

        request.UpdateTriage(CasePriority.Critical, SubmittedAt.AddHours(2), SubmittedAt.AddHours(12), SubmittedAt.AddMinutes(20));

        Assert.Equal(ServiceRequestStatus.Assigned, request.Status);
        Assert.Equal(CasePriority.Critical, request.Priority);
        Assert.Equal(SubmittedAt.AddHours(12), request.ResolutionDueAtUtc);
    }

    [Fact]
    public void Reassign_InProgress_PreservesWorkflowStatus()
    {
        var request = CreateRequest();
        request.Triage(CasePriority.High, SubmittedAt.AddHours(4), SubmittedAt.AddHours(24), SubmittedAt.AddMinutes(5));
        request.Assign(Guid.NewGuid(), SubmittedAt.AddMinutes(10));
        request.StartProgress(SubmittedAt.AddMinutes(15));
        var replacement = Guid.NewGuid();

        request.Assign(replacement, SubmittedAt.AddMinutes(20));

        Assert.Equal(ServiceRequestStatus.InProgress, request.Status);
        Assert.Equal(replacement, request.AssignedOfficerId);
    }

    [Fact]
    public void CompleteFirstResponse_RecordsOnlyTheFirstPublicResponseTime()
    {
        var request = CreateRequest();
        request.ApplyInitialSla(4, 24);

        request.CompleteFirstResponse(SubmittedAt.AddHours(3));
        request.CompleteFirstResponse(SubmittedAt.AddHours(5));

        Assert.Equal(SubmittedAt.AddHours(3), request.FirstResponseCompletedAtUtc);
    }

    private static ServiceRequest CreateRequest()
    {
        return ServiceRequest.Create(
            "CF-2026-8K4P2M",
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Damaged streetlight",
            "The streetlight has not worked for three nights.",
            "100 Example Street, Brisbane QLD",
            SubmittedAt);
    }
}
