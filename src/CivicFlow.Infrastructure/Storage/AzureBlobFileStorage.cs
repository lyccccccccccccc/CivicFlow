using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using CivicFlow.Application.Storage;
using Microsoft.Extensions.Options;

namespace CivicFlow.Infrastructure.Storage;

public sealed class AzureBlobFileStorage : IFileStorage
{
    private readonly BlobContainerClient container;

    public AzureBlobFileStorage(IOptions<FileStorageOptions> options)
    {
        var value = options.Value;
        if (!string.IsNullOrWhiteSpace(value.ConnectionString))
            container = new BlobContainerClient(value.ConnectionString, value.ContainerName);
        else if (value.UseManagedIdentity && value.ServiceUri is not null)
            container = new BlobContainerClient(new Uri(value.ServiceUri, value.ContainerName), new DefaultAzureCredential());
        else
            throw new InvalidOperationException("File storage requires a local connection string or a service URI with managed identity.");
    }

    public async Task StoreAsync(string storageKey, Stream content, string contentType, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        var blob = container.GetBlobClient(storageKey);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
            Metadata = metadata.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase),
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        }, cancellationToken);
    }

    public async Task<StoredFile?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await container.GetBlobClient(storageKey).DownloadStreamingAsync(cancellationToken: cancellationToken);
            return new StoredFile(response.Value.Content, response.Value.Details.ContentType ?? "application/octet-stream", response.Value.Details.ContentLength, response.Value.Details.ETag.ToString());
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }

    public async Task<bool> DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default) =>
        (await container.GetBlobClient(storageKey).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken)).Value;

    public async IAsyncEnumerable<StoredObject> ListAsync(string prefix, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await EnsureContainerAsync(cancellationToken);
        await foreach (var item in container.GetBlobsAsync(BlobTraits.Metadata, BlobStates.None, prefix, cancellationToken))
            yield return new StoredObject(item.Name, item.Properties.CreatedOn ?? DateTimeOffset.MinValue,
                new Dictionary<string, string>(item.Metadata, StringComparer.OrdinalIgnoreCase));
    }

    private async Task EnsureContainerAsync(CancellationToken cancellationToken) =>
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
}
