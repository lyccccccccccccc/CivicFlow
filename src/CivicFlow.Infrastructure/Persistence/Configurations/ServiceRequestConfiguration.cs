using CivicFlow.Domain.Entities;
using CivicFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CivicFlow.Infrastructure.Persistence.Configurations;

public sealed class ServiceRequestConfiguration : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("ServiceRequests", table => table.HasCheckConstraint(
            "CK_ServiceRequests_CoordinatePairAndRange",
            "([Latitude] IS NULL AND [Longitude] IS NULL) OR ([Latitude] BETWEEN -90 AND 90 AND [Longitude] BETWEEN -180 AND 180)"));
        builder.HasKey(request => request.Id);

        builder.Property(request => request.ReferenceNumber)
            .HasMaxLength(24)
            .IsRequired();

        builder.HasIndex(request => request.ReferenceNumber)
            .IsUnique();

        builder.Property(request => request.Title)
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(request => request.Description)
            .HasMaxLength(4000)
            .IsRequired();

        builder.Property(request => request.Address)
            .HasMaxLength(300);

        builder.Property(request => request.Latitude)
            .HasPrecision(9, 6);

        builder.Property(request => request.Longitude)
            .HasPrecision(9, 6);

        builder.Property(request => request.Priority)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(request => request.Status)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.HasIndex(request => new { request.Status, request.Priority });
        builder.HasIndex(request => request.AssignedOfficerId);
        builder.HasIndex(request => request.SubmittedAtUtc);

        builder.HasOne<ServiceCategory>()
            .WithMany()
            .HasForeignKey(request => request.ServiceCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(request => request.ResidentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(request => request.AssignedOfficerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
