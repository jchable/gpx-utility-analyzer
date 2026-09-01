using System.Net;
using System.Net.Http.Json;
using System.Text;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Services;
using GpxAnalyzer.Api.Services.Integrations;
using GpxAnalyzer.Api.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GpxAnalyzer.Api.Tests.Integrations;

/// <summary>
/// Regression tests for #130 — GarminService.ExchangeCodeAsync never populated
/// TokenInfo.ExternalUserId, so every Garmin integration stored a null one. Webhook
/// routing resolves the owning user by ExternalUserId, which made Garmin webhook
/// import completely inert: every event was logged and dropped.
///
/// Mirrors WebhookRoutingTests: the real GarminService runs and only its
/// IHttpClientFactory is replaced, so the test can observe which OAuth token the
/// handler decided to sign with.
/// </summary>
[Collection("Integration")]
public class GarminWebhookRoutingTests
{
    private const string Secret = "garmin-webhook-secret";

    private const string MinimalGpx = """
        <?xml version="1.0" encoding="UTF-8"?>
        <gpx version="1.1" creator="test" xmlns="http://www.topografix.com/GPX/1/1">
          <trk><trkseg>
            <trkpt lat="45.0" lon="6.0"><ele>1000</ele><time>2026-01-01T10:00:00Z</time></trkpt>
            <trkpt lat="45.001" lon="6.001"><ele>1010</ele><time>2026-01-01T10:00:10Z</time></trkpt>
          </trkseg></trk>
        </gpx>
        """;

    // ─── Test doubles ────────────────────────────────────────────────────────

    /// <summary>Canned Garmin API. Records the OAuth token each call was signed with.</summary>
    private sealed class GarminApiStub : IHttpClientFactory
    {
        public string? LastAuthorizationHeader { get; private set; }
        public int ActivityFileFetchCount { get; private set; }

        /// <summary>Value returned by the user-id endpoint; null makes it fail.</summary>
        public string? UserId { get; set; } = "garmin-user-42";

        public HttpClient CreateClient(string name) => new(new Handler(this));

        private sealed class Handler(GarminApiStub owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.ToString();
                owner.LastAuthorizationHeader = request.Headers.Authorization?.Parameter;

                if (url.Contains("/oauth/access_token"))
                    return Text("oauth_token=access-token&oauth_token_secret=access-secret");

                if (url.Contains("/wellness-api/rest/user/id"))
                    return owner.UserId is null
                        ? Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable))
                        : Json($$"""{"userId":"{{owner.UserId}}"}""");

                if (url.Contains("/wellness-api/rest/activityFile"))
                {
                    owner.ActivityFileFetchCount++;
                    return Text(MinimalGpx, "application/gpx+xml");
                }

                if (url.Contains("/wellness-api/rest/activityDetails"))
                    return Json("""{"activityName":"Imported trail","activityType":"trail_running"}""");

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            private static Task<HttpResponseMessage> Text(string body, string mediaType = "text/plain") =>
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, mediaType),
                });

            private static Task<HttpResponseMessage> Json(string body) => Text(body, "application/json");
        }
    }

    private sealed class FakeSettings(Dictionary<string, string> values) : ISettingsService
    {
        public Task<string?> GetAsync(string key, string? fallback = null) =>
            Task.FromResult(values.TryGetValue(key, out var v) ? v : fallback);

        public Task<string?> GetAsync(Guid userId, string key, string? fallback = null) =>
            GetAsync(key, fallback);

        public Task SetManyAsync(Guid userId, Dictionary<string, string> settings) => Task.CompletedTask;
        public Task SetGlobalManyAsync(Dictionary<string, string> settings) => Task.CompletedTask;
    }

    // ─── Fixture helpers ─────────────────────────────────────────────────────

    private static WebApplicationFactory<Program> WithGarminStub(ApiFactory factory, GarminApiStub stub) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Integrations:Garmin:ConsumerKey", "consumer-key");
            builder.UseSetting("Integrations:Garmin:ConsumerSecret", "consumer-secret");
            builder.UseSetting("Integrations:garmin:WebhookSecret", Secret);
            builder.ConfigureServices(s => s.AddSingleton<IHttpClientFactory>(stub));
        });

    private static async Task SeedIntegrationAsync(
        WebApplicationFactory<Program> factory, string userId, string? garminUserId, string token)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Integrations.Add(new Integration
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse(userId),
            Provider = "garmin",
            AccessToken = token,          // Garmin stores "token|secret"
            ExternalUserId = garminUserId,
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<List<Activity>> GarminActivitiesAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Activities.Where(a => a.Source == "garmin").ToListAsync();
    }

    private static object WebhookBody(long activityId, string garminUserId) => new
    {
        activityDetails = new[] { new { activityId, userId = garminUserId } },
    };

    // ─── Connect-time: the external user id must be captured ─────────────────

    [Fact]
    public async Task ExchangeCode_PopulatesTheExternalUserId()
    {
        var stub = new GarminApiStub { UserId = "garmin-user-42" };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("garmin:request_token:request-token", "request-secret");

        var service = new GarminService(
            new FakeSettings(new()
            {
                ["Integrations:Garmin:ConsumerKey"] = "consumer-key",
                ["Integrations:Garmin:ConsumerSecret"] = "consumer-secret",
            }),
            stub, cache, NullLogger<GarminService>.Instance);

        var token = await service.ExchangeCodeAsync("request-token|verifier", "https://host/callback");

        // Without this, webhook routing can never resolve the owning user.
        Assert.Equal("garmin-user-42", token.ExternalUserId);
        Assert.Equal("access-token|access-secret", token.AccessToken);
    }

    // ─── Routing: mirrors the Strava two-user test ───────────────────────────

    [Fact]
    public async Task Webhook_RoutesActivityToTheOwningUser_NotTheFirstIntegration()
    {
        using var baseFactory = new ApiFactory();
        var stub = new GarminApiStub();
        using var factory = WithGarminStub(baseFactory, stub);
        var client = factory.CreateClient();

        // Alice's row is inserted first, so a handler that just took the first
        // active garmin integration would always pick her.
        var alice = await TestHelpers.RegisterAsync(client, $"g_alice_{Guid.NewGuid():N}@test.local");
        await SeedIntegrationAsync(factory, alice.User.Id, "garmin-alice", "alice-token|alice-secret");

        var bob = await TestHelpers.RegisterAsync(client, $"g_bob_{Guid.NewGuid():N}@test.local");
        await SeedIntegrationAsync(factory, bob.User.Id, "garmin-bob", "bob-token|bob-secret");

        var resp = await client.PostAsJsonAsync(
            $"/api/webhooks/garmin?secret={Secret}", WebhookBody(9001L, "garmin-bob"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var activity = Assert.Single(await GarminActivitiesAsync(factory));
        Assert.Equal(Guid.Parse(bob.User.Id), activity.UserId);
        Assert.Equal("9001", activity.ExternalId);

        // And it was fetched with BOB's OAuth token, not whichever row came first.
        Assert.NotNull(stub.LastAuthorizationHeader);
        Assert.Contains("oauth_token=\"bob-token\"", stub.LastAuthorizationHeader);
        Assert.True(stub.ActivityFileFetchCount > 0);
    }

    [Fact]
    public async Task Webhook_ForAnUnknownGarminUser_DoesNotTouchAnyIntegration()
    {
        using var baseFactory = new ApiFactory();
        var stub = new GarminApiStub();
        using var factory = WithGarminStub(baseFactory, stub);
        var client = factory.CreateClient();

        var alice = await TestHelpers.RegisterAsync(client, $"g_only_{Guid.NewGuid():N}@test.local");
        await SeedIntegrationAsync(factory, alice.User.Id, "garmin-alice", "alice-token|alice-secret");

        var resp = await client.PostAsJsonAsync(
            $"/api/webhooks/garmin?secret={Secret}", WebhookBody(9002L, "garmin-stranger"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty(await GarminActivitiesAsync(factory));
        Assert.Equal(0, stub.ActivityFileFetchCount);
    }

    // ─── Existing rows with a null external user id ──────────────────────────

    [Fact]
    public async Task Integrations_SurfaceReconnectRequired_WhenTheExternalUserIdIsMissing()
    {
        using var baseFactory = new ApiFactory();
        var stub = new GarminApiStub();
        using var factory = WithGarminStub(baseFactory, stub);
        var client = factory.CreateClient();

        var auth = await TestHelpers.RegisterAsync(client, $"g_null_{Guid.NewGuid():N}@test.local");
        // A row written before ExchangeCodeAsync captured the id.
        await SeedIntegrationAsync(factory, auth.User.Id, null, "old-token|old-secret");

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var resp = await authed.GetAsync("/api/integrations");
        resp.EnsureSuccessStatusCode();

        var body = await resp.Content.ReadFromJsonAsync<List<Dictionary<string, object?>>>();
        var garmin = Assert.Single(body!, i => i["provider"]?.ToString() == "garmin");

        // Connected, but it can never receive a webhook — say so rather than
        // leaving the user wondering why nothing imports.
        Assert.True(((System.Text.Json.JsonElement)garmin["isConnected"]!).GetBoolean());
        Assert.True(((System.Text.Json.JsonElement)garmin["needsReconnect"]!).GetBoolean());
    }

    [Fact]
    public async Task Integrations_DoNotAskForAReconnect_WhenTheExternalUserIdIsPresent()
    {
        using var baseFactory = new ApiFactory();
        var stub = new GarminApiStub();
        using var factory = WithGarminStub(baseFactory, stub);
        var client = factory.CreateClient();

        var auth = await TestHelpers.RegisterAsync(client, $"g_ok_{Guid.NewGuid():N}@test.local");
        await SeedIntegrationAsync(factory, auth.User.Id, "garmin-ok", "tok|sec");

        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var body = await (await authed.GetAsync("/api/integrations"))
            .Content.ReadFromJsonAsync<List<Dictionary<string, object?>>>();
        var garmin = Assert.Single(body!, i => i["provider"]?.ToString() == "garmin");

        Assert.False(((System.Text.Json.JsonElement)garmin["needsReconnect"]!).GetBoolean());
    }
}
