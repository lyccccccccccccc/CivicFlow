using CivicFlow.Domain.Common;

namespace CivicFlow.Domain.Entities;

public sealed class CaseActivity : BaseEntity
{
    private CaseActivity() { }

    public CaseActivity(Guid requestId, Guid actorId, string type, string message, bool isPublic, DateTimeOffset createdAtUtc)
    {
        ServiceRequestId = requestId;
        ActorId = actorId;
        Type = type.Trim();
        Message = message.Trim();
        IsPublic = isPublic;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid ServiceRequestId { get; private set; }
    public Guid ActorId { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public bool IsPublic { get; private set; }
}
