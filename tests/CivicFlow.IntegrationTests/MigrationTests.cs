using CivicFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CivicFlow.IntegrationTests;

public sealed class MigrationTests
{
    [Fact]
    public void MigrationAssembly_StartsWithFrozenPhase2Baseline()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=MetadataOnly;Integrated Security=true;TrustServerCertificate=true")
            .Options;
        using var db = new ApplicationDbContext(options);
        Assert.Equal(LegacyDatabaseBaseline.InitialMigrationId, db.Database.GetMigrations().First());
    }

    [Fact]
    public void LegacyBaseline_IsSecureByDefault()
    {
        var options = new DatabaseMigrationOptions();
        Assert.False(options.EnableLegacyBaselineRegistration);
        Assert.False(options.ApplyPhase2UpgradeBeforeBaseline);
        Assert.False(options.AutoMigrate);
    }
}
