using System.Net;
using System.Net.Http.Json;
using System.Text;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GpxAnalyzer.Api.Tests.Integrations;

/// <summary>
/// Regression tests for #92 (webhook picked an arbitrary integration row, so an
/// event for one athlete was fetched with another user's OAuth token and stored
/// under that other user) and #94 (the POST path validated nothing).
///
/// The real <c>StravaService</c> runs — only its <see cref="IHttpClientFactory"/>
/// is replaced, so the Strava HTTP calls are canned and the test can observe
/// *which* access token the handler decided to use.
/// </summary>
[Collection("Integration")]
public class WebhookRoutingTests
{
    // ─── Test doubles ────────────────────────────────────────────────────────

    /// <summary>Canned Strava API: records the bearer token it was called with.</summary>
    private sealed class StravaApiStub : IHttpClientFactory
    {
        public string? LastAuthorizationToken { get; private set; }
        public int ActivityFetchCount { get; private set; }

        public HttpClient CreateClient(string name) => new(new Handler(this));

        private sealed class Handler : HttpMessageHandler
        {
            private readonly StravaApiStub _owner;
            public Handler(StravaApiStub owner) => _owner = owner;

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var url = request.RequestUri!.ToString();

                if (url.Contains("/activities/"))
                {
                    _owner.LastAuthorizationToken = request.Headers.Authorization?.Parameter;

                    var json = url.Contains("/streams")
                        ? """
                          {
                            "latlng":   { "data": [[45.0,6.0],[45.001,6.001],[45.002,6.002]] },
                            "altitude": { "data": [1000.0, 1010.0, 1020.0] },
                            "time":     { "data": [0, 10, 20] }
                          }
                          """
                        : """
                          { "name": "Imported run", "type": "Run",
                            "start_date": "2026-01-01T10:00:00Z" }
                          """;

                    if (!url.Contains("/streams")) _owner.ActivityFetchCount++;

                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json"),
                    });
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }
    }

    // ─── Fixture helpers ─────────────────────────────────────────────────────

    private static WebApplicationFactory<Program> WithStravaStub(
        ApiFactory factory, StravaApiStub stub, string? subscriptionId = null)
        => factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Integrations:strava:WebhookSecret", "test-webhook-secret");
            if (subscriptionId is not null)
                builder.UseSetting("Integrations:Strava:SubscriptionId", subscriptionId);

            // Last registration wins for a single-service resolve.
            builder.ConfigureServices(s => s.AddSingleton<IHttpClientFactory>(stub));
        });

    private static async Task SeedIntegrationAsync(
        WebApplicationFactory<Program> factory, string userId, string athleteId, string token)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Integrations.Add(new Integration
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse(userId),
            Provider = "strava",
            AccessToken = token,
            ExternalUserId = athleteId,
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<List<Activity>> StravaActivitiesAsync(
        WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Activities.Where(a => a.Source == "strava").ToListAsync();
    }

    // ─── #92: routing ────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_RoutesActivityToTheOwningUser_NotTheFirstIntegration()
    {
        using var baseFactory = new ApiFactory();
        var stub = new StravaApiStub();
        using var factory = WithStravaStub(baseFactory, stub);
        var client = factory.CreateClient();

        // Alice's row is inserted first, so the buggy
        // FirstOrDefault(provider && IsActive) always selects it.
        var alice = await TestHelpers.RegisterAsync(client, $"alice_{Guid.NewGuid():N}@test.local");
        await SeedIntegrationAsync(factory, alice.User.Id, "1001", "alice-token");

        var bob = await TestHelpers.RegisterAsync(client, $"bob_{Guid.NewGuid():N}@test.local");
        await SeedIntegrationAsync(factory, bob.User.Id, "2002", "bob-token");

        // Bob (athlete 2002) finishes a run.
        var resp = await client.PostAsJsonAsync("/api/webhooks/strava?secret=test-webhook-secret", new
        {
            object_type = "activity",
            aspect_type = "create",
            object_id = 9001L,
            owner_id = 2002L,
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // The activity must belong to Bob, and it must have been fetched with
        // Bob's OAuth token — not with whichever integration row came first.
        var activities = await StravaActivitiesAsync(factory);
        var activity = Assert.Single(activities);
        Assert.Equal(Guid.Parse(bob.User.Id), activity.UserId);
        Assert.Equal("bob-token", stub.LastAuthorizationToken);
    }

    [Fact]
    public async Task Webhook_ForUnknownOwner_DoesNotTouchAnyIntegration()
    {
        using var baseFactory = new ApiFactory();
        var stub = new StravaApiStub();
        using var factory = WithStravaStub(baseFactory, stub);
        var client = factory.CreateClient();

        // Alice connects Strava as athlete 1001. Her row is the only one, so the
        // buggy FirstOrDefault(provider && IsActive) always selects it.
        var alice = await TestHelpers.RegisterAsync(client, $"alice_{Guid.NewGuid():N}@test.local");
        await SeedIntegrationAsync(factory, alice.User.Id, "1001", "alice-token");

        // Bob (athlete 2002, not connected here) finishes a run.
        var resp = await client.PostAsJsonAsync("/api/webhooks/strava?secret=test-webhook-secret", new
        {
            object_type = "activity",
            aspect_type = "create",
            object_id = 9001L,
            owner_id = 2002L,
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        // No activity may be created for Alice from Bob's event, and Alice's
        // credentials must never have been used to fetch it.
        Assert.Empty(await StravaActivitiesAsync(factory));
        Assert.Null(stub.LastAuthorizationToken);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var row = await db.Integrations.SingleAsync(i => i.Provider == "strava");
        Assert.Equal("alice-token", row.AccessToken);
    }

    [Fact]
    public async Task Webhook_WithNoOwnerId_IsDroppedInsteadOfGuessing()
    {
        using var baseFactory = new ApiFactory();
        var stub = new StravaApiStub();
        using var factory = WithStravaStub(baseFactory, stub);
        var client = factory.CreateClient();

        var alice = await TestHelpers.RegisterAsync(client, $"alice2_{Guid.NewGuid():N}@test.local");
        await SeedIntegrationAsync(factory, alice.User.Id, "1001", "alice-token");

        // An anonymous attacker's minimal injection payload: no owner_id.
        var resp = await client.PostAsJsonAsync("/api/webhooks/strava?secret=test-webhook-secret", new
        {
            object_type = "activity",
            aspect_type = "create",
            object_id = 123456L,
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty(await StravaActivitiesAsync(factory));
        Assert.Null(stub.LastAuthorizationToken);
    }

    // ─── #94: request validation ─────────────────────────────────────────────

    [Fact]
    public async Task Webhook_FromUnknownSubscription_IsRejected()
    {
        using var baseFactory = new ApiFactory();
        var stub = new StravaApiStub();
        using var factory = WithStravaStub(baseFactory, stub, subscriptionId: "12345");
        var client = factory.CreateClient();

        var alice = await TestHelpers.RegisterAsync(client, $"alice3_{Guid.NewGuid():N}@test.local");
        await SeedIntegrationAsync(factory, alice.User.Id, "1001", "alice-token");

        // Correct owner, but the event was not issued against our subscription.
        var resp = await client.PostAsJsonAsync("/api/webhooks/strava?secret=test-webhook-secret", new
        {
            subscription_id = 999L,
            object_type = "activity",
            aspect_type = "create",
            object_id = 4242L,
            owner_id = 1001L,
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Empty(await StravaActivitiesAsync(factory));
        Assert.Null(stub.LastAuthorizationToken);
    }

    [Fact]
    public async Task Webhook_FromOurSubscription_ForAKnownOwner_IsImported()
    {
        using var baseFactory = new ApiFactory();
        var stub = new StravaApiStub();
        using var factory = WithStravaStub(baseFactory, stub, subscriptionId: "12345");
        var client = factory.CreateClient();

        var alice = await TestHelpers.RegisterAsync(client, $"alice4_{Guid.NewGuid():N}@test.local");
        await SeedIntegrationAsync(factory, alice.User.Id, "1001", "alice-token");

        var resp = await client.PostAsJsonAsync("/api/webhooks/strava?secret=test-webhook-secret", new
        {
            subscription_id = 12345L,
            object_type = "activity",
            aspect_type = "create",
            object_id = 4243L,
            owner_id = 1001L,
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var activity = Assert.Single(await StravaActivitiesAsync(factory));
        Assert.Equal(Guid.Parse(alice.User.Id), activity.UserId);
        Assert.Equal("alice-token", stub.LastAuthorizationToken);
    }

    [Fact]
    public async Task Webhook_WithoutSecret_IsRejectedBeforeUsingCredentials()
    {
        using var baseFactory = new ApiFactory();
        var stub = new StravaApiStub();
        using var factory = WithStravaStub(baseFactory, stub);
        var client = factory.CreateClient();
        var alice = await TestHelpers.RegisterAsync(client, $"unsigned_{Guid.NewGuid():N}@test.local");
        await SeedIntegrationAsync(factory, alice.User.Id, "1001", "alice-token");

        var resp = await client.PostAsJsonAsync("/api/webhooks/strava", new
        {
            object_type = "activity",
            aspect_type = "create",
            object_id = 9999L,
            owner_id = 1001L,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.Null(stub.LastAuthorizationToken);
        Assert.Empty(await StravaActivitiesAsync(factory));
    }
}
