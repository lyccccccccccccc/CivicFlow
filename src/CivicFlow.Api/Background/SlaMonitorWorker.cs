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
        var threshold = now.AddHours(-12);
        var overdue = await db.ServiceRequests.Where(x => x.AssignedOfficerId != null && x.ResolutionDueAtUtc < now &&
            x.Status != ServiceRequestStatus.Resolved && x.Status != ServiceRequestStatus.Closed && x.Status != ServiceRequestStatus.Rejected).ToListAsync(cancellationToken);
        foreach (var item in overdue)
        {
            var alreadySent = await db.UserNotifications.AnyAsync(x => x.UserId == item.AssignedOfficerId &&
                x.ServiceRequestId == item.Id && x.Title == "SLA overdue" && x.CreatedAtUtc >= threshold, cancellationToken);
            if (!alreadySent) db.UserNotifications.Add(new UserNotification(item.AssignedOfficerId!.Value, item.Id,
                "SLA overdue", $"{item.ReferenceNumber} has passed its resolution target.", now));
        }
        if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync(cancellationToken);
    }
}
