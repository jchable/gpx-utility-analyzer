using System.Net;
using System.Net.Http.Json;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Services.Integrations;
using GpxAnalyzer.Api.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GpxAnalyzer.Api.Tests.Integrations;

[Collection("Integration")]
public class OAuthCallbackTests
{
    private sealed class OAuthImporterStub : IActivityImporter
    {
        public string ProviderName => "strava";
        public Task<string> GetAuthorizationUrlAsync(string callbackUrl, string state) =>
            Task.FromResult($"https://provider.test/authorize?state={Uri.EscapeDataString(state)}");
        public Task<TokenInfo> ExchangeCodeAsync(string code, string callbackUrl) =>
            Task.FromResult(new TokenInfo { AccessToken = "access", ExternalUserId = "athlete-1" });
        public Task<TokenInfo> RefreshTokenAsync(string refreshToken) => throw new NotSupportedException();
        public Task<bool> ValidateSubscriptionAsync(HttpContext context) => Task.FromResult(false);
        public Task<WebhookEvent?> ReadWebhookEventAsync(HttpContext context) => Task.FromResult<WebhookEvent?>(null);
        public Task<ImportedActivity> FetchActivityAsync(string externalId, string accessToken) => throw new NotSupportedException();
    }

    private static WebApplicationFactory<Program> WithOAuthStub(ApiFactory factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IActivityImporter>();
            services.AddScoped<IActivityImporter, OAuthImporterStub>();
        }));

    [Fact]
    public async Task Callback_WithoutAuthorizationHeader_IsNotRejectedAsUnauthorized()
    {
        using var factory = new ApiFactory();
        var anon = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing
            .WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        // A real OAuth redirect is a browser navigation: no Authorization header.
        var resp = await anon.GetAsync("/api/integrations/strava/callback?code=abc&state=garbage");

        // The state is invalid so we expect a 400, NOT a 401 — a 401 means the
        // [Authorize] filter short-circuited and the flow can never complete.
        Assert.NotEqual(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Connect_ReturnsAuthUrlCarryingAStateParameter()
    {
        using var baseFactory = new ApiFactory();
        using var factory = WithOAuthStub(baseFactory);
        var client = factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(client, $"oauth_{Guid.NewGuid():N}@test.local");
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var resp = await authed.PostAsync("/api/integrations/strava/connect", null);

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Contains("state=", body!["authUrl"]);
    }

    [Fact]
    public async Task Callback_ConcurrentReplay_AllowsExactlyOneBinding()
    {
        using var baseFactory = new ApiFactory();
        using var factory = WithOAuthStub(baseFactory);
        var anon = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var auth = await TestHelpers.RegisterAsync(anon, $"oauth_replay_{Guid.NewGuid():N}@test.local");
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var connect = await authed.PostAsync("/api/integrations/strava/connect", null);
        var body = await connect.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        var authUrl = new Uri(body!["authUrl"]);
        var state = System.Web.HttpUtility.ParseQueryString(authUrl.Query)["state"];
        Assert.False(string.IsNullOrWhiteSpace(state));

        var callbackUrl = $"/api/integrations/strava/callback?code=valid&state={Uri.EscapeDataString(state!)}";
        var callbacks = await Task.WhenAll(
            anon.GetAsync(callbackUrl),
            anon.GetAsync(callbackUrl));

        Assert.Single(callbacks, response => response.StatusCode == HttpStatusCode.Redirect);
        Assert.Single(callbacks, response => response.StatusCode == HttpStatusCode.BadRequest);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var integration = await db.Integrations.SingleAsync();
        Assert.Equal(Guid.Parse(auth.User.Id), integration.UserId);
    }

    // ─── Defect D: abandoned flows must not accumulate ───────────────────────

    /// <summary>
    /// OAuthStates rows were deleted only by a SUCCESSFUL callback. Every user who
    /// started a connect and then closed the tab left a row behind for good, so the
    /// table grew without bound on nothing but abandoned flows.
    /// </summary>
    [Fact]
    public async Task Connect_PurgesExpiredOAuthStates()
    {
        using var baseFactory = new ApiFactory();
        using var factory = WithOAuthStub(baseFactory);
        var client = factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(client, $"oauth_gc_{Guid.NewGuid():N}@test.local");
        var authed = factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        var abandoned = new[] { "ABANDONED-1", "ABANDONED-2", "ABANDONED-3" };
        var stillValid = "STILL-VALID";

        using (var seed = factory.Services.CreateScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<AppDbContext>();
            foreach (var nonce in abandoned)
                db.OAuthStates.Add(new GpxAnalyzer.Api.Entities.OAuthState
                {
                    Nonce = nonce,
                    UserId = Guid.Parse(auth.User.Id),
                    Provider = "strava",
                    ExpiresAt = DateTime.UtcNow.AddMinutes(-30),   // flows nobody finished
                });

            db.OAuthStates.Add(new GpxAnalyzer.Api.Entities.OAuthState
            {
                Nonce = stillValid,
                UserId = Guid.Parse(auth.User.Id),
                Provider = "strava",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),        // someone is mid-flow
            });
            await db.SaveChangesAsync();
        }

        (await authed.PostAsync("/api/integrations/strava/connect", null)).EnsureSuccessStatusCode();

        using var verify = factory.Services.CreateScope();
        var verifyDb = verify.ServiceProvider.GetRequiredService<AppDbContext>();
        var nonces = await verifyDb.OAuthStates.Select(s => s.Nonce).ToListAsync();

        foreach (var nonce in abandoned)
            Assert.DoesNotContain(nonce, nonces);

        // An unexpired flow belongs to a user who is still in the provider's consent
        // screen — purging it would break their connect.
        Assert.Contains(stillValid, nonces);

        // The nonce this very request created is naturally still there.
        Assert.Equal(2, nonces.Count);
    }
}
