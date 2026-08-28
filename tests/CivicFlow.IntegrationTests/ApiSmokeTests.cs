using System.Net;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
            services.RemoveAll<ApplicationDbContext>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase($"CivicFlowTests-{Guid.NewGuid()}"));
        });
    }
}
