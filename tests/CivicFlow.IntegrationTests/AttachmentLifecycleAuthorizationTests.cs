using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CivicFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp;

namespace CivicFlow.IntegrationTests;

public sealed class AttachmentLifecycleAuthorizationTests(CivicFlowFactory factory) : IClassFixture<CivicFlowFactory>
{
    private readonly HttpClient client = factory.CreateClient();
    private static readonly byte[] Png = CreatePng();

    [Fact]
    public async Task ResolvedCase_ResidentAndOfficerAttachmentWritesReturn404WithoutSideEffects()
    {
        var scenario = await CreateResolvedScenario();
        var residentBefore = await Snapshot(scenario.CaseId);
        Authorize(scenario.ResidentToken);
        Assert.All((await Attachments(scenario.CaseId)).EnumerateArray(), attachment => Assert.False(attachment.GetProperty("canDelete").GetBoolean()));
        Assert.Equal(HttpStatusCode.NotFound, (await Upload(scenario.CaseId, "resident-blocked.png", "Public")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(scenario.CaseId, scenario.ResidentAttachmentId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(scenario.CaseId, scenario.OfficerAttachmentId)).StatusCode);
        AssertStateEqual(residentBefore, await Snapshot(scenario.CaseId));

        var officerBefore = await Snapshot(scenario.CaseId);
        Authorize(scenario.OfficerToken);
        Assert.Equal(HttpStatusCode.NotFound, (await Upload(scenario.CaseId, "officer-blocked.png", "Internal")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(scenario.CaseId, scenario.OfficerAttachmentId)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(scenario.CaseId, scenario.ResidentAttachmentId)).StatusCode);
        AssertStateEqual(officerBefore, await Snapshot(scenario.CaseId));

        Authorize(scenario.ManagerToken);
        Assert.All((await Attachments(scenario.CaseId)).EnumerateArray(), attachment => Assert.False(attachment.GetProperty("canDelete").GetBoolean()));
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(scenario.CaseId, scenario.ManagerAttachmentId)).StatusCode);
    }

    [Fact]
    public async Task ReopenedCase_EnforcesUploaderOwnershipAndPreservesOriginalRecords()
    {
        var scenario = await CreateResolvedScenario(); var before = await Snapshot(scenario.CaseId);
        Authorize(scenario.ResidentToken);
        Assert.Equal(HttpStatusCode.NoContent, (await ChangeStatus(scenario.CaseId, "Reopened", "The completed work still requires further investigation.")).StatusCode);
        var residentUpload = await Upload(scenario.CaseId, "resident-reopened.png", "Public"); Assert.Equal(HttpStatusCode.Created, residentUpload.StatusCode);
        var residentNewId = (await residentUpload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(scenario.CaseId, scenario.OfficerAttachmentId)).StatusCode);

        Authorize(scenario.ManagerToken);
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(scenario.CaseId, residentNewId)).StatusCode);

        Authorize(scenario.ResidentToken);
        var residentList = await Attachments(scenario.CaseId);
        Assert.True(residentList.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == residentNewId).GetProperty("canDelete").GetBoolean());
        Assert.True(residentList.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == scenario.ResidentAttachmentId).GetProperty("canDelete").GetBoolean());

        Authorize(scenario.OfficerToken);
        var officerInternal = await Upload(scenario.CaseId, "officer-internal.png", "Internal"); Assert.Equal(HttpStatusCode.Created, officerInternal.StatusCode);
        var officerInternalId = (await officerInternal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var officerList = await Attachments(scenario.CaseId);
        Assert.True(officerList.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == officerInternalId).GetProperty("canDelete").GetBoolean());
        Assert.False(officerList.EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == residentNewId).GetProperty("canDelete").GetBoolean());
        Assert.Equal(HttpStatusCode.NotFound, (await Delete(scenario.CaseId, residentNewId)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await Delete(scenario.CaseId, officerInternalId)).StatusCode);

        Authorize(scenario.ResidentToken);
        Assert.Equal(HttpStatusCode.NoContent, (await Delete(scenario.CaseId, residentNewId)).StatusCode);
        var after = await Snapshot(scenario.CaseId);
        Assert.Equal(before.FirstResponseDue, after.FirstResponseDue); Assert.Equal(before.ResolutionDue, after.ResolutionDue);
        Assert.Contains("Resolved", after.ActivityTypes); Assert.Contains("Reopened", after.ActivityTypes);
        Assert.Contains(scenario.ResidentAttachmentId, after.AttachmentIds); Assert.Contains(scenario.OfficerAttachmentId, after.AttachmentIds);
    }

    [Fact]
    public async Task ConcurrentReopen_OnSqlServer_ProducesOneBusinessEventAndOneOfficerNotification()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var providerDb = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (!providerDb.Database.IsSqlServer()) return;
        }
        var scenario = await CreateResolvedScenario(); var before = await Snapshot(scenario.CaseId); Authorize(scenario.ResidentToken);
        var first = ChangeStatus(scenario.CaseId, "Reopened", "Concurrent reopen reason requiring more service work.");
        var second = ChangeStatus(scenario.CaseId, "Reopened", "Concurrent reopen reason requiring more service work.");
        var responses = await Task.WhenAll(first, second);
        Assert.All(responses, x => Assert.Contains(x.StatusCode, new[] { HttpStatusCode.NoContent, HttpStatusCode.Conflict }));
        var after = await Snapshot(scenario.CaseId);
        Assert.Equal(1, after.ActivityTypes.Count(x => x == "Reopened") - before.ActivityTypes.Count(x => x == "Reopened"));
        Assert.Equal(1, after.NotificationCount - before.NotificationCount);
    }

    private async Task<Scenario> CreateResolvedScenario()
    {
        var resident = await Login("resident@civicflow.local"); Authorize(resident);
        var category = (await client.GetFromJsonAsync<JsonElement>("/api/categories")).EnumerateArray().First().GetProperty("id").GetGuid();
        var created = await client.PostAsJsonAsync("/api/cases", new { categoryId = category, title = $"Lifecycle attachment {Guid.NewGuid():N}", description = "Dedicated attachment lifecycle authorization integration test case.", address = "200 Lifecycle Test Street" }); created.EnsureSuccessStatusCode();
        var caseId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var residentUpload = await Upload(caseId, "resident-original.png", "Public"); residentUpload.EnsureSuccessStatusCode();
        var residentAttachmentId = (await residentUpload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var manager = await Login("manager@civicflow.local"); Authorize(manager);
        var managerUpload = await Upload(caseId, "manager-original.png", "Internal"); managerUpload.EnsureSuccessStatusCode();
        var managerAttachmentId = (await managerUpload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/cases/{caseId}/triage", new { priority = "Medium" })).EnsureSuccessStatusCode();
        var officers = await client.GetFromJsonAsync<JsonElement>("/api/officers"); var officerId = officers.EnumerateArray().First(x => x.GetProperty("email").GetString() == "officer@civicflow.local").GetProperty("id").GetGuid();
        (await client.PostAsJsonAsync($"/api/cases/{caseId}/assign", new { officerId })).EnsureSuccessStatusCode();

        var officer = await Login("officer@civicflow.local"); Authorize(officer);
        var officerUpload = await Upload(caseId, "officer-original.png", "Internal"); officerUpload.EnsureSuccessStatusCode();
        var officerAttachmentId = (await officerUpload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        (await ChangeStatus(caseId, "InProgress")).EnsureSuccessStatusCode();
        (await ChangeStatus(caseId, "Resolved", "Lifecycle test resolution summary retained for audit.")).EnsureSuccessStatusCode();
        return new(caseId, resident, officer, manager, residentAttachmentId, officerAttachmentId, managerAttachmentId);
    }

    private async Task<StateSnapshot> Snapshot(Guid caseId)
    {
        await using var scope = factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.ServiceRequests.AsNoTracking().SingleAsync(x => x.Id == caseId);
        var attachments = await db.CaseAttachments.AsNoTracking().Where(x => x.ServiceRequestId == caseId).OrderBy(x => x.Id).ToListAsync();
        var activities = await db.CaseActivities.AsNoTracking().Where(x => x.ServiceRequestId == caseId).OrderBy(x => x.CreatedAtUtc).Select(x => x.Type).ToListAsync();
        var notifications = await db.UserNotifications.AsNoTracking().CountAsync(x => x.ServiceRequestId == caseId);
        var storage = factory.Services.GetRequiredService<TestFileStorage>();
        return new(attachments.Select(x => x.Id).ToArray(), attachments.Select(x => $"{x.Id}|{x.IsDeleted}|{x.DeletedAtUtc:O}|{x.DeletedByUserId}|{x.DeletionReason}").ToArray(), activities.ToArray(), notifications, storage.Keys.ToArray(), item.FirstResponseDueAtUtc, item.ResolutionDueAtUtc);
    }

    private static void AssertStateEqual(StateSnapshot expected, StateSnapshot actual)
    {
        Assert.Equal(expected.AttachmentIds, actual.AttachmentIds); Assert.Equal(expected.AttachmentMetadata, actual.AttachmentMetadata);
        Assert.Equal(expected.ActivityTypes, actual.ActivityTypes); Assert.Equal(expected.NotificationCount, actual.NotificationCount);
        Assert.Equal(expected.BlobKeys, actual.BlobKeys); Assert.Equal(expected.FirstResponseDue, actual.FirstResponseDue); Assert.Equal(expected.ResolutionDue, actual.ResolutionDue);
    }

    private async Task<HttpResponseMessage> Upload(Guid caseId, string name, string visibility)
    {
        using var form = new MultipartFormDataContent(); var file = new ByteArrayContent(Png); file.Headers.ContentType = MediaTypeHeaderValue.Parse("image/png");
        form.Add(file, "file", name); form.Add(new StringContent(visibility), "visibility");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/cases/{caseId}/attachments") { Content = form }; request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        return await client.SendAsync(request);
    }
    private Task<HttpResponseMessage> Delete(Guid caseId, Guid attachmentId) => client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/cases/{caseId}/attachments/{attachmentId}") { Content = JsonContent.Create(new { reason = "Lifecycle security verification deletion." }) });
    private Task<HttpResponseMessage> ChangeStatus(Guid caseId, string status, string? note = null) => client.PostAsJsonAsync($"/api/cases/{caseId}/status", new { status, note });
    private Task<JsonElement> Attachments(Guid caseId) => client.GetFromJsonAsync<JsonElement>($"/api/cases/{caseId}/attachments");
    private async Task<string> Login(string email) { var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = TestCredentials.Password }); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!; }
    private void Authorize(string token) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    private static byte[] CreatePng() { using var bitmap = new SKBitmap(1, 1); bitmap.SetPixel(0, 0, SKColors.Green); using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray(); }

    private sealed record Scenario(Guid CaseId, string ResidentToken, string OfficerToken, string ManagerToken, Guid ResidentAttachmentId, Guid OfficerAttachmentId, Guid ManagerAttachmentId);
    private sealed record StateSnapshot(Guid[] AttachmentIds, string[] AttachmentMetadata, string[] ActivityTypes, int NotificationCount, string[] BlobKeys, DateTimeOffset? FirstResponseDue, DateTimeOffset? ResolutionDue);
}
