namespace CivicFlow.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    public DateTimeOffset CreatedAtUtc { get; protected set; }

    public DateTimeOffset? UpdatedAtUtc { get; protected set; }

    protected void MarkUpdated(DateTimeOffset updatedAtUtc)
    {
        UpdatedAtUtc = updatedAtUtc;
    }
}
