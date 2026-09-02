using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CivicFlow.Infrastructure.Persistence;

public static class LegacyDatabaseBaseline
{
    public const string InitialMigrationId = "20260901013912_InitialCivicFlowSchema";

    public static async Task RegisterIfRequiredAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        if (!db.Database.IsSqlServer()) return;

        // A brand-new database has no legacy schema to register. Returning here
        // lets the normal EF migrator create the database and apply Initial.
        // Existing databases remain subject to the strict semantic validator.
        if (!await db.GetService<IRelationalDatabaseCreator>().ExistsAsync(cancellationToken)) return;

        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var probe = connection.CreateCommand();
        probe.CommandText = "SELECT CASE WHEN OBJECT_ID('dbo.ServiceRequests','U') IS NULL THEN 0 ELSE 1 END, CASE WHEN OBJECT_ID('dbo.__EFMigrationsHistory','U') IS NULL THEN 0 ELSE 1 END";
        await using var reader = await probe.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken); var hasSchema = reader.GetInt32(0) == 1; var hasHistory = reader.GetInt32(1) == 1;
        await reader.CloseAsync();
        if (!hasSchema || hasHistory) return;

        var options = scope.ServiceProvider.GetRequiredService<IOptions<DatabaseMigrationOptions>>().Value;
        if (!options.EnableLegacyBaselineRegistration)
            throw new InvalidOperationException("A legacy CivicFlow database without migration history was detected. Baseline registration is disabled.");

        var result = await scope.ServiceProvider.GetRequiredService<SchemaBaselineValidator>().ValidateAsync(cancellationToken);
        if (!result.IsValid)
            throw new InvalidOperationException("Legacy database schema does not match the Phase 2 baseline:\n" + string.Join('\n', result.Differences));

        var history = db.GetService<IHistoryRepository>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        await db.Database.ExecuteSqlRawAsync("EXEC sp_getapplock @Resource='CivicFlow.EFMigrationBaseline', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=60000", cancellationToken);
        await db.Database.ExecuteSqlRawAsync(history.GetCreateIfNotExistsScript(), cancellationToken);
        await db.Database.ExecuteSqlRawAsync(history.GetInsertScript(new HistoryRow(InitialMigrationId, "10.0.11")), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
