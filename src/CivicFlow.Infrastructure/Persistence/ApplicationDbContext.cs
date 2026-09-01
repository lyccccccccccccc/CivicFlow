using CivicFlow.Domain.Entities;
using CivicFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CivicFlow.Infrastructure.Persistence;

public sealed class ApplicationDbContext(
    DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();

    public DbSet<ServiceRequest> ServiceRequests => Set<ServiceRequest>();

    public DbSet<CaseActivity> CaseActivities => Set<CaseActivity>();

    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

    public DbSet<CaseAttachment> CaseAttachments => Set<CaseAttachment>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    private void EnsureAuditAppendOnly()
    {
        if (ChangeTracker.Entries<CaseActivity>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Audit records are append-only.");
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAuditAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureAuditAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        builder.Entity<ServiceRequest>().Property(x => x.UpdatedAtUtc).IsConcurrencyToken();

        builder.Entity<CaseActivity>(entity =>
        {
            entity.Property(x => x.Type).HasMaxLength(40);
            entity.Property(x => x.Message).HasMaxLength(2000);
            entity.Property(x => x.OperationKey).HasMaxLength(160);
            entity.HasIndex(x => new { x.ActorId, x.OperationKey }).IsUnique().HasFilter("[OperationKey] IS NOT NULL");
            entity.HasIndex(x => new { x.ServiceRequestId, x.CreatedAtUtc });
        });
        builder.Entity<UserNotification>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Message).HasMaxLength(1000);
            entity.Property(x => x.EventKey).HasMaxLength(160);
            entity.HasIndex(x => new { x.UserId, x.EventKey }).IsUnique().HasFilter("[EventKey] IS NOT NULL");
            entity.HasIndex(x => new { x.UserId, x.ReadAtUtc });
        });
        builder.Entity<RefreshToken>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(128);
            entity.HasIndex(x => x.TokenHash).IsUnique();
        });
    }
}
