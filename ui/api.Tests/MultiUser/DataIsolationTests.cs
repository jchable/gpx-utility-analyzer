using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GpxAnalyzer.Api.Tests.Helpers;

namespace GpxAnalyzer.Api.Tests.MultiUser;

/// <summary>
/// Integration tests verifying that each user can only see their own data.
/// Each test gets a fully isolated environment (own factory + own DB).
/// </summary>
public class DataIsolationTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private HttpClient _clientAlice = null!;
    private HttpClient _clientBob = null!;
    private string _aliceActivityId = "";

    public async Task InitializeAsync()
    {
        var anon = _factory.CreateClient();

        // Register two independent users
        var alice = await TestHelpers.RegisterAsync(anon, "alice@test.com", displayName: "Alice Runner");
        var bob   = await TestHelpers.RegisterAsync(anon, "bob@test.com",   displayName: "Bob Hiker");

        _clientAlice = TestHelpers.CreateAuthorizedClient(_factory, alice.AccessToken);
        _clientBob   = TestHelpers.CreateAuthorizedClient(_factory, bob.AccessToken);

        // Upload one activity as Alice and wait for processing to complete
        _aliceActivityId = await TestHelpers.UploadTestGpxAsync(_clientAlice, "trail");
        await TestHelpers.WaitForProcessingAsync(_clientAlice, _aliceActivityId);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    // ─── Activities isolation ─────────────────────────────────────────────────

    [Fact]
    public async Task Activities_AliceSeesHerActivity()
    {
        var resp = await _clientAlice.GetAsync("/api/activities?page=1&pageSize=20");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var items = json!.RootElement.EnumerateArray().ToList();

        Assert.Single(items);
        Assert.Equal(_aliceActivityId, items[0].GetProperty("id").GetString());
    }

    [Fact]
    public async Task Activities_BobSeesEmptyList()
    {
        var resp = await _clientBob.GetAsync("/api/activities?page=1&pageSize=20");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Empty(json!.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Activity_BobCannotAccessAlicesActivity()
    {
        var resp = await _clientBob.GetAsync($"/api/activities/{_aliceActivityId}");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Activity_AliceCanAccessHerOwnActivity()
    {
        var resp = await _clientAlice.GetAsync($"/api/activities/{_aliceActivityId}");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(_aliceActivityId, json!.RootElement.GetProperty("id").GetString());
    }

    // ─── Dashboard isolation ──────────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_AliceSeesHerActivity()
    {
        var resp = await _clientAlice.GetAsync("/api/dashboard/summary");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(1, json!.RootElement.GetProperty("totalActivities").GetInt32());
    }

    [Fact]
    public async Task Dashboard_BobSeesZeroActivities()
    {
        var resp = await _clientBob.GetAsync("/api/dashboard/summary");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(0, json!.RootElement.GetProperty("totalActivities").GetInt32());
    }

    [Fact]
    public async Task Dashboard_BobSeesZeroDistance()
    {
        var resp = await _clientBob.GetAsync("/api/dashboard/summary");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal(0.0, json!.RootElement.GetProperty("totalDistanceKm").GetDouble());
    }

    // ─── Routes isolation ─────────────────────────────────────────────────────

    [Fact]
    public async Task Routes_BobCannotSeeAlicesRoutes()
    {
        // Alice creates a route
        var createResp = await _clientAlice.PostAsJsonAsync("/api/routes",
            new { name = "Alice's route", activityType = "trail" });
        createResp.EnsureSuccessStatusCode();

        // Bob lists routes — should be empty
        var listResp = await _clientBob.GetAsync("/api/routes?page=1&pageSize=20");
        listResp.EnsureSuccessStatusCode();

        var json = await listResp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Empty(json!.RootElement.EnumerateArray());
    }

    [Fact]
    public async Task Routes_BobCannotDirectlyAccessAlicesRoute()
    {
        // Alice creates a route
        var createResp = await _clientAlice.PostAsJsonAsync("/api/routes",
            new { name = "Alice's private route", activityType = "trail" });
        createResp.EnsureSuccessStatusCode();

        var created = await createResp.Content.ReadFromJsonAsync<JsonDocument>();
        var routeId = created!.RootElement.GetProperty("id").GetString()!;

        // Bob tries direct access
        var bobResp = await _clientBob.GetAsync($"/api/routes/{routeId}");
        Assert.Equal(HttpStatusCode.NotFound, bobResp.StatusCode);
    }

    // ─── GPX download isolation ───────────────────────────────────────────────

    [Fact]
    public async Task GpxDownload_BobCannotDownloadAlicesGpx()
    {
        var resp = await _clientBob.GetAsync($"/api/activities/{_aliceActivityId}/gpx");

        // Should be 404 (not found for this user)
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    // ─── Deletion isolation ───────────────────────────────────────────────────

    [Fact]
    public async Task Delete_BobCannotDeleteAlicesActivity()
    {
        var resp = await _clientBob.DeleteAsync($"/api/activities/{_aliceActivityId}");

        // 404 because Bob doesn't see Alice's activity
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);

        // Alice's activity must still be there
        var check = await _clientAlice.GetAsync($"/api/activities/{_aliceActivityId}");
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
    }
}
