using CivicFlow.Domain.Entities;
using CivicFlow.Domain.Enums;

namespace CivicFlow.Api.Common;

public static class SlaCalculator
{
    public static bool IsTerminal(ServiceRequest item) =>
        item.Status is ServiceRequestStatus.Resolved or ServiceRequestStatus.Closed;

    public static string FirstResponseState(ServiceRequest item, DateTimeOffset now)
    {
        if (item.FirstResponseDueAtUtc is null) return "NoSla";
        if (item.FirstResponseCompletedAtUtc.HasValue)
            return item.FirstResponseWasBreached == true ? "Breached" : "Complete";
        return TargetState(item.FirstResponseDueAtUtc.Value, now);
    }

    public static string ResolutionState(ServiceRequest item, DateTimeOffset now)
    {
        if (IsTerminal(item)) return "Complete";
        return item.ResolutionDueAtUtc is null ? "NoSla" : TargetState(item.ResolutionDueAtUtc.Value, now);
    }

    public static string OverallState(ServiceRequest item, DateTimeOffset now)
    {
        if (IsTerminal(item)) return "Complete";
        var first = item.FirstResponseCompletedAtUtc is null ? FirstResponseState(item, now) : "Complete";
        var resolution = ResolutionState(item, now);
        if (first == "Overdue" || resolution == "Overdue") return "Overdue";
        if (first == "AtRisk" || resolution == "AtRisk") return "AtRisk";
        if (first == "NoSla" || resolution == "NoSla") return "NoSla";
        return "OnTrack";
    }

    public static DateTimeOffset? NextDue(ServiceRequest item) =>
        item.FirstResponseCompletedAtUtc is null && item.FirstResponseDueAtUtc < item.ResolutionDueAtUtc
            ? item.FirstResponseDueAtUtc : item.ResolutionDueAtUtc;

    public static string? NextTarget(ServiceRequest item) =>
        item.FirstResponseCompletedAtUtc is null && item.FirstResponseDueAtUtc < item.ResolutionDueAtUtc
            ? "First response" : item.ResolutionDueAtUtc.HasValue ? "Resolution" : null;

    private static string TargetState(DateTimeOffset due, DateTimeOffset now) =>
        due < now ? "Overdue" : due <= now.AddHours(24) ? "AtRisk" : "OnTrack";
}
