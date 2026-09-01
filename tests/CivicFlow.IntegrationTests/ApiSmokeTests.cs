using System.Net;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CivicFlow.Application.Storage;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace CivicFlow.IntegrationTests;

public sealed class ApiSmokeTests : IClassFixture<CivicFlowFactory>
{
    private readonly HttpClient _client;

    public ApiSmokeTests(CivicFlowFactory factory) => _client = factory.CreateClient();

    [Theory]
    [InlineData("/health")]
    [InlineData("/api/system/status")]
    [InlineData("/api/categories")]
    public async Task PublicEndpoints_ReturnSuccess(string route)
    {
        var response = await _client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CasesEndpoint_RequiresAuthentication()
    {
        var response = await _client.GetAsync("/api/cases");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class CivicFlowFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"CivicFlowTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                var sql = Environment.GetEnvironmentVariable("CIVICFLOW_TEST_SQL");
                if (string.IsNullOrWhiteSpace(sql)) options.UseInMemoryDatabase(_databaseName);
                else options.UseSqlServer(sql);
            });
            services.RemoveAll<IFileStorage>();
            services.AddSingleton<TestFileStorage>();
            services.AddSingleton<IFileStorage>(x => x.GetRequiredService<TestFileStorage>());
        });
    }
}

public sealed class TestFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, (byte[] Data, string Type, DateTimeOffset Created, Dictionary<string, string> Metadata)> files = new();
    public async Task StoreAsync(string storageKey, Stream content, string contentType, IReadOnlyDictionary<string, string> metadata, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream(); await content.CopyToAsync(buffer, cancellationToken);
        if (!files.TryAdd(storageKey, (buffer.ToArray(), contentType, DateTimeOffset.UtcNow, new(metadata)))) throw new InvalidOperationException("Duplicate test blob.");
    }
    public Task<StoredFile?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult<StoredFile?>(files.TryGetValue(storageKey, out var value) ? new(new MemoryStream(value.Data), value.Type, value.Data.Length, "test-etag") : null);
    public Task<bool> DeleteIfExistsAsync(string storageKey, CancellationToken cancellationToken = default) => Task.FromResult(files.TryRemove(storageKey, out _));
    public async IAsyncEnumerable<StoredObject> ListAsync(string prefix, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    { foreach (var item in files.Where(x => x.Key.StartsWith(prefix, StringComparison.Ordinal))) { cancellationToken.ThrowIfCancellationRequested(); yield return new(item.Key, item.Value.Created, item.Value.Metadata); await Task.Yield(); } }
}
