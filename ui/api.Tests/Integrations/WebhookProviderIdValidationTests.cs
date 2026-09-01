using System.Net;
using System.Text;
using System.Text.Json;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Services.Integrations;
using GpxAnalyzer.Api.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GpxAnalyzer.Api.Tests.Integrations;

/// <summary>
/// Provider-side account ids arrive in an unauthenticated webhook body and are then
/// compared against stored ids, written to the database and named in operator log
/// lines. A value carrying CR/LF can forge whole log entries (CodeQL cs/log-forging),
/// so <see cref="WebhookJson.ReadProviderId"/> constrains what an id may contain and
/// the event is dropped when it does not fit.
/// </summary>
[Collection("Integration")]
public class WebhookProviderIdValidationTests
{
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

    // ─── The reader ──────────────────────────────────────────────────────────

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement;

    [Theory]
    [InlineData("""{"owner_id":1001}""", "1001")]                    // Strava: a number
    [InlineData("""{"owner_id":"1001"}""", "1001")]                  // a numeric string
    [InlineData("""{"owner_id":"d3315b1e-a3f4-4dc0-8b16-000000000001"}""", "d3315b1e-a3f4-4dc0-8b16-000000000001")]
    public void ReadProviderId_AcceptsTheShapesProvidersActuallySend(string json, string expected)
        => Assert.Equal(expected, WebhookJson.ReadProviderId(Parse(json), "owner_id"));

    [Theory]
    [InlineData("""{"owner_id":"1001\r\nWARN: forged log line"}""")]
    [InlineData("""{"owner_id":"1001\nforged"}""")]
    [InlineData("""{"owner_id":"1001 1002"}""")]
    [InlineData("""{"owner_id":"1001\u0000"}""")]
    [InlineData("""{"owner_id":""}""")]
    [InlineData("""{"owner_id":true}""")]
    public void ReadProviderId_RejectsAnythingThatIsNotAnId(string json)
        => Assert.Null(WebhookJson.ReadProviderId(Parse(json), "owner_id"));

    [Fact]
    public void ReadProviderId_RejectsAnAbsurdlyLongId()
    {
        var json = $$"""{"owner_id":"{{new string('7', 129)}}"}""";
        Assert.Null(WebhookJson.ReadProviderId(Parse(json), "owner_id"));
    }

    // ─── End to end ──────────────────────────────────────────────────────────

    /// <summary>
    /// The routing consequence: an owner id that cannot be logged safely never gets as
    /// far as being matched, even when a row carries that exact string. Before the
    /// reader validated its input this body matched the seeded integration and the
    /// provider API was called for it.
    /// </summary>
    [Fact]
    public async Task StravaWebhook_WithAnOwnerIdCarryingNewlines_IsDroppedBeforeRouting()
    {
        const string hostile = "1001\r\nwarn: Imported activity 4242 from strava";

        using var baseFactory = new ApiFactory();
        var stub = new NeverCalledApiStub();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Integrations:strava:WebhookSecret", Secret);
            builder.ConfigureServices(s => s.AddSingleton<IHttpClientFactory>(stub));
        });
        var client = factory.CreateClient();

        var alice = await TestHelpers.RegisterAsync(client, $"forge_{Guid.NewGuid():N}@test.local");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Integrations.Add(new Integration
            {
                Id = Guid.NewGuid(),
                UserId = Guid.Parse(alice.User.Id),
                Provider = "strava",
                AccessToken = "alice-token",
                ExternalUserId = hostile,
                IsActive = true,
            });
            await db.SaveChangesAsync();
        }

        var body = JsonSerializer.Serialize(new
        {
            object_type = "activity",
            aspect_type = "create",
            object_id = 9001L,
            owner_id = hostile,
        });

        var resp = await client.PostAsync(
            $"/api/webhooks/strava?secret={Secret}",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.Equal(0, stub.Calls);

        using var check = factory.Services.CreateScope();
        var activities = check.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await activities.Activities.CountAsync());
    }
}
