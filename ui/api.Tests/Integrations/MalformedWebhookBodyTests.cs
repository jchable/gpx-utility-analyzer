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
/// Regression tests for #132 — a well-formed-JSON but wrong-typed webhook body threw
/// out of ReadWebhookEventAsync (GetString()/GetInt64() with no ValueKind guard),
/// producing a 500 on a public unauthenticated endpoint. A 500 also makes Strava
/// retry the same broken event.
///
/// A body we cannot interpret is now treated exactly like an unknown owner: log it
/// and answer 200 with nothing imported.
/// </summary>
[Collection("Integration")]
public class MalformedWebhookBodyTests
{
    /// <summary>Records whether the provider API was contacted at all.</summary>
    private sealed class NeverCalledApiStub : IHttpClientFactory
    {
        public int Calls;
        public HttpClient CreateClient(string name) => new(new Handler(this));

        private sealed class Handler(NeverCalledApiStub owner) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref owner.Calls);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }
        }
    }

    private const string Secret = "test-webhook-secret";

    private static WebApplicationFactory<Program> WithStubs(ApiFactory factory, NeverCalledApiStub stub) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Integrations:strava:WebhookSecret", Secret);
            builder.UseSetting("Integrations:garmin:WebhookSecret", Secret);
            builder.ConfigureServices(s => s.AddSingleton<IHttpClientFactory>(stub));
        });

    private static async Task SeedStravaIntegrationAsync(
        WebApplicationFactory<Program> factory, string userId, string athleteId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Integrations.Add(new Integration
        {
            Id = Guid.NewGuid(),
            UserId = Guid.Parse(userId),
            Provider = "strava",
            AccessToken = "alice-token",
            ExternalUserId = athleteId,
            IsActive = true,
        });
        await db.SaveChangesAsync();
    }

    private static async Task<int> ActivityCountAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Activities.CountAsync();
    }

    // ─── Strava ──────────────────────────────────────────────────────────────

    public static TheoryData<string, string> MalformedStravaBodies() => new()
    {
        // The reported case: object_id sent as a string rather than a number.
        { "object_id as a string", """
            {"object_type":"activity","aspect_type":"create","object_id":"9001","owner_id":1001}
            """ },
        { "object_id as an object", """
            {"object_type":"activity","aspect_type":"create","object_id":{"id":1},"owner_id":1001}
            """ },
        { "object_id as an array", """
            {"object_type":"activity","aspect_type":"create","object_id":[9001],"owner_id":1001}
            """ },
        { "object_id as a boolean", """
            {"object_type":"activity","aspect_type":"create","object_id":true,"owner_id":1001}
            """ },
        { "object_id as a float", """
            {"object_type":"activity","aspect_type":"create","object_id":1.5,"owner_id":1001}
            """ },
        { "object_type as a number", """
            {"object_type":7,"aspect_type":"create","object_id":9001,"owner_id":1001}
            """ },
        { "aspect_type as an object", """
            {"object_type":"activity","aspect_type":{"x":1},"object_id":9001,"owner_id":1001}
            """ },
        { "owner_id as a boolean", """
            {"object_type":"activity","aspect_type":"create","object_id":9001,"owner_id":false}
            """ },
        { "owner_id as an array", """
            {"object_type":"activity","aspect_type":"create","object_id":9001,"owner_id":[1001]}
            """ },
        { "a bare JSON array", "[1,2,3]" },
        { "a bare JSON string", "\"hello\"" },
        { "JSON null", "null" },
        { "not JSON at all", "<xml/>" },
        { "an empty body", "" },
    };

    [Theory]
    [MemberData(nameof(MalformedStravaBodies))]
    public async Task StravaWebhook_WithAMalformedBody_IsDroppedWith200(string label, string body)
    {
        using var baseFactory = new ApiFactory();
        var stub = new NeverCalledApiStub();
        using var factory = WithStubs(baseFactory, stub);
        var client = factory.CreateClient();

        var alice = await TestHelpers.RegisterAsync(client, $"malformed_{Guid.NewGuid():N}@test.local");
        await SeedStravaIntegrationAsync(factory, alice.User.Id, "1001");

        var resp = await client.PostAsync(
            $"/api/webhooks/strava?secret={Secret}",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.False((int)resp.StatusCode >= 500,
            $"{label}: a public unauthenticated endpoint returned {(int)resp.StatusCode}, " +
            "which also makes the provider retry");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(0, await ActivityCountAsync(factory));
        Assert.Equal(0, stub.Calls);
    }

    // ─── Garmin ──────────────────────────────────────────────────────────────

    public static TheoryData<string, string> MalformedGarminBodies() => new()
    {
        { "activityId as a string", """{"activityDetails":[{"activityId":"555","userId":"g-1"}]}""" },
        { "activityId as an object", """{"activityDetails":[{"activityId":{"v":1},"userId":"g-1"}]}""" },
        { "activityId as a boolean", """{"activityDetails":[{"activityId":true,"userId":"g-1"}]}""" },
        { "userId as an object", """{"activityDetails":[{"activityId":555,"userId":{"v":1}}]}""" },
        { "userId as an array", """{"activityDetails":[{"activityId":555,"userId":[1]}]}""" },
        // The index-out-of-range path: nothing to read at activityDetails[0].
        { "an empty activityDetails array", """{"activityDetails":[]}""" },
        { "activityDetails as an object", """{"activityDetails":{"activityId":555}}""" },
        { "a bare JSON array", "[1,2,3]" },
        { "not JSON at all", "<xml/>" },
        { "an empty body", "" },
    };

    [Theory]
    [MemberData(nameof(MalformedGarminBodies))]
    public async Task GarminWebhook_WithAMalformedBody_IsDroppedWith200(string label, string body)
    {
        using var baseFactory = new ApiFactory();
        var stub = new NeverCalledApiStub();
        using var factory = WithStubs(baseFactory, stub);
        var client = factory.CreateClient();

        var resp = await client.PostAsync(
            $"/api/webhooks/garmin?secret={Secret}",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.False((int)resp.StatusCode >= 500,
            $"{label}: a public unauthenticated endpoint returned {(int)resp.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(0, await ActivityCountAsync(factory));
        Assert.Equal(0, stub.Calls);
    }

    /// <summary>
    /// subscription_id is only read when a subscription is configured, so this case
    /// needs its own fixture to exercise the guard at all.
    /// </summary>
    [Theory]
    [InlineData("""{"subscription_id":{"a":1},"object_type":"activity","aspect_type":"create","object_id":9001,"owner_id":1001}""")]
    [InlineData("""{"subscription_id":[12345],"object_type":"activity","aspect_type":"create","object_id":9001,"owner_id":1001}""")]
    [InlineData("""{"subscription_id":true,"object_type":"activity","aspect_type":"create","object_id":9001,"owner_id":1001}""")]
    public async Task StravaWebhook_WithAMalformedSubscriptionId_IsDroppedWith200(string body)
    {
        using var baseFactory = new ApiFactory();
        var stub = new NeverCalledApiStub();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Integrations:strava:WebhookSecret", Secret);
            builder.UseSetting("Integrations:Strava:SubscriptionId", "12345");
            builder.ConfigureServices(s => s.AddSingleton<IHttpClientFactory>(stub));
        });
        var client = factory.CreateClient();

        var alice = await TestHelpers.RegisterAsync(client, $"sub_{Guid.NewGuid():N}@test.local");
        await SeedStravaIntegrationAsync(factory, alice.User.Id, "1001");

        var resp = await client.PostAsync(
            $"/api/webhooks/strava?secret={Secret}",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.False((int)resp.StatusCode >= 500, $"returned {(int)resp.StatusCode}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(0, await ActivityCountAsync(factory));
        Assert.Equal(0, stub.Calls);
    }

    // ─── A well-formed body still works ──────────────────────────────────────

    [Fact]
    public async Task StravaWebhook_WithAWellFormedBody_ForAnUnknownOwner_IsStillDropped()
    {
        using var baseFactory = new ApiFactory();
        var stub = new NeverCalledApiStub();
        using var factory = WithStubs(baseFactory, stub);
        var client = factory.CreateClient();

        var resp = await client.PostAsJsonAsync($"/api/webhooks/strava?secret={Secret}", new
        {
            object_type = "activity",
            aspect_type = "create",
            object_id = 9001L,
            owner_id = 2002L,
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(0, stub.Calls);
    }
}
