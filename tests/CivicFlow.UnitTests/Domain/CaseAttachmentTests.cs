using CivicFlow.Domain.Entities;
using CivicFlow.Domain.Enums;
using CivicFlow.Domain.Exceptions;

namespace CivicFlow.UnitTests.Domain;

public sealed class CaseAttachmentTests
{
    [Fact]
    public void SoftDelete_RequiresReasonAndPreservesMetadata()
    {
        var item = CaseAttachment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "photo.jpg", "random-key", "image/jpeg", 100, new string('a', 64), AttachmentVisibility.Public, DateTimeOffset.UtcNow);
        Assert.Throws<DomainRuleException>(() => item.SoftDelete(Guid.NewGuid(), "short", DateTimeOffset.UtcNow));
        item.SoftDelete(Guid.NewGuid(), "Duplicate resident photograph.", DateTimeOffset.UtcNow);
        Assert.True(item.IsDeleted); Assert.Equal("photo.jpg", item.OriginalFileName);
    }
}
