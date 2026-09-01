using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CivicFlow.Api.Common;
using CivicFlow.Domain.Entities;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CivicFlow.IntegrationTests;

public sealed class Phase2HardeningTests(CivicFlowFactory factory) : IClassFixture<CivicFlowFactory>
{
    private readonly HttpClient client = factory.CreateClient();

    [Fact]
    public void SlaStates_TrackFirstResponseAndUseTheMostSevereIncompleteTarget()
    {
        var submitted = DateTimeOffset.UtcNow.AddHours(-10);
        var item = ServiceRequest.Create("TEST-SLA-STATE", Guid.NewGuid(), Guid.NewGuid(), "Valid SLA title", "A sufficiently detailed service request.", "Test location", submitted);
        item.ApplyInitialSla(4, 48);
        Assert.Equal("Overdue", SlaCalculator.FirstResponseState(item, DateTimeOffset.UtcNow));
        Assert.Equal("OnTrack", SlaCalculator.ResolutionState(item, DateTimeOffset.UtcNow));
        Assert.Equal("Overdue", SlaCalculator.OverallState(item, DateTimeOffset.UtcNow));

        item.CompleteFirstResponse(submitted.AddHours(3));
        Assert.Equal("Complete", SlaCalculator.FirstResponseState(item, DateTimeOffset.UtcNow));
        Assert.Equal("OnTrack", SlaCalculator.OverallState(item, DateTimeOffset.UtcNow));

        var late = ServiceRequest.Create("TEST-SLA-LATE", Guid.NewGuid(), Guid.NewGuid(), "Valid SLA title", "A sufficiently detailed service request.", "Test location", submitted);
        late.ApplyInitialSla(4, 48); late.CompleteFirstResponse(submitted.AddHours(5));
        Assert.Equal("Breached", SlaCalculator.FirstResponseState(late, DateTimeOffset.UtcNow));
        Assert.Equal("OnTrack", SlaCalculator.OverallState(late, DateTimeOffset.UtcNow));
        late.UpdateTriage(CivicFlow.Domain.Enums.CasePriority.Medium, submitted.AddHours(6), submitted.AddHours(48), submitted.AddHours(6));
        Assert.Equal("Breached", SlaCalculator.FirstResponseState(late, DateTimeOffset.UtcNow));
        Assert.Equal(late.ResolutionDueAtUtc, SlaCalculator.NextDue(late));
    }

    [Fact]
    public async Task CreateCase_RejectsTrimmedLengthAndInactiveCategory_WithFieldErrors()
    {
        var resident = await Login("resident"); Auth(resident.Token);
        var active = (await Json("/api/categories")).EnumerateArray().First().GetProperty("id").GetGuid();
        var invalidText = new[] {
            new { categoryId = active, title = "123", description = "A sufficiently detailed description.", address = "Valid location" },
            new { categoryId = active, title = "     ", description = "A sufficiently detailed description.", address = "Valid location" },
            new { categoryId = active, title = "Valid title", description = "123", address = "Valid location" },
            new { categoryId = active, title = "Valid title", description = new string('x', 2001), address = "Valid location" },
            new { categoryId = active, title = new string('x', 151), description = "A sufficiently detailed description.", address = "Valid location" },
            new { categoryId = active, title = "Valid title", description = "A sufficiently detailed description.", address = "123" },
            new { categoryId = active, title = "Valid title", description = "A sufficiently detailed description.", address = new string('x', 301) }
        };
        foreach (var payload in invalidText)
        {
            var response = await client.PostAsJsonAsync("/api/cases", payload);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("errors", await response.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        }
        foreach (var categoryId in new[] { Guid.NewGuid(), await AddDisabledCategory() })
        {
            var response = await client.PostAsJsonAsync("/api/cases", new { categoryId, title = "Valid title", description = "A sufficiently detailed description.", address = "Valid location" });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("CategoryId", await response.Content.ReadAsStringAsync());
        }
        var valid = await client.PostAsJsonAsync("/api/cases", new { categoryId = active, title = "  Valid new request  ", description = "  A sufficiently detailed service request description.  ", address = "  10 Valid Street  " });
        valid.EnsureSuccessStatusCode(); var id = (await valid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var detail = (await Json($"/api/cases/{id}")).GetProperty("case");
        Assert.Equal("Valid new request", detail.GetProperty("title").GetString());
        Assert.NotEqual(JsonValueKind.Null, detail.GetProperty("firstResponseDueAtUtc").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, detail.GetProperty("resolutionDueAtUtc").ValueKind);
    }

    [Fact]
    public async Task OnlyPublicStaffMessage_CompletesFirstResponse()
    {
        var resident = await Login("resident"); var officer = await Login("officer"); var manager = await Login("manager");
        Auth(resident.Token); var category = (await Json("/api/categories")).EnumerateArray().First().GetProperty("id").GetGuid();
        var created = await client.PostAsJsonAsync("/api/cases", new { categoryId = category, title = "First response contract", description = "Testing which activity completes first response.", address = "10 Test Street" });
        created.EnsureSuccessStatusCode(); var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Auth(manager.Token); await Post($"/api/cases/{id}/triage", new { priority = "Medium" }); await Post($"/api/cases/{id}/assign", new { officerId = officer.Id });
        Auth(officer.Token); await Post($"/api/cases/{id}/status", new { status = "InProgress" }); await Post($"/api/cases/{id}/comments", new { message = "Private investigation note", @internal = true });
        Assert.Equal(JsonValueKind.Null, (await Json($"/api/cases/{id}")).GetProperty("case").GetProperty("firstResponseCompletedAtUtc").ValueKind);
        await Post($"/api/cases/{id}/comments", new { message = "Public service team response", @internal = false });
        var detail = (await Json($"/api/cases/{id}")).GetProperty("case");
        Assert.NotEqual(JsonValueKind.Null, detail.GetProperty("firstResponseCompletedAtUtc").ValueKind);
        Assert.Equal("Complete", detail.GetProperty("firstResponseSlaState").GetString());
    }

    [Fact]
    public async Task DashboardAndCsv_UseOverallSlaIncludingOverdueFirstResponse()
    {
        var resident = await Login("resident"); var manager = await Login("manager"); var label = $"SLA-consistency-{Guid.NewGuid():N}";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>(); var category = await db.ServiceCategories.FirstAsync(); var submitted = DateTimeOffset.UtcNow.AddHours(-10);
            var item = ServiceRequest.Create($"TEST-{Guid.NewGuid():N}"[..22], resident.Id, category.Id, label, "First response is overdue while resolution remains on track.", "Test location", submitted);
            item.ApplyInitialSla(4, 48); db.ServiceRequests.Add(item); await db.SaveChangesAsync();
        }
        Auth(manager.Token); var dashboard = await Json($"/api/dashboard?search={label}");
        Assert.Equal(1, dashboard.GetProperty("overdue").GetInt32()); Assert.Equal(0, dashboard.GetProperty("atRisk").GetInt32());
        var csv = await client.GetStringAsync($"/api/reports/cases.csv?search={label}");
        Assert.Contains(",OnTrack,Overdue,0", csv);
    }

    [Theory]
    [InlineData("InternalNote", true)]
    [InlineData("PriorityChanged", true)]
    [InlineData("SlaChanged", true)]
    [InlineData("Assigned", true)]
    [InlineData("UserRoleChanged", false)]
    [InlineData("CategoryUpdated", false)]
    [InlineData("UnknownFutureAudit", false)]
    public void BusinessProjection_FailsClosed_EvenForLegacyPublicAudits(string type, bool resident)
    {
        var entry = new CaseActivity(Guid.NewGuid(), Guid.NewGuid(), type, "Confidential technical detail", true, DateTimeOffset.UtcNow);
        Assert.Empty(ActivityFeed.Project([entry], resident, Guid.NewGuid()));
    }

    [Fact]
    public void LegacyWorkflow_IsNaturalLanguageAndDeduplicated()
    {
        var now = DateTimeOffset.UtcNow;
        var entries = new[] {
            new CaseActivity(Guid.NewGuid(), Guid.NewGuid(), "StatusChanged", "Status changed to InProgress.", true, now),
            new CaseActivity(Guid.NewGuid(), Guid.NewGuid(), "StatusChanged", "Status changed to InProgress.", true, now.AddSeconds(1)),
            new CaseActivity(Guid.NewGuid(), Guid.NewGuid(), "Resolved", "Status changed to Resolved. Fixed the damaged path.", true, now.AddSeconds(2))
        };
        var feed = ActivityFeed.Project(entries, true, Guid.NewGuid());
        Assert.Equal(2, feed.Count);
        Assert.Contains(feed, x => x.Message == "Fixed the damaged path.");
        Assert.DoesNotContain(feed, x => x.Message.Contains("InProgress"));
    }

    [Fact]
    public void ResidentProjection_CollapsesRapidToggleBurstAndExplainsLegacyReply()
    {
        var now = DateTimeOffset.UtcNow; var resident = Guid.NewGuid(); var officer = Guid.NewGuid(); var id = Guid.NewGuid();
        var source = new[] {
            new CaseActivity(id, officer, "StatusChanged", "Status changed to InProgress.", true, now),
            new CaseActivity(id, officer, "StatusChanged", "Status changed to WaitingForResident.", true, now.AddSeconds(3)),
            new CaseActivity(id, officer, "StatusChanged", "Status changed to InProgress.", true, now.AddSeconds(6)),
            new CaseActivity(id, officer, "StatusChanged", "Status changed to WaitingForResident.", true, now.AddSeconds(9)),
            new CaseActivity(id, resident, "Comment", "Confirmed the location", true, now.AddMinutes(10))
        };
        var feed = ActivityFeed.Project(source, true, resident);
        Assert.Equal(3, feed.Count);
        Assert.Single(feed, x => x.Type == "WaitingForResident");
        Assert.Contains(feed, x => x.Message == "Resident replied. Work has resumed.");
    }

    [Fact]
    public async Task Workflow_EnforcesVisibilitySlaNotificationAndReopenContracts()
    {
        var resident = await Login("resident"); var officer = await Login("officer");
        var manager = await Login("manager"); var admin = await Login("admin");
        Auth(resident.Token);
        var category = (await Json("/api/categories")).EnumerateArray().First();
        var name = $"Phase2 hardening {Guid.NewGuid():N}";
        var created = await client.PostAsJsonAsync("/api/cases", new { categoryId = category.GetProperty("id").GetGuid(), title = name, description = "Non-destructive regression request", address = "1 Test Street" });
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var path = $"/api/cases/{id}";
        var initial = (await Json(path)).GetProperty("case");
        var submitted = initial.GetProperty("submittedAtUtc").GetDateTimeOffset();
        var due = initial.GetProperty("resolutionDueAtUtc").GetDateTimeOffset();
        Assert.Equal(submitted.AddHours(category.GetProperty("resolutionHours").GetInt32()), due);
        Assert.Equal("Submitted", initial.GetProperty("status").GetString());
        Auth(manager.Token);
        await Post(path + "/triage", new { priority = "Critical" });
        Assert.Equal(due, (await Json(path)).GetProperty("case").GetProperty("resolutionDueAtUtc").GetDateTimeOffset());
        await Post(path + "/triage", new { priority = "High", firstResponseDueAtUtc = submitted.AddHours(1), resolutionDueAtUtc = submitted.AddHours(2) });
        await Post(path + "/triage", new { priority = "Critical" }); // Recalculate must restore original baseline, not now.
        Assert.Equal(due, (await Json(path)).GetProperty("case").GetProperty("resolutionDueAtUtc").GetDateTimeOffset());
        await Post(path + "/assign", new { officerId = officer.Id });
        await Post(path + "/assign", new { officerId = officer.Id }); // no-op
        Auth(officer.Token);
        Assert.Single(await Notifications(id));
        await Post(path + "/status", new { status = "InProgress" });
        await Post(path + "/comments", new { message = "Private officer investigation", @internal = true });
        await Post(path + "/comments", new { message = "Please confirm the location", @internal = false });
        await Post(path + "/status", new { status = "WaitingForResident" });
        await Post(path + "/status", new { status = "WaitingForResident" }); // no duplicate activity or notice
        var officerDetail = await Json(path);
        Assert.Contains(officerDetail.GetProperty("activities").EnumerateArray(), x => x.GetProperty("type").GetString() == "InternalNote");
        Assert.DoesNotContain(officerDetail.GetProperty("activities").EnumerateArray(), x => x.GetProperty("type").GetString() is "SlaChanged" or "PriorityChanged");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/audit-logs")).StatusCode);
        Auth(resident.Token);
        var detail = (await Json(path)).ToString();
        foreach (var forbidden in new[] { "Private officer investigation", "InternalNote", "PriorityChanged", "SlaChanged", "Reassigned" }) Assert.DoesNotContain(forbidden, detail);
        Assert.Equal(2, (await Notifications(id)).Count);
        var notice = (await Notifications(id))[0].GetProperty("id").GetGuid();
        await Post($"/api/notifications/{notice}/read", new { });
        await Post($"/api/notifications/{notice}/read", new { });
        Assert.NotEqual(JsonValueKind.Null, (await Notifications(id)).Single(x => x.GetProperty("id").GetGuid() == notice).GetProperty("readAtUtc").ValueKind);
        client.DefaultRequestHeaders.Add("Idempotency-Key", Guid.NewGuid().ToString());
        await Post(path + "/comments", new { message = "The address is correct, please continue." });
        await Post(path + "/comments", new { message = "The address is correct, please continue." });
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        var resumed = await Json(path);
        Assert.Equal("InProgress", resumed.GetProperty("case").GetProperty("status").GetString());
        Assert.Contains(resumed.GetProperty("activities").EnumerateArray(), x => x.GetProperty("message").GetString() == "Resident replied. Work has resumed.");
        Assert.Equal(2, (await Notifications(id)).Count); // No self / resumed notification.
        Auth(officer.Token);
        Assert.Equal(2, (await Notifications(id)).Count); // assignment + exactly one resident reply
        await Post(path + "/status", new { status = "Resolved", note = "short" }, HttpStatusCode.BadRequest);
        await Post(path + "/status", new { status = "Resolved", note = "The damaged footpath has been repaired safely." });
        Auth(resident.Token);
        Assert.Contains("The damaged footpath has been repaired safely.", (await Json(path)).ToString());
        Assert.Equal("Complete", (await Json(path)).GetProperty("case").GetProperty("slaState").GetString());
        await Post(path + "/status", new { status = "Reopened", note = "" }, HttpStatusCode.BadRequest);
        await Post(path + "/status", new { status = "Reopened", note = "The repair remains incomplete at the northern end." });
        Assert.Equal(due, (await Json(path)).GetProperty("case").GetProperty("resolutionDueAtUtc").GetDateTimeOffset());
        Assert.Equal(3, (await Notifications(id)).Count); // public message, waiting, resolved; not own reopen
        Auth(officer.Token);
        Assert.Equal(3, (await Notifications(id)).Count);
        foreach (var account in new[] { manager, admin })
        {
            Auth(account.Token);
            var audit = await Json($"/api/admin/audit-logs?case={Uri.EscapeDataString(name)}&pageSize=100");
            var actions = audit.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("action").GetString()).ToList();
            Assert.Contains("PriorityChanged", actions); Assert.Contains("SlaChanged", actions); Assert.Contains("InternalNote", actions);
            Assert.Equal(1, actions.Count(x => x == "WaitingForResident"));
            Assert.Equal(1, actions.Count(x => x == "Assigned"));
        }
        Auth(manager.Token);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/users")).StatusCode);
    }

    [Fact]
    public async Task ActiveWorkload_DashboardAndCsvExcludeAllTerminalStates()
    {
        var resident = await Login("resident"); var officer = await Login("officer"); var manager = await Login("manager");
        var label = $"Workload-{Guid.NewGuid():N}";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var category = await db.ServiceCategories.FirstAsync();
            for (var i = 0; i < 4; i++)
            {
                var now = DateTimeOffset.UtcNow;
                var item = ServiceRequest.Create($"TEST-{Guid.NewGuid():N}"[..22], resident.Id, category.Id, label, "Workload regression", "Test street", now);
                item.Triage(CivicFlow.Domain.Enums.CasePriority.Medium, now.AddHours(1), now.AddHours(48), now);
                if (i == 3) item.Reject(now);
                else { item.Assign(officer.Id, now); item.StartProgress(now); if (i > 0) item.Resolve(now); if (i == 2) item.Close(now); }
                db.ServiceRequests.Add(item);
                // Include a historical rejected case retaining an officer assignment.
                if (i == 3) db.Entry(item).Property(x => x.AssignedOfficerId).CurrentValue = officer.Id;
            }
            await db.SaveChangesAsync();
        }
        Auth(manager.Token);
        var dashboard = await Json($"/api/dashboard?search={label}");
        Assert.Equal(1, dashboard.GetProperty("activeWorkload").GetInt32());
        Assert.Equal(1, dashboard.GetProperty("officerWorkload")[0].GetProperty("count").GetInt32());
        var csv = await client.GetStringAsync($"/api/reports/cases.csv?search={label}");
        Assert.Equal(1, csv.Split('\n').Count(x => x.TrimEnd().EndsWith(",1")));
    }

    [Fact]
    public async Task AuditRecords_CannotBeDeletedThroughPersistence()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entry = new CaseActivity(Guid.Empty, Guid.Empty, "HardeningVerification", "Append-only audit verification", false, DateTimeOffset.UtcNow);
        db.CaseActivities.Add(entry); await db.SaveChangesAsync();
        db.CaseActivities.Remove(entry);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task SqlServer_UniqueNotificationKeyRejectsDuplicates()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await using var transaction = db.Database.IsSqlServer() ? await db.Database.BeginTransactionAsync() : null;
        var user = await db.Users.FirstAsync(); var key = Guid.NewGuid().ToString();
        db.UserNotifications.Add(new(user.Id, null, "Constraint test", "Unique key regression", DateTimeOffset.UtcNow, key));
        await db.SaveChangesAsync();
        if (!db.Database.IsSqlServer())
        {
            Assert.Contains(db.Model.FindEntityType(typeof(UserNotification))!.GetIndexes(), x => x.IsUnique && x.Properties.Any(p => p.Name == "EventKey"));
            return;
        }
        db.UserNotifications.Add(new(user.Id, null, "Constraint test", "Duplicate must fail", DateTimeOffset.UtcNow, key));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    private async Task<(string Token, Guid Id)> Login(string name)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = name + "@civicflow.local", password = TestCredentials.Password });
        response.EnsureSuccessStatusCode(); var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (json.GetProperty("accessToken").GetString()!, json.GetProperty("user").GetProperty("id").GetGuid());
    }
    private async Task<Guid> AddDisabledCategory()
    {
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var category = new ServiceCategory($"Disabled test {Guid.NewGuid():N}", "Validation regression", 4, 24, DateTimeOffset.UtcNow);
        category.SetActive(false, DateTimeOffset.UtcNow); db.ServiceCategories.Add(category); await db.SaveChangesAsync(); return category.Id;
    }
    private void Auth(string token) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    private async Task<JsonElement> Json(string path) => await client.GetFromJsonAsync<JsonElement>(path);
    private async Task<List<JsonElement>> Notifications(Guid id) => (await Json("/api/notifications")).EnumerateArray().Where(x => x.GetProperty("serviceRequestId").ValueKind == JsonValueKind.String && x.GetProperty("serviceRequestId").GetGuid() == id).ToList();
    private async Task Post(string path, object body, HttpStatusCode expected = HttpStatusCode.NoContent)
    {
        var response = await client.PostAsJsonAsync(path, body);
        Assert.True(response.StatusCode == expected, $"{path}: expected {expected}, got {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
    }
}
