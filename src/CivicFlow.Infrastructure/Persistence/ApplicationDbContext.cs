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

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        builder.Entity<CaseActivity>(entity =>
        {
            entity.Property(x => x.Type).HasMaxLength(40);
            entity.Property(x => x.Message).HasMaxLength(2000);
            entity.HasIndex(x => new { x.ServiceRequestId, x.CreatedAtUtc });
        });
        builder.Entity<UserNotification>(entity =>
        {
            entity.Property(x => x.Title).HasMaxLength(160);
            entity.Property(x => x.Message).HasMaxLength(1000);
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
