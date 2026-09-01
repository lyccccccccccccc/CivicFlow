namespace CivicFlow.Application.Storage;

public sealed record StoredFile(Stream Content, string ContentType, long Length, string ETag);
public sealed record StoredObject(string StorageKey, DateTimeOffset CreatedAtUtc, IReadOnlyDictionary<string, string> Metadata);

public interface IFileStorage
{
    Task StoreAsync(string storageKey, Stream content, string contentType, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default);
    Task<StoredFile?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default);
    Task<bool> DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default);
    IAsyncEnumerable<StoredObject> ListAsync(string prefix, CancellationToken cancellationToken = default);
}
