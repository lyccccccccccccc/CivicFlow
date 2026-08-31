using CivicFlow.Domain.Entities;
using CivicFlow.Domain.Enums;
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
        var overdue = await db.ServiceRequests.Where(x => x.AssignedOfficerId != null && x.ResolutionDueAtUtc <= now.AddHours(24) &&
            x.Status != ServiceRequestStatus.Resolved && x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected).ToListAsync(cancellationToken);
        foreach (var item in overdue)
        {
            var state = item.ResolutionDueAtUtc < now ? "overdue" : "at risk";
            var key = $"sla:{item.Id}:{item.ResolutionDueAtUtc:O}:{state}";
            var alreadySent = await db.UserNotifications.AnyAsync(x => x.UserId == item.AssignedOfficerId && x.EventKey == key, cancellationToken);
            if (!alreadySent) db.UserNotifications.Add(new UserNotification(item.AssignedOfficerId!.Value, item.Id,
                $"SLA {state}", $"{item.ReferenceNumber}: the resolution target is {state}.", now, key));
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);
    }
}
