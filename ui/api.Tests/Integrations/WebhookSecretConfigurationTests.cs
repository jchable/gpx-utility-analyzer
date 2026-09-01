using System.Collections.Concurrent;
using System.Net.Http.Json;
using GpxAnalyzer.Api.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GpxAnalyzer.Api.Tests.Integrations;

/// <summary>
/// Regression tests for defect B: the shipped appsettings.json has
/// <c>WebhookSecret: ""</c> for every provider and the handler 401s whenever the
/// expected secret is empty — so in the default configuration EVERY webhook was
/// rejected and imports stopped silently.
///
/// The secret is now mandatory: a provider that has credentials but no webhook
/// secret makes the API refuse to start, with a message naming the provider, the
/// config key, and the re-registration the change requires.
/// </summary>
[Collection("Integration")]
public class WebhookSecretConfigurationTests
{
    // ─── Log capture ─────────────────────────────────────────────────────────

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentBag<string> Messages { get; } = [];
        public ILogger CreateLogger(string categoryName) => new Capturing(this, categoryName);
        public void Dispose() { }

        private sealed class Capturing(CapturingLoggerProvider owner, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
                => owner.Messages.Add($"{category}: {formatter(state, exception)} {exception}");
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string Flatten(Exception ex)
    {
        var text = "";
        for (Exception? e = ex; e is not null; e = e.InnerException)
            text += e.Message + "\n";
        return text;
    }

    // ─── Startup validation ──────────────────────────────────────────────────

    [Theory]
    [InlineData("strava", "Integrations:Strava:ClientId", "Integrations:Strava:WebhookSecret")]
    [InlineData("garmin", "Integrations:Garmin:ConsumerKey", "Integrations:Garmin:WebhookSecret")]
    public void Startup_WithConfiguredCredentialsButNoWebhookSecret_RefusesToStart(
        string provider, string credentialKey, string secretKey)
    {
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(b => b.UseSetting(credentialKey, "an-id"));

        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        var message = Flatten(ex);

        // Actionable: which provider, which key to set, how to set it from the
        // environment, which credential made it required, and what else must change.
        Assert.Contains(provider, message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(secretKey, message, StringComparison.Ordinal);
        Assert.Contains(secretKey.Replace(":", "__"), message, StringComparison.Ordinal);
        Assert.Contains(credentialKey, message, StringComparison.Ordinal);
        Assert.Contains("re-register", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"/api/webhooks/{provider}?secret=", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Startup_WithConfiguredCredentialsAndAWebhookSecret_Starts()
    {
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Integrations:Strava:ClientId", "an-id");
            b.UseSetting("Integrations:Strava:WebhookSecret", "a-long-random-secret");
        });

        var client = factory.CreateClient();
        Assert.NotNull(client);
    }

    [Fact]
    public void Startup_WithNoProviderConfigured_Starts()
    {
        // The shipped default: empty credentials AND empty webhook secrets. Nothing
        // can be imported anyway, so there is nothing to refuse to start over.
        using var factory = new ApiFactory();
        Assert.NotNull(factory.CreateClient());
    }

    // ─── The secret must not reach the application log ───────────────────────

    [Fact]
    public async Task Webhook_DoesNotLogTheSuppliedSecret()
    {
        const string secret = "sup3r-s3cret-value-not-to-be-logged";

        var capture = new CapturingLoggerProvider();
        using var baseFactory = new ApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(b =>
        {
            b.UseSetting("Integrations:Strava:ClientId", "an-id");
            b.UseSetting("Integrations:Strava:WebhookSecret", secret);
            b.ConfigureServices(s => s.AddSingleton<ILoggerProvider>(capture));
        });

        var client = factory.CreateClient();

        // Strava cannot send custom headers, so the secret has to travel in the
        // query string — which means the application itself must never echo it.
        await client.PostAsJsonAsync($"/api/webhooks/strava?secret={secret}", new
        {
            object_type = "activity",
            aspect_type = "create",
            object_id = 1L,
            owner_id = 1L,
        });

        // A wrong secret must not be logged either.
        await client.PostAsJsonAsync("/api/webhooks/strava?secret=guessed-wrong", new
        {
            object_type = "activity",
            aspect_type = "create",
            object_id = 2L,
            owner_id = 1L,
        });

        var leaked = capture.Messages.Where(m => m.Contains(secret, StringComparison.Ordinal)).ToList();
        Assert.True(leaked.Count == 0,
            "the webhook secret appeared in the application log:\n" + string.Join("\n", leaked));

        var leakedGuess = capture.Messages
            .Where(m => m.Contains("guessed-wrong", StringComparison.Ordinal)).ToList();
        Assert.True(leakedGuess.Count == 0,
            "a supplied webhook secret appeared in the application log:\n" + string.Join("\n", leakedGuess));
    }
}
