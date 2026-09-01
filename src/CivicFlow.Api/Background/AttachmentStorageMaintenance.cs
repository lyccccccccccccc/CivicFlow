using CivicFlow.Application.Storage;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CivicFlow.Api.Background;

public sealed class AttachmentStorageMaintenance(ApplicationDbContext db, IFileStorage storage, IOptions<FileStorageOptions> options, ILogger<AttachmentStorageMaintenance> logger)
{
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, options.Value.SoftDeleteRetentionDays));
        var deleted = await db.CaseAttachments.AsNoTracking().Where(x => x.IsDeleted && x.DeletedAtUtc <= cutoff).Select(x => x.StorageKey).ToListAsync(cancellationToken);
        foreach (var key in deleted)
            try { await storage.DeleteIfExistsAsync(key, cancellationToken); }
            catch (Exception ex) { logger.LogError(ex, "A retained attachment blob could not be removed; cleanup will retry."); }

        var orphanCutoff = DateTimeOffset.UtcNow.AddHours(-24);
        await foreach (var blob in storage.ListAsync("cases/", cancellationToken))
        {
            if (blob.CreatedAtUtc > orphanCutoff) continue;
            if (!await db.CaseAttachments.AnyAsync(x => x.StorageKey == blob.StorageKey, cancellationToken))
                try { await storage.DeleteIfExistsAsync(blob.StorageKey, cancellationToken); }
                catch (Exception ex) { logger.LogError(ex, "An orphan attachment blob could not be removed; cleanup will retry."); }
        }
    }
}

public sealed class AttachmentCleanupWorker(IServiceScopeFactory scopeFactory, ILogger<AttachmentCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<AttachmentStorageMaintenance>().RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Attachment storage maintenance failed and will retry later."); }
            if (!stoppingToken.IsCancellationRequested) await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
        }
    }
}
