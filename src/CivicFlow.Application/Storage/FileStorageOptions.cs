namespace CivicFlow.Application.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";
    public string ContainerName { get; init; } = "case-attachments";
    public string? ConnectionString { get; init; }
    public Uri? ServiceUri { get; init; }
    public bool UseManagedIdentity { get; init; }
    public int SoftDeleteRetentionDays { get; init; } = 30;
}
