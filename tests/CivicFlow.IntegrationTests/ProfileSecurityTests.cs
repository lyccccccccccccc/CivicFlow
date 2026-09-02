using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CivicFlow.Infrastructure.Identity;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CivicFlow.IntegrationTests;

public sealed class ProfileSecurityTests : IClassFixture<CivicFlowFactory>
{
    private readonly CivicFlowFactory factory;
    private readonly HttpClient client;

    public ProfileSecurityTests(CivicFlowFactory factory)
    {
        this.factory = factory;
        client = factory.CreateClient();
    }

    [Fact]
    public async Task Profile_IsAvailableToAllFourRoles_AndCannotAddressAnotherUser()
    {
        foreach (var account in new[]
                 {
                     ("resident@civicflow.local", CivicFlowRoles.Resident),
                     ("officer@civicflow.local", CivicFlowRoles.CaseOfficer),
                     ("manager@civicflow.local", CivicFlowRoles.TeamManager),
                     ("admin@civicflow.local", CivicFlowRoles.SystemAdministrator)
                 })
        {
            var auth = await Login(account.Item1);
            Authorize(auth.Token);
            var profile = await client.GetFromJsonAsync<JsonElement>("/api/profile");
            Assert.Equal(auth.UserId, profile.GetProperty("id").GetGuid());
            Assert.Equal(account.Item2, profile.GetProperty("roles")[0].GetString());
            Assert.True(profile.GetProperty("isActive").GetBoolean());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/profile/{Guid.NewGuid()}")).StatusCode);
        }
    }

    [Fact]
    public async Task Profile_Update_IsValidatedOwnOnlyNoOpAndConcurrencyProtected()
    {
        var email = $"profile-{Guid.NewGuid():N}@example.test";
        var auth = await Register(email, "Original Profile");
        Authorize(auth.Token);
        var initial = await client.GetFromJsonAsync<JsonElement>("/api/profile");
        var version = initial.GetProperty("version").GetString()!;

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/profile", new { fullName = "   ", version })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/api/profile", new { fullName = new string('A', 151), version })).StatusCode);

        using var beforeScope = factory.Services.CreateScope();
        var beforeDb = beforeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var auditBefore = await beforeDb.CaseActivities.CountAsync(x => x.ActorId == auth.UserId && x.Type == "ProfileUpdated");
        var noOp = await client.PutAsJsonAsync("/api/profile", new { fullName = "  Original   Profile ", version });
        Assert.Equal(HttpStatusCode.OK, noOp.StatusCode);

        var changed = await client.PutAsJsonAsync("/api/profile", new
        {
            fullName = "Updated Profile", version, id = Guid.NewGuid(), email = "attacker@example.test",
            roles = new[] { CivicFlowRoles.SystemAdministrator }, isActive = false
        });
        changed.EnsureSuccessStatusCode();
        var updated = await changed.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(email, updated.GetProperty("email").GetString());
        Assert.Equal(CivicFlowRoles.Resident, updated.GetProperty("roles")[0].GetString());
        Assert.True(updated.GetProperty("isActive").GetBoolean());
        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync("/api/profile", new { fullName = "Stale Update", version })).StatusCode);

        using var afterScope = factory.Services.CreateScope();
        var afterDb = afterScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(auditBefore + 1, await afterDb.CaseActivities.CountAsync(x => x.ActorId == auth.UserId && x.Type == "ProfileUpdated"));
        var stored = await afterDb.Users.AsNoTracking().SingleAsync(x => x.Id == auth.UserId);
        Assert.Equal("Updated", stored.FirstName);
        Assert.Equal("Profile", stored.LastName);
        Assert.Equal(email, stored.Email);
        Assert.True(stored.IsActive);
    }

    [Fact]
    public async Task ChangePassword_ValidatesInputRevokesAllRefreshTokensAndAuditsSafely()
    {
        var email = $"password-{Guid.NewGuid():N}@example.test";
        var auth = await Register(email, "Password Test");
        var secondSession = await Login(email);
        Authorize(auth.Token);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/profile/change-password", new { currentPassword = "Incorrect-Aa1!", newPassword = "Valid-New-Aa1!" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/profile/change-password", new { currentPassword = TestCredentials.Password, newPassword = "weak" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/profile/change-password", new { currentPassword = TestCredentials.Password, newPassword = TestCredentials.Password })).StatusCode);

        var newPassword = $"Changed-Aa1!{Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/profile/change-password", new { currentPassword = TestCredentials.Password, newPassword })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = auth.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = secondSession.RefreshToken })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestCredentials.Password })).StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.All(await db.RefreshTokens.Where(x => x.UserId == auth.UserId).ToListAsync(), token => Assert.NotNull(token.RevokedAtUtc));
        var audit = await db.CaseActivities.SingleAsync(x => x.ActorId == auth.UserId && x.Type == "PasswordChanged");
        Assert.DoesNotContain(TestCredentials.Password, audit.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(newPassword, audit.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("token", audit.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", audit.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = newPassword })).StatusCode);
    }

    private async Task<(string Token, string RefreshToken, Guid UserId)> Register(string email, string fullName)
    {
        var names = fullName.Split(' ', 2);
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = TestCredentials.Password, firstName = names[0], lastName = names[1] });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("accessToken").GetString()!, json.GetProperty("refreshToken").GetString()!, json.GetProperty("user").GetProperty("id").GetGuid());
    }

    private async Task<(string Token, string RefreshToken, Guid UserId)> Login(string email)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestCredentials.Password });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("accessToken").GetString()!, json.GetProperty("refreshToken").GetString()!, json.GetProperty("user").GetProperty("id").GetGuid());
    }

    private void Authorize(string token) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
