using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CivicFlow.Infrastructure.Persistence;

public static class DatabaseStartup
{
    public static async Task InitialiseAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseMigrationOptions>>().Value;

        if (db.Database.IsSqlServer())
        {
            if (options.ApplyPhase2UpgradeBeforeBaseline)
            {
                if (!options.EnableLegacyBaselineRegistration)
                    throw new InvalidOperationException("Phase2Upgrade requires explicit legacy baseline registration enablement.");
                await Phase2Upgrade.ApplyAsync(services);
            }

            await LegacyDatabaseBaseline.RegisterIfRequiredAsync(services, cancellationToken);
            var allowMigration = options.AutoMigrate || environment.IsDevelopment() || environment.IsEnvironment("Testing");
            if (allowMigration) await MigrateWithLockAsync(db, cancellationToken);
            else if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
                throw new InvalidOperationException("Pending database migrations detected. Apply the reviewed migration bundle before starting production.");
        }

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            if (db.Database.IsSqlServer()) await SeedWithLockAsync(db, scope.ServiceProvider, cancellationToken);
            else await DatabaseSeeder.SeedDevelopmentAsync(scope.ServiceProvider);
        }
    }

    private static async Task MigrateWithLockAsync(ApplicationDbContext db, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource='CivicFlow.EFCoreMigrations', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=60000", cancellationToken);
            await db.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            try { await db.Database.ExecuteSqlRawAsync("EXEC sp_releaseapplock @Resource='CivicFlow.EFCoreMigrations', @LockOwner='Session'", CancellationToken.None); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }

    private static async Task SeedWithLockAsync(ApplicationDbContext db, IServiceProvider services, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await db.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource='CivicFlow.DevelopmentSeed', @LockMode='Exclusive', @LockOwner='Session', @LockTimeout=60000", cancellationToken);
            await DatabaseSeeder.SeedDevelopmentAsync(services);
        }
        finally
        {
            try { await db.Database.ExecuteSqlRawAsync("EXEC sp_releaseapplock @Resource='CivicFlow.DevelopmentSeed', @LockOwner='Session'", CancellationToken.None); }
            finally { await db.Database.CloseConnectionAsync(); }
        }
    }
}
