namespace GpxAnalyzer.Api.Services.Integrations;

/// <summary>
/// Refuses to start an API whose configured integrations cannot receive webhooks.
///
/// The webhook handler compares the supplied secret against the configured one and
/// rejects the request when the expected value is empty. The shipped
/// appsettings.json ships <c>WebhookSecret: ""</c>, so a deployment that filled in
/// only its OAuth credentials silently 401'd every event and imports stopped with
/// nothing but a warning line to show for it.
///
/// The secret is therefore mandatory: configuring a provider without one is a
/// misconfiguration, and it fails loudly at startup rather than quietly at runtime.
/// </summary>
public static class WebhookSecretValidator
{
    private sealed record Provider(string Name, string Section, string[] CredentialKeys);

    private static readonly Provider[] Providers =
    [
        new("strava", "Strava",
            ["Integrations:Strava:ClientId", "Integrations:Strava:ClientSecret"]),
        new("garmin", "Garmin",
            ["Integrations:Garmin:ConsumerKey", "Integrations:Garmin:ConsumerSecret"]),
    ];

    /// <summary>
    /// Reads through <see cref="ISettingsService"/> so a credential stored in
    /// GlobalSettings by the settings UI counts as "configured" just like one from
    /// appsettings or the environment.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A provider has credentials but no webhook secret.
    /// </exception>
    public static async Task ValidateAsync(ISettingsService settings)
    {
        foreach (var provider in Providers)
        {
            var configuredWith = await FirstConfiguredCredentialAsync(settings, provider);
            if (configuredWith is null) continue;

            var secretKey = $"Integrations:{provider.Section}:WebhookSecret";
            var secret = await settings.GetAsync(secretKey);
            if (!string.IsNullOrWhiteSpace(secret)) continue;

            throw new InvalidOperationException(BuildMessage(provider, configuredWith, secretKey));
        }
    }

    private static async Task<string?> FirstConfiguredCredentialAsync(
        ISettingsService settings, Provider provider)
    {
        foreach (var key in provider.CredentialKeys)
            if (!string.IsNullOrWhiteSpace(await settings.GetAsync(key)))
                return key;

        return null;
    }

    private static string BuildMessage(Provider provider, string configuredWith, string secretKey)
    {
        var envKey = secretKey.Replace(':', '_').Replace("_", "__");

        return $"""
            The '{provider.Name}' integration is configured ({configuredWith} is set) but has no
            webhook secret, so every incoming {provider.Name} webhook would be rejected with 401 and
            imports would stop silently. Refusing to start.

            Set a long random value for:

                {secretKey}
                (environment variable: {envKey})

            The secret travels in the callback URL's query string, not a header: Strava cannot send
            custom headers on its webhook POSTs, so the URL is the only channel available. Treat the
            whole callback URL as a credential — it will appear in reverse-proxy and provider access
            logs.

            After setting it you MUST re-register the existing {provider.Name} webhook subscription
            with the new callback URL:

                https://<your-host>/api/webhooks/{provider.Name}?secret=<the value you just set>

            Until the subscription is re-registered, {provider.Name} keeps posting to the old URL and
            every event is rejected.

            To turn the integration off instead, clear its credentials
            ({string.Join(", ", provider.CredentialKeys)}).
            """;
    }
}
