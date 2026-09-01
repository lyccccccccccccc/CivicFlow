using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CivicFlow.Infrastructure.Persistence;

/// <summary>Additive upgrade for the original EnsureCreated database; never recreates or seeds it.</summary>
public static class Phase2Upgrade
{
    public static async Task ApplyAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!db.Database.IsSqlServer()) return;
        await using var transaction = await db.Database.BeginTransactionAsync();
        await db.Database.ExecuteSqlRawAsync("""
            EXEC sp_getapplock @Resource='CivicFlow.Phase2Hardening', @LockMode='Exclusive', @LockOwner='Transaction';
            IF COL_LENGTH('CaseActivities', 'OperationKey') IS NULL
                ALTER TABLE CaseActivities ADD OperationKey nvarchar(160) NULL;
            IF COL_LENGTH('UserNotifications', 'EventKey') IS NULL
                ALTER TABLE UserNotifications ADD EventKey nvarchar(160) NULL;
            IF COL_LENGTH('ServiceRequests', 'FirstResponseCompletedAtUtc') IS NULL
                ALTER TABLE ServiceRequests ADD FirstResponseCompletedAtUtc datetimeoffset NULL;
            IF COL_LENGTH('ServiceRequests', 'FirstResponseWasBreached') IS NULL
                ALTER TABLE ServiceRequests ADD FirstResponseWasBreached bit NULL;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE request
            SET FirstResponseCompletedAtUtc = response.CompletedAtUtc,
                FirstResponseWasBreached = CASE WHEN response.CompletedAtUtc > request.FirstResponseDueAtUtc THEN 1 ELSE 0 END
            FROM ServiceRequests request
            CROSS APPLY (
                SELECT MIN(activity.CreatedAtUtc) CompletedAtUtc
                FROM CaseActivities activity
                WHERE activity.ServiceRequestId = request.Id
                  AND activity.Type = 'Comment' AND activity.IsPublic = 1
                  AND activity.ActorId <> request.ResidentId
            ) response
            WHERE (request.FirstResponseCompletedAtUtc IS NULL OR request.FirstResponseWasBreached IS NULL) AND response.CompletedAtUtc IS NOT NULL;
            """);
        await db.Database.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_CaseActivities_ActorId_OperationKey')
                CREATE UNIQUE INDEX IX_CaseActivities_ActorId_OperationKey ON CaseActivities(ActorId, OperationKey) WHERE OperationKey IS NOT NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name='IX_UserNotifications_UserId_EventKey')
                CREATE UNIQUE INDEX IX_UserNotifications_UserId_EventKey ON UserNotifications(UserId, EventKey) WHERE EventKey IS NOT NULL;
            """);
        // Preserve existing targets/history. Only legacy requests with no targets get a baseline.
        var missing = await db.ServiceRequests.Where(x => x.FirstResponseDueAtUtc == null && x.ResolutionDueAtUtc == null).ToListAsync();
        var categories = await db.ServiceCategories.ToDictionaryAsync(x => x.Id);
        foreach (var item in missing)
        {
            var category = categories[item.ServiceCategoryId];
            item.ApplyInitialSla(category.FirstResponseHours, category.ResolutionHours);
            db.CaseActivities.Add(new(item.Id, Guid.Empty, "SlaChanged", "Applied category baseline from original submission during Phase 2 upgrade.", false, DateTimeOffset.UtcNow));
        }
        var responseAuditMissing = await db.ServiceRequests.Where(x => x.FirstResponseCompletedAtUtc != null &&
            !db.CaseActivities.Any(a => a.ServiceRequestId == x.Id && a.Type == "FirstResponseCompleted")).ToListAsync();
        foreach (var item in responseAuditMissing)
        {
            var result = item.FirstResponseWasBreached == true ? "breached" : "within target";
            db.CaseActivities.Add(new(item.Id, Guid.Empty, "FirstResponseCompleted",
                $"Derived first resident-visible staff response at {item.FirstResponseCompletedAtUtc:u}; {result}; due {item.FirstResponseDueAtUtc:u}.",
                false, DateTimeOffset.UtcNow));
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
