using CivicFlow.Domain.Common;

namespace CivicFlow.Domain.Entities;

public sealed class UserNotification : BaseEntity
{
    private UserNotification() { }

    public UserNotification(Guid userId, Guid? requestId, string title, string message, DateTimeOffset createdAtUtc)
    {
        UserId = userId;
        ServiceRequestId = requestId;
        Title = title.Trim();
        Message = message.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid UserId { get; private set; }
    public Guid? ServiceRequestId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public DateTimeOffset? ReadAtUtc { get; private set; }
    public void MarkRead(DateTimeOffset now) => ReadAtUtc ??= now;
}
