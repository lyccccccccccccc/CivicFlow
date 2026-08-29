using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace CivicFlow.IntegrationTests;

public sealed class StaffOperationsTests : IClassFixture<CivicFlowFactory>
{
    private readonly HttpClient _client;

    public StaffOperationsTests(CivicFlowFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task StaffWorkflow_SupportsPagingTriageAssignmentAndResolutionAudit()
    {
        var resident = await Login("resident@civicflow.local");
        var manager = await Login("manager@civicflow.local");
        var officer = await Login("officer@civicflow.local");
        var caseId = await CreateCase(resident.Token, "Staff workflow integration test");

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
        var page = await _client.GetFromJsonAsync<JsonElement>("/api/cases?priority=Critical&page=1&pageSize=10&sortBy=priority&sortDirection=desc");
        Assert.True(page.GetProperty("totalCount").GetInt32() >= 1);
        Assert.Contains(page.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == caseId);
        var detail = await _client.GetFromJsonAsync<JsonElement>($"/api/cases/{caseId}");
        var actions = detail.GetProperty("activities").EnumerateArray().Select(x => x.GetProperty("type").GetString()).ToList();
        Assert.Contains("PriorityChanged", actions);
        Assert.Contains("SlaChanged", actions);
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
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password = "REDACTED_HISTORICAL_DEVELOPMENT_SECRET" });
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
