namespace CivicFlow.Infrastructure.Persistence;

public sealed class DatabaseMigrationOptions
{
    public const string SectionName = "DatabaseMigration";
    public bool AutoMigrate { get; init; }
    public bool EnableLegacyBaselineRegistration { get; init; }
    public bool ApplyPhase2UpgradeBeforeBaseline { get; init; }
}
