using CivicFlow.Domain.Entities;
using CivicFlow.Domain.Enums;
using CivicFlow.Api.Common;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CivicFlow.Api.Background;

public sealed class SlaMonitorWorker(IServiceScopeFactory scopeFactory, ILogger<SlaMonitorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                logger.LogError(exception, "SLA monitoring cycle failed.");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }

    private async Task ScanAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTimeOffset.UtcNow;
        var candidates = await db.ServiceRequests.Where(x => x.AssignedOfficerId != null &&
            x.Status != ServiceRequestStatus.Resolved && x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected &&
            ((x.FirstResponseCompletedAtUtc == null && x.FirstResponseDueAtUtc <= now.AddHours(24)) || x.ResolutionDueAtUtc <= now.AddHours(24)))
            .ToListAsync(cancellationToken);
        foreach (var item in candidates)
        {
            var due = SlaCalculator.NextDue(item)!.Value;
            var target = SlaCalculator.NextTarget(item)!;
            var state = due < now ? "overdue" : "at risk";
            var key = $"sla:{item.Id}:{target}:{due:O}:{state}";
            var alreadySent = await db.UserNotifications.AnyAsync(x => x.UserId == item.AssignedOfficerId && x.EventKey == key, cancellationToken);
            if (!alreadySent) db.UserNotifications.Add(new UserNotification(item.AssignedOfficerId!.Value, item.Id,
                $"SLA {state}", $"{item.ReferenceNumber}: the {target.ToLowerInvariant()} target is {state}.", now, key));
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);
    }
}
