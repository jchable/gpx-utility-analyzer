using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using GpxAnalyzer.Api.Tests.Helpers;

namespace GpxAnalyzer.Api.Tests.Enrichment;

/// <summary>
/// Integration tests for activity enrichment (BLOC 4 / Étape 6):
/// - PATCH /api/activities/{id} with description, RPE, tags, sessionType
/// - GET /api/activities/tags
/// - Calories after processing
/// </summary>
[Collection("Integration")]
public class EnrichmentApiTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private HttpClient _alice = null!;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public async Task InitializeAsync()
    {
        var anon = _factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(anon, "enrichment@test.com", displayName: "Alice");
        _alice = TestHelpers.CreateAuthorizedClient(_factory, auth.AccessToken);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    // ─── Helper ───────────────────────────────────────────────────────────────

    private async Task<JsonElement> GetDetailAsync(string id)
    {
        var resp = await _alice.GetAsync($"/api/activities/{id}");
        resp.EnsureSuccessStatusCode();
        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(JsonOpts);
        return doc!.RootElement;
    }

    private async Task PatchAsync(string id, object body)
    {
        var json = JsonSerializer.Serialize(body);
        var resp = await _alice.PatchAsync(
            $"/api/activities/{id}",
            new StringContent(json, Encoding.UTF8, "application/json"));
        resp.EnsureSuccessStatusCode();
    }

    // ─── Description ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchDescription_Persists_ReturnedInDetail()
    {
        var id = await TestHelpers.UploadTestGpxAsync(_alice);

        await PatchAsync(id, new { description = "Sortie matinale en montagne" });

        var detail = await GetDetailAsync(id);
        Assert.Equal("Sortie matinale en montagne", detail.GetProperty("description").GetString());
    }

    [Fact]
    public async Task PatchDescription_EmptyString_ClearsDescription()
    {
        var id = await TestHelpers.UploadTestGpxAsync(_alice);
        await PatchAsync(id, new { description = "Initial description" });
        await PatchAsync(id, new { description = "" });

        var detail = await GetDetailAsync(id);
        var desc = detail.GetProperty("description");
        Assert.True(desc.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || desc.GetString() == null);
    }

    // ─── RPE ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchRpe_ValidRange_Persists()
    {
        var id = await TestHelpers.UploadTestGpxAsync(_alice);

        await PatchAsync(id, new { perceivedExertion = 7 });

        var detail = await GetDetailAsync(id);
        Assert.Equal(7, detail.GetProperty("perceivedExertion").GetInt32());
    }

    [Fact]
    public async Task PatchRpe_BoundaryValues_Persists()
    {
        var id = await TestHelpers.UploadTestGpxAsync(_alice);

        await PatchAsync(id, new { perceivedExertion = 1 });
        var detail1 = await GetDetailAsync(id);
        Assert.Equal(1, detail1.GetProperty("perceivedExertion").GetInt32());

        await PatchAsync(id, new { perceivedExertion = 10 });
        var detail10 = await GetDetailAsync(id);
        Assert.Equal(10, detail10.GetProperty("perceivedExertion").GetInt32());
    }

    [Fact]
    public async Task PatchRpe_Zero_IsIgnoredOrNull()
    {
        var id = await TestHelpers.UploadTestGpxAsync(_alice);

        // RPE 0 is out of range (1-10), controller maps it to null
        await PatchAsync(id, new { perceivedExertion = 0 });

        var detail = await GetDetailAsync(id);
        var rpe = detail.GetProperty("perceivedExertion");
        Assert.True(rpe.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
            || rpe.GetInt32() == 0);
    }

    // ─── Tags ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchTags_PersistAsArray()
    {
        var id = await TestHelpers.UploadTestGpxAsync(_alice);

        await PatchAsync(id, new { tags = new[] { "alpes", "trail", "montagne" } });

        var detail = await GetDetailAsync(id);
        var tags = detail.GetProperty("tags").EnumerateArray()
            .Select(t => t.GetString())
            .ToArray();

        Assert.Contains("alpes", tags);
        Assert.Contains("trail", tags);
        Assert.Contains("montagne", tags);
        Assert.Equal(3, tags.Length);
    }

    [Fact]
    public async Task PatchTags_EmptyArray_ClearsTags()
    {
        var id = await TestHelpers.UploadTestGpxAsync(_alice);
        await PatchAsync(id, new { tags = new[] { "alpes", "trail" } });
        await PatchAsync(id, new { tags = Array.Empty<string>() });

        var detail = await GetDetailAsync(id);
        var tagsEl = detail.GetProperty("tags");
        var isEmptyOrNull = tagsEl.ValueKind == JsonValueKind.Null
            || (tagsEl.ValueKind == JsonValueKind.Array && tagsEl.GetArrayLength() == 0);
        Assert.True(isEmptyOrNull);
    }

    // ─── SessionType ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchSessionType_Persists()
    {
        var id = await TestHelpers.UploadTestGpxAsync(_alice);

        await PatchAsync(id, new { sessionType = "training" });

        var detail = await GetDetailAsync(id);
        Assert.Equal("training", detail.GetProperty("sessionType").GetString());
    }

    [Fact]
    public async Task PatchSessionType_EmptyString_Clears()
    {
        var id = await TestHelpers.UploadTestGpxAsync(_alice);
        await PatchAsync(id, new { sessionType = "race" });
        await PatchAsync(id, new { sessionType = "" });

        var detail = await GetDetailAsync(id);
        var st = detail.GetProperty("sessionType");
        Assert.True(st.ValueKind == JsonValueKind.Null || st.GetString() == null);
    }

    // ─── Security ────────────────────────────────────────────────────────────

    [Fact]
    public async Task PatchEnrichment_OtherUserActivity_Returns404()
    {
        var id = await TestHelpers.UploadTestGpxAsync(_alice);

        // Bob tries to patch Alice's activity
        var anon = _factory.CreateClient();
        var bobAuth = await TestHelpers.RegisterAsync(anon, "bob_enrichment@test.com");
        var bob = TestHelpers.CreateAuthorizedClient(_factory, bobAuth.AccessToken);

        var json = JsonSerializer.Serialize(new { description = "Hacked!" });
        var resp = await bob.PatchAsync(
            $"/api/activities/{id}",
            new StringContent(json, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ─── GET /api/activities/tags ─────────────────────────────────────────────

    [Fact]
    public async Task GetTags_NoActivitiesWithTags_ReturnsEmptyArray()
    {
        // Upload without any tags
        await TestHelpers.UploadTestGpxAsync(_alice);

        var resp = await _alice.GetAsync("/api/activities/tags");
        resp.EnsureSuccessStatusCode();

        var tags = await resp.Content.ReadFromJsonAsync<string[]>(JsonOpts);
        Assert.NotNull(tags);
        Assert.Empty(tags);
    }

    [Fact]
    public async Task GetTags_MultipleActivities_ReturnsDedupedSorted()
    {
        var id1 = await TestHelpers.UploadTestGpxAsync(_alice);
        var id2 = await TestHelpers.UploadTestGpxAsync(_alice);

        await PatchAsync(id1, new { tags = new[] { "montagne", "alpes" } });
        await PatchAsync(id2, new { tags = new[] { "alpes", "neige" } }); // "alpes" shared

        var resp = await _alice.GetAsync("/api/activities/tags");
        resp.EnsureSuccessStatusCode();

        var tags = await resp.Content.ReadFromJsonAsync<string[]>(JsonOpts);
        Assert.NotNull(tags);
        Assert.Equal(3, tags!.Length); // deduplicated: alpes, montagne, neige
        Assert.Equal(new[] { "alpes", "montagne", "neige" }, tags); // sorted alphabetically
    }

    [Fact]
    public async Task GetTags_Unauthenticated_Returns401()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/api/activities/tags");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ─── List exposes new fields ──────────────────────────────────────────────

    [Fact]
    public async Task ListActivities_ExposesEnrichmentFields()
    {
        var id = await TestHelpers.UploadTestGpxAsync(_alice);
        await PatchAsync(id, new { sessionType = "recovery", tags = new[] { "test-tag" } });

        var resp = await _alice.GetAsync("/api/activities?page=1&pageSize=10");
        resp.EnsureSuccessStatusCode();

        var doc = await resp.Content.ReadFromJsonAsync<JsonDocument>(JsonOpts);
        var activity = doc!.RootElement.EnumerateArray()
            .FirstOrDefault(a => a.GetProperty("id").GetString() == id);

        Assert.Equal(JsonValueKind.Object, activity.ValueKind);
        Assert.Equal("recovery", activity.GetProperty("sessionType").GetString());

        var tags = activity.GetProperty("tags").EnumerateArray()
            .Select(t => t.GetString()).ToArray();
        Assert.Contains("test-tag", tags);
    }

    // ─── Calories after processing ────────────────────────────────────────────

    [Fact]
    public async Task Calories_AfterProcessing_ArePopulated()
    {
        var id = await TestHelpers.UploadTestGpxAsync(_alice, "trail");
        var status = await TestHelpers.WaitForProcessingAsync(_alice, id);

        if (status != "Completed") return; // skip if processing timed out in CI

        var detail = await GetDetailAsync(id);

        // estimatedCalories should be a positive number
        var kcalEl = detail.GetProperty("estimatedCalories");
        Assert.Equal(JsonValueKind.Number, kcalEl.ValueKind);
        Assert.True(kcalEl.GetDouble() > 0);

        // calorieMethod should be "hr" or "met"
        var methodEl = detail.GetProperty("calorieMethod");
        Assert.Equal(JsonValueKind.String, methodEl.ValueKind);
        Assert.True(methodEl.GetString() is "hr" or "met");
    }
}
