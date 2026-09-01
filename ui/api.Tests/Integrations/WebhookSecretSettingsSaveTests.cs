using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GpxAnalyzer.Api.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GpxAnalyzer.Api.Tests.Integrations;

/// <summary>
/// Regression tests for issue #143.
///
/// <see cref="GpxAnalyzer.Api.Services.Integrations.WebhookSecretValidator"/> runs at
/// startup, so it cannot see a credential that did not exist at startup. Provider
/// credentials are also settable at runtime through
/// <c>PUT /api/settings/global</c>, which left a window where saving Strava
/// credentials without a webhook secret produced silent 401s on every inbound
/// webhook until the next restart — and then an API that refused to boot, long
/// after the change that caused it.
///
/// The save is therefore rejected with the same message the startup validator
/// produces. Auto-generating a secret was rejected as a fix: the operator has to
/// know its value anyway to register the provider's callback URL.
///
/// The check runs against the state that WOULD RESULT from the update, not the
/// request body, because <c>UpdateGlobalSettings</c> only writes keys whose
/// incoming value is non-empty — so a client id and its webhook secret may
/// legitimately arrive in two separate requests.
/// </summary>
[Collection("Integration")]
public class WebhookSecretSettingsSaveTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>The first registered user gets the Admin role, which /global requires.</summary>
    private static async Task<HttpClient> AdminClientAsync(ApiFactory factory)
    {
        var anonymous = factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(anonymous, "admin@test.com");
        Assert.Equal("Admin", auth.User.Role);
        return TestHelpers.CreateAuthorizedClient(factory, auth.AccessToken);
    }

    private static object StravaBody(string clientId = "", string clientSecret = "", string webhookSecret = "")
        => new { integrations = new { strava = new { clientId, clientSecret, webhookSecret } } };

    private static object GarminBody(string consumerKey = "", string consumerSecret = "", string webhookSecret = "")
        => new { integrations = new { garmin = new { consumerKey, consumerSecret, webhookSecret } } };

    private static string Flatten(Exception ex)
    {
        var text = "";
        for (Exception? e = ex; e is not null; e = e.InnerException)
            text += e.Message + "\n";
        return text;
    }

    // ─── Reject: the update would leave a provider without a webhook secret ──

    [Fact]
    public async Task SavingStravaCredentialsWithoutAWebhookSecret_IsRejected()
    {
        using var factory = new ApiFactory();
        var client = await AdminClientAsync(factory);

        var resp = await client.PutAsJsonAsync("/api/settings/global",
            StravaBody(clientId: "an-id", clientSecret: "a-secret"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();

        // Same actionable content the startup validator gives: which provider,
        // which key, its environment form, and the re-registration required.
        Assert.Contains("strava", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Integrations:Strava:WebhookSecret", body, StringComparison.Ordinal);
        Assert.Contains("Integrations__Strava__WebhookSecret", body, StringComparison.Ordinal);
        Assert.Contains("re-register", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/webhooks/strava?secret=", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SavingGarminCredentialsWithoutAWebhookSecret_IsRejected()
    {
        using var factory = new ApiFactory();
        var client = await AdminClientAsync(factory);

        var resp = await client.PutAsJsonAsync("/api/settings/global",
            GarminBody(consumerKey: "a-key", consumerSecret: "a-secret"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        var body = await resp.Content.ReadAsStringAsync();
        Assert.Contains("garmin", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Integrations:Garmin:WebhookSecret", body, StringComparison.Ordinal);
        Assert.Contains("Integrations__Garmin__WebhookSecret", body, StringComparison.Ordinal);
        Assert.Contains("/api/webhooks/garmin?secret=", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// A rejected save must persist NOTHING. Writing the credential and only then
    /// refusing would leave exactly the broken state the check exists to prevent,
    /// and would brick the next restart.
    /// </summary>
    [Fact]
    public async Task ARejectedSave_PersistsNothing()
    {
        using var factory = new ApiFactory();
        var client = await AdminClientAsync(factory);

        var rejected = await client.PutAsJsonAsync("/api/settings/global",
            StravaBody(clientId: "an-id", clientSecret: "a-secret"));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);

        var read = await client.GetAsync("/api/settings/global");
        read.EnsureSuccessStatusCode();
        var dto = await read.Content.ReadFromJsonAsync<JsonDocument>(JsonOpts);

        var strava = dto!.RootElement.GetProperty("integrations").GetProperty("strava");
        Assert.Equal("", strava.GetProperty("clientId").GetString());
        Assert.False(strava.GetProperty("hasClientSecret").GetBoolean());
    }

    // ─── Accept: the resulting state has both ────────────────────────────────

    [Fact]
    public async Task SavingCredentialsTogetherWithAWebhookSecret_IsAccepted()
    {
        using var factory = new ApiFactory();
        var client = await AdminClientAsync(factory);

        var resp = await client.PutAsJsonAsync("/api/settings/global",
            StravaBody(clientId: "an-id", clientSecret: "a-secret", webhookSecret: "a-long-random-secret"));

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    /// <summary>
    /// The partial-update case. <c>UpdateGlobalSettings</c> only writes non-empty
    /// values, so saving the webhook secret first and the client id second is a
    /// legitimate sequence. Validating the REQUEST would reject the second call;
    /// validating the RESULTING state accepts it.
    /// </summary>
    [Fact]
    public async Task SavingAClientIdWhenAWebhookSecretIsAlreadyStored_IsAccepted()
    {
        using var factory = new ApiFactory();
        var client = await AdminClientAsync(factory);

        // Request 1: the webhook secret alone. No credentials yet, so nothing to refuse.
        var first = await client.PutAsJsonAsync("/api/settings/global",
            StravaBody(webhookSecret: "a-long-random-secret"));
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // Request 2: the credentials, carrying no webhook secret of their own.
        var second = await client.PutAsJsonAsync("/api/settings/global",
            StravaBody(clientId: "an-id", clientSecret: "a-secret"));

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
    }

    /// <summary>Mirror image: the same second request WITHOUT a stored secret is refused.</summary>
    [Fact]
    public async Task SavingAClientIdWhenNoWebhookSecretIsStored_IsRejected()
    {
        using var factory = new ApiFactory();
        var client = await AdminClientAsync(factory);

        // Request 1 stores an unrelated setting, so the only difference from the
        // accepted case is the presence of the webhook secret.
        var first = await client.PutAsJsonAsync("/api/settings/global",
            new { aiProvider = new { model = "a-model" } });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.PutAsJsonAsync("/api/settings/global",
            StravaBody(clientId: "an-id", clientSecret: "a-secret"));

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    /// <summary>A save that touches no integration at all is never affected.</summary>
    [Fact]
    public async Task SavingUnrelatedSettings_IsAccepted()
    {
        using var factory = new ApiFactory();
        var client = await AdminClientAsync(factory);

        var resp = await client.PutAsJsonAsync("/api/settings/global",
            new { aiProvider = new { name = "ollama", model = "llama3" } });

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    // ─── The stored webhook secret is never echoed back ──────────────────────

    /// <summary>
    /// The DTO reports <c>HasClientSecret</c> rather than the secret itself; a
    /// webhook secret is equally sensitive and gets the same treatment.
    /// </summary>
    [Fact]
    public async Task TheStoredWebhookSecret_IsReportedAsABooleanAndNeverEchoedBack()
    {
        const string secret = "a-long-random-secret-not-to-be-echoed";

        using var factory = new ApiFactory();
        var client = await AdminClientAsync(factory);

        var saved = await client.PutAsJsonAsync("/api/settings/global",
            StravaBody(clientId: "an-id", clientSecret: "a-secret", webhookSecret: secret));
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);

        var read = await client.GetAsync("/api/settings/global");
        read.EnsureSuccessStatusCode();
        var body = await read.Content.ReadAsStringAsync();

        Assert.DoesNotContain(secret, body, StringComparison.Ordinal);

        var dto = JsonDocument.Parse(body);
        var strava = dto.RootElement.GetProperty("integrations").GetProperty("strava");
        Assert.True(strava.GetProperty("hasWebhookSecret").GetBoolean());

        var garmin = dto.RootElement.GetProperty("integrations").GetProperty("garmin");
        Assert.False(garmin.GetProperty("hasWebhookSecret").GetBoolean());
    }

    // ─── Both paths must teach the same thing ────────────────────────────────

    /// <summary>
    /// The rejection the settings save returns must be the message the startup
    /// validator produces — character for character — so the two cannot drift into
    /// separate copies that explain the same misconfiguration differently.
    /// </summary>
    [Theory]
    [InlineData("Integrations:Strava:ClientId")]
    [InlineData("Integrations:Garmin:ConsumerKey")]
    public async Task TheSaveRejectionAndTheStartupRefusal_ShareOneMessage(string credentialKey)
    {
        // What the startup validator says for this provider.
        using var startupBase = new ApiFactory();
        using var startupFactory = startupBase.WithWebHostBuilder(b => b.UseSetting(credentialKey, "an-id"));
        var startupText = Flatten(Assert.ThrowsAny<Exception>(() => startupFactory.CreateClient()));

        // What the settings save says for the same provider.
        using var factory = new ApiFactory();
        var client = await AdminClientAsync(factory);

        var body = credentialKey.Contains("Strava", StringComparison.Ordinal)
            ? StravaBody(clientId: "an-id")
            : GarminBody(consumerKey: "an-id");

        var resp = await client.PutAsJsonAsync("/api/settings/global", body);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>(JsonOpts);
        var saveMessage = json!.RootElement.GetProperty("message").GetString()!;

        Assert.False(string.IsNullOrWhiteSpace(saveMessage));
        Assert.Contains(saveMessage, startupText, StringComparison.Ordinal);
    }
}
