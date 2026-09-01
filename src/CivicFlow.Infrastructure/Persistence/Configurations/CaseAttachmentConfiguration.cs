using CivicFlow.Domain.Entities;
using CivicFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicFlow.Infrastructure.Persistence.Configurations;

public sealed class CaseAttachmentConfiguration : IEntityTypeConfiguration<CaseAttachment>
{
    public void Configure(EntityTypeBuilder<CaseAttachment> builder)
    {
        builder.ToTable("CaseAttachments"); builder.HasKey(x => x.Id);
        builder.Property(x => x.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Sha256).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.Visibility).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.DeletionReason).HasMaxLength(500);
        builder.HasIndex(x => x.StorageKey).IsUnique();
        builder.HasIndex(x => new { x.ServiceRequestId, x.IsDeleted, x.UploadedAtUtc });
        builder.HasOne<ServiceRequest>().WithMany().HasForeignKey(x => x.ServiceRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.DeletedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
