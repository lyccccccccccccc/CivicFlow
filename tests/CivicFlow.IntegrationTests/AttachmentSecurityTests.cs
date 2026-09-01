using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using SkiaSharp;

namespace CivicFlow.IntegrationTests;

public sealed class AttachmentSecurityTests(CivicFlowFactory factory) : IClassFixture<CivicFlowFactory>
{
    private readonly HttpClient client = factory.CreateClient();
    private static readonly byte[] Png = CreatePng();

    [Fact]
    public async Task PublicUpload_IsIdempotent_DownloadsSafely_AndSoftDeletes()
    {
        var token = await Login("resident@civicflow.local"); Authorize(token); var id = await CreateCase("Attachment security case");
        var first = await Upload(id, Png, "../unsafe\u202Ename.png", "image/png", "Public", "same-key");
        Assert.True(first.StatusCode == HttpStatusCode.Created, await first.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.NoContent, (await Upload(id, Png, "retry.png", "image/png", "Public", "same-key")).StatusCode);
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/cases/{id}/attachments"); Assert.Single(list.EnumerateArray());
        var attachment = list.EnumerateArray().Single(); var attachmentId = attachment.GetProperty("id").GetGuid();
        Assert.DoesNotContain("storage", attachment.ToString(), StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("sha256", attachment.ToString(), StringComparison.OrdinalIgnoreCase);
        var download = await client.GetAsync($"/api/cases/{id}/attachments/{attachmentId}/content"); download.EnsureSuccessStatusCode();
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single()); Assert.NotNull(download.Headers.ETag); Assert.Equal(Png.Length, download.Content.Headers.ContentLength);
        var deleted = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/cases/{id}/attachments/{attachmentId}") { Content = JsonContent.Create(new { reason = "Uploaded the wrong photograph." }) });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/cases/{id}/attachments/{attachmentId}/content")).StatusCode);
    }

    [Fact]
    public async Task InternalAttachment_IsNeverProjectedOrDownloadedByResident_AndCrossResidentIs404()
    {
        var resident = await Login("resident@civicflow.local"); Authorize(resident); var id = await CreateCase("Internal attachment isolation");
        var manager = await Login("manager@civicflow.local"); Authorize(manager);
        var upload = await Upload(id, Png, "internal.png", "image/png", "Internal", Guid.NewGuid().ToString());
        Assert.True(upload.IsSuccessStatusCode, await upload.Content.ReadAsStringAsync());
        var internalId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Authorize(resident); Assert.Empty((await client.GetFromJsonAsync<JsonElement>($"/api/cases/{id}/attachments")).EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/cases/{id}/attachments/{internalId}/content")).StatusCode);

        var email = $"other-{Guid.NewGuid():N}@example.local";
        var registration = await client.PostAsJsonAsync("/api/auth/register", new { email, password = "REDACTED_HISTORICAL_DEVELOPMENT_SECRET", firstName = "Other", lastName = "Resident" }); registration.EnsureSuccessStatusCode();
        var other = (await registration.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!; Authorize(other);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/cases/{id}/attachments")).StatusCode);
    }

    [Fact]
    public async Task InvalidSignature_IsRejectedWithoutAttachmentRecord()
    {
        Authorize(await Login("resident@civicflow.local")); var id = await CreateCase("Invalid signature attachment");
        var response = await Upload(id, Encoding.UTF8.GetBytes("not a real pdf"), "evidence.pdf", "application/pdf", "Public", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>($"/api/cases/{id}/attachments")).EnumerateArray());
    }

    private async Task<HttpResponseMessage> Upload(Guid id, byte[] bytes, string name, string type, string visibility, string key)
    {
        using var form = new MultipartFormDataContent(); var file = new ByteArrayContent(bytes); file.Headers.ContentType = MediaTypeHeaderValue.Parse(type);
        form.Add(file, "file", name); form.Add(new StringContent(visibility), "visibility");
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/cases/{id}/attachments") { Content = form }; request.Headers.Add("Idempotency-Key", key);
        return await client.SendAsync(request);
    }
    private async Task<Guid> CreateCase(string title)
    {
        var categories = await client.GetFromJsonAsync<JsonElement>("/api/categories"); var categoryId = categories.EnumerateArray().First().GetProperty("id").GetGuid();
        var response = await client.PostAsJsonAsync("/api/cases", new { categoryId, title, description = "A sufficiently detailed attachment integration request.", address = "10 Test Street" }); response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
    private async Task<string> Login(string email) { var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "REDACTED_HISTORICAL_DEVELOPMENT_SECRET" }); response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!; }
    private void Authorize(string token) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    private static byte[] CreatePng() { using var bitmap = new SKBitmap(1, 1); bitmap.SetPixel(0, 0, SKColors.Blue); using var image = SKImage.FromBitmap(bitmap); using var data = image.Encode(SKEncodedImageFormat.Png, 100); return data.ToArray(); }
}
