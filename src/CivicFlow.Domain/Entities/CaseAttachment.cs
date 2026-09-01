using CivicFlow.Domain.Common;
using CivicFlow.Domain.Enums;
using CivicFlow.Domain.Exceptions;

namespace CivicFlow.Domain.Entities;

public sealed class CaseAttachment : BaseEntity
{
    private CaseAttachment() { }

    private CaseAttachment(Guid id, Guid caseId, Guid uploadedByUserId, string originalFileName, string storageKey,
        string contentType, long sizeBytes, string sha256, AttachmentVisibility visibility, DateTimeOffset uploadedAtUtc)
    {
        Id = id; ServiceRequestId = caseId; UploadedByUserId = uploadedByUserId;
        OriginalFileName = originalFileName; StorageKey = storageKey; ContentType = contentType;
        SizeBytes = sizeBytes; Sha256 = sha256; Visibility = visibility;
        UploadedAtUtc = uploadedAtUtc; CreatedAtUtc = uploadedAtUtc;
    }

    public Guid ServiceRequestId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public string OriginalFileName { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long SizeBytes { get; private set; }
    public string Sha256 { get; private set; } = string.Empty;
    public AttachmentVisibility Visibility { get; private set; }
    public DateTimeOffset UploadedAtUtc { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public string? DeletionReason { get; private set; }

    public static CaseAttachment Create(Guid id, Guid caseId, Guid uploaderId, string originalFileName, string storageKey,
        string contentType, long sizeBytes, string sha256, AttachmentVisibility visibility, DateTimeOffset uploadedAtUtc)
    {
        if (id == Guid.Empty || caseId == Guid.Empty || uploaderId == Guid.Empty) throw new ArgumentException("Attachment identifiers are required.");
        if (string.IsNullOrWhiteSpace(originalFileName) || string.IsNullOrWhiteSpace(storageKey) || string.IsNullOrWhiteSpace(contentType))
            throw new ArgumentException("Attachment metadata is required.");
        if (sizeBytes <= 0 || sha256.Length != 64) throw new DomainRuleException("Attachment size or digest is invalid.");
        return new(id, caseId, uploaderId, originalFileName, storageKey, contentType, sizeBytes, sha256.ToLowerInvariant(), visibility, uploadedAtUtc);
    }

    public void SoftDelete(Guid deletedByUserId, string reason, DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted) return;
        var cleanReason = reason.Trim();
        if (deletedByUserId == Guid.Empty || cleanReason.Length is < 10 or > 500)
            throw new DomainRuleException("A deletion reason of 10–500 characters is required.");
        IsDeleted = true; DeletedByUserId = deletedByUserId; DeletionReason = cleanReason; DeletedAtUtc = deletedAtUtc;
        MarkUpdated(deletedAtUtc);
    }
}
