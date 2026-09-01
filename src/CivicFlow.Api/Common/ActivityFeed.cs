using CivicFlow.Domain.Entities;

namespace CivicFlow.Api.Common;

public sealed record ActivityView(Guid Id, string Type, string Label, string Message, string Section,
    bool IsPublic, DateTimeOffset CreatedAtUtc, string ActorName);

/// <summary>Fail-closed business projection. Audit records are never returned by a case detail endpoint.</summary>
public static class ActivityFeed
{
    public static IReadOnlyList<ActivityView> Project(IEnumerable<CaseActivity> source, bool resident, Guid residentId)
    {
        var result = new List<ActivityView>();
        foreach (var a in source.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id))
        {
            if (resident && !a.IsPublic) continue;
            var type = a.Type;
            var message = a.Message;
            // Historical audit remains unchanged; only known legacy workflow records get a safe projection.
            if (type is "StatusChanged" or "Resolved")
            {
                var known = new[] { "InProgress", "WaitingForResident", "Resolved", "Closed", "Reopened", "Rejected" };
                var legacy = known.FirstOrDefault(s => message.StartsWith($"Status changed to {s}.", StringComparison.Ordinal));
                if (legacy != null) { type = legacy; message = message[(legacy.Length + 19)..].Trim(); }
                else if (type == "StatusChanged") continue;
            }
            var fromResident = a.ActorId == residentId;
            var label = type switch
            {
                "Comment" => fromResident ? "Message from resident" : "Message from service officer",
                "InternalNote" when !resident => "Internal note",
                "Submitted" => "Submitted",
                "Assigned" when !resident => "Assigned",
                "Reassigned" when !resident => "Reassigned",
                "InProgress" => "In progress",
                "WorkResumed" => "Work resumed",
                "ResidentReplied" => "Work resumed",
                "WaitingForResident" => "Waiting for resident",
                "Resolved" => "Resolved",
                "Closed" => "Closed",
                "Reopened" => "Reopened",
                "Rejected" => "Rejected",
                _ => null
            };
            if (label == null) continue;
            var section = type == "Comment" ? "conversation" : type == "InternalNote" ? "internal" : "progress";
            if (type == "ResidentReplied") message = "Resident replied. Work has resumed.";
            else if (string.IsNullOrWhiteSpace(message)) message = label + ".";
            // Collapse consecutive identical workflow milestones, never public messages or summaries.
            if (section == "progress" && result.LastOrDefault(x => x.Section == "progress") is { } last && last.Type == type && last.Message == message) continue;
            // A rapid state-toggle burst with no intervening conversation is not several resident milestones.
            var working = new[] { "InProgress", "WorkResumed", "WaitingForResident" };
            if (resident && working.Contains(type) && result.LastOrDefault() is { } previous &&
                working.Contains(previous.Type) && a.CreatedAtUtc - previous.CreatedAtUtc < TimeSpan.FromMinutes(5))
                result.RemoveAt(result.Count - 1);
            result.Add(new(a.Id, type, label, message, section, a.IsPublic, a.CreatedAtUtc,
                fromResident ? "Resident" : a.ActorId == Guid.Empty ? "System" : "Service team"));
            // Legacy resident replies resumed work without a dedicated workflow record.
            if (type == "Comment" && fromResident && result.LastOrDefault(x => x.Section == "progress")?.Type == "WaitingForResident")
                result.Add(new(a.Id, "ResidentReplied", "Work resumed", "Resident replied. Work has resumed.", "progress", true, a.CreatedAtUtc, "Resident"));
        }
        return result.OrderByDescending(x => x.CreatedAtUtc).ToList();
    }
}
