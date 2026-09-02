using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CivicFlow.Infrastructure.Identity;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CivicFlow.IntegrationTests;

public sealed class StaffOperationsTests : IClassFixture<CivicFlowFactory>
{
    private readonly HttpClient _client;
    private readonly CivicFlowFactory _factory;

    public StaffOperationsTests(CivicFlowFactory factory) { _factory = factory; _client = factory.CreateClient(); }

    [Fact]
    public async Task TwoOfficersWithSameDisplayName_AreDisambiguatedByEmailAndAssignedById()
    {
        var manager = await Login("manager@civicflow.local");
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var duplicateEmail = $"casey-{Guid.NewGuid():N}@example.test";
        var duplicate = new ApplicationUser { Id = Guid.NewGuid(), Email = duplicateEmail, UserName = duplicateEmail, EmailConfirmed = true, FirstName = "Casey", LastName = "Officer", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow };
        Assert.True((await users.CreateAsync(duplicate, TestCredentials.Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(duplicate, CivicFlowRoles.CaseOfficer)).Succeeded);
        Authorize(manager.Token);
        var candidates = await _client.GetFromJsonAsync<JsonElement>("/api/officers");
        var sameNames = candidates.EnumerateArray().Where(x => x.GetProperty("firstName").GetString() == "Casey" && x.GetProperty("lastName").GetString() == "Officer").ToList();
        Assert.True(sameNames.Count >= 2); Assert.Equal(sameNames.Count, sameNames.Select(x => x.GetProperty("id").GetGuid()).Distinct().Count()); Assert.All(sameNames, x => Assert.False(string.IsNullOrWhiteSpace(x.GetProperty("email").GetString())));
    }

    [Fact]
    public async Task DisabledOfficer_IsExcludedFromAssignmentCandidates()
    {
        var manager = await Login("manager@civicflow.local"); var admin = await Login("admin@civicflow.local"); var officer = await Login("officer@civicflow.local");
        Authorize(admin.Token); Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsJsonAsync($"/api/admin/users/{officer.UserId}/active", new { isActive = false })).StatusCode);
        try { Authorize(manager.Token); var candidates = await _client.GetFromJsonAsync<JsonElement>("/api/officers"); Assert.DoesNotContain(candidates.EnumerateArray(), x => x.GetProperty("id").GetGuid() == officer.UserId); }
        finally { Authorize(admin.Token); await _client.PutAsJsonAsync($"/api/admin/users/{officer.UserId}/active", new { isActive = true }); }
    }

    [Fact]
    public async Task Assignment_ToOfficerId_AppearsInThatOfficersMineQueue_AndSameAssignmentIsNoOp()
    {
        var resident = await Login("resident@civicflow.local"); var manager = await Login("manager@civicflow.local"); var email = $"mine-{Guid.NewGuid():N}@example.test";
        using (var setupScope = _factory.Services.CreateScope()) { var users = setupScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var user = new ApplicationUser { Id = Guid.NewGuid(), Email = email, UserName = email, EmailConfirmed = true, FirstName = "Mine", LastName = "Officer", IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow }; Assert.True((await users.CreateAsync(user, TestCredentials.Password)).Succeeded); Assert.True((await users.AddToRoleAsync(user, CivicFlowRoles.CaseOfficer)).Succeeded); }
        var officer = await Login(email); var caseId = await CreateCase(resident.Token, $"Mine assignment {Guid.NewGuid():N}");
        Authorize(manager.Token); Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync($"/api/cases/{caseId}/triage", new { priority = "High" })).StatusCode); var firstAssignment = await _client.PostAsJsonAsync($"/api/cases/{caseId}/assign", new { officerId = officer.UserId }); Assert.True(firstAssignment.StatusCode == HttpStatusCode.NoContent, await firstAssignment.Content.ReadAsStringAsync());
        using var beforeScope = _factory.Services.CreateScope(); var beforeDb = beforeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var beforeActivities = await beforeDb.CaseActivities.CountAsync(x => x.ServiceRequestId == caseId); var beforeNotices = await beforeDb.UserNotifications.CountAsync(x => x.ServiceRequestId == caseId && x.UserId == officer.UserId);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync($"/api/cases/{caseId}/assign", new { officerId = officer.UserId })).StatusCode);
        using var afterScope = _factory.Services.CreateScope(); var afterDb = afterScope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); Assert.Equal(beforeActivities, await afterDb.CaseActivities.CountAsync(x => x.ServiceRequestId == caseId)); Assert.Equal(beforeNotices, await afterDb.UserNotifications.CountAsync(x => x.ServiceRequestId == caseId && x.UserId == officer.UserId));
        Authorize(officer.Token); var mine = await _client.GetFromJsonAsync<JsonElement>("/api/cases?mine=true&pageSize=100"); Assert.Contains(mine.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == caseId);
    }

    [Fact]
    public async Task AssignmentCandidateDirectory_IsManagerOnly_AndResidentProjectionDoesNotExposeEmail()
    {
        var resident = await Login("resident@civicflow.local"); var officer = await Login("officer@civicflow.local"); var manager = await Login("manager@civicflow.local");
        Authorize(officer.Token); Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/officers")).StatusCode);
        Authorize(manager.Token); Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/officers")).StatusCode);
        var caseId = await CreateCase(resident.Token, $"Projection privacy {Guid.NewGuid():N}"); Authorize(resident.Token); var json = await _client.GetStringAsync($"/api/cases/{caseId}"); Assert.DoesNotContain("@civicflow.local", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StaffWorkflow_SupportsPagingTriageAssignmentAndResolutionAudit()
    {
        var resident = await Login("resident@civicflow.local");
        var manager = await Login("manager@civicflow.local");
        var officer = await Login("officer@civicflow.local");
        var uniqueTitle = $"Staff workflow {Guid.NewGuid():N}";
        var caseId = await CreateCase(resident.Token, uniqueTitle);

        Authorize(manager.Token);
        var firstDue = DateTimeOffset.UtcNow.AddHours(2);
        var resolutionDue = DateTimeOffset.UtcNow.AddHours(12);
        var triage = await _client.PostAsJsonAsync($"/api/cases/{caseId}/triage", new
        {
            priority = "Critical", firstResponseDueAtUtc = firstDue, resolutionDueAtUtc = resolutionDue
        });
        Assert.Equal(HttpStatusCode.NoContent, triage.StatusCode);
        var officers = await _client.GetFromJsonAsync<JsonElement>("/api/officers");
        var officerId = officers.EnumerateArray().Single(x => x.GetProperty("email").GetString() == "officer@civicflow.local").GetProperty("id").GetGuid();
        var assignment = await _client.PostAsJsonAsync($"/api/cases/{caseId}/assign", new { officerId });
        Assert.Equal(HttpStatusCode.NoContent, assignment.StatusCode);

        Authorize(officer.Token);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync($"/api/cases/{caseId}/status", new { status = "InProgress", note = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync($"/api/cases/{caseId}/status", new { status = "Resolved", note = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PostAsJsonAsync($"/api/cases/{caseId}/status", new { status = "Resolved", note = "Verified resolution summary." })).StatusCode);

        Authorize(manager.Token);
        var page = await _client.GetFromJsonAsync<JsonElement>($"/api/cases?priority=Critical&search={uniqueTitle}&page=1&pageSize=10&sortBy=priority&sortDirection=desc");
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 1);
        Assert.Contains(page.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == caseId);
        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/cases/{caseId}");
        var actions = detail.GetProperty("activities").EnumerateArray().Select(x => x.GetProperty("type").GetString()).ToList();
        Assert.DoesNotContain("PriorityChanged", actions);
        Assert.DoesNotContain("SlaChanged", actions);
        var audit = await _client.GetFromJsonAsync<JsonElement>("/api/admin/audit-logs?action=Changed&pageSize=100");
        Assert.Contains(audit.GetProperty("items").EnumerateArray(), x => x.GetProperty("action").GetString() == "PriorityChanged");
        Assert.Contains("Assigned", actions);
        Assert.Contains("Resolved", actions);
    }

    [Fact]
    public async Task Officer_CannotUpdateUnassignedCase()
    {
        var resident = await Login("resident@civicflow.local");
        var officer = await Login("officer@civicflow.local");
        var caseId = await CreateCase(resident.Token, "Unassigned ownership test");

        Authorize(officer.Token);
        var response = await _client.PostAsJsonAsync($"/api/cases/{caseId}/status", new { status = "InProgress", note = "" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_ProtectsSelfAndAuditsCategoryLifecycle()
    {
        var admin = await Login("admin@civicflow.local");
        Authorize(admin.Token);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"/api/admin/users/{admin.UserId}/active", new { isActive = false })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"/api/admin/users/{admin.UserId}/role", new { role = "Resident" })).StatusCode);

        var name = $"Integration category {Guid.NewGuid():N}";
        var create = await _client.PostAsJsonAsync("/api/admin/categories", new { name, description = "Integration test", firstResponseHours = 2, resolutionHours = 12 });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var categories = await _client.GetFromJsonAsync<JsonElement>("/api/admin/categories");
        var category = categories.EnumerateArray().Single(x => x.GetProperty("name").GetString() == name);
        var id = category.GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsJsonAsync($"/api/admin/categories/{id}", new { name, description = "Updated integration test", firstResponseHours = 4, resolutionHours = 24 })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await _client.PutAsJsonAsync($"/api/admin/categories/{id}/active", new { isActive = false })).StatusCode);
        var audit = await _client.GetFromJsonAsync<JsonElement>("/api/admin/audit-logs?action=Category&pageSize=100");
        Assert.True(audit.GetProperty("items").GetArrayLength() >= 3);
    }

    private async Task<(string Token, Guid UserId)> Login(string email)
    {
        _client.DefaultRequestHeaders.Authorization = null;
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = TestCredentials.Password });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("accessToken").GetString()!, json.GetProperty("user").GetProperty("id").GetGuid());
    }

    private async Task<Guid> CreateCase(string token, string title)
    {
        Authorize(token);
        var categories = await _client.GetFromJsonAsync<JsonElement>("/api/categories");
        var categoryId = categories.EnumerateArray().First().GetProperty("id").GetGuid();
        var response = await _client.PostAsJsonAsync("/api/cases", new { categoryId, title, description = "Integration test request.", address = "1 Test Street" });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }

    private void Authorize(string token) => _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
}
