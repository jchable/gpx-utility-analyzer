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
///
/// Credentials are settable at runtime too, through the settings UI, which startup
/// validation cannot cover. <see cref="FindMisconfigurationAsync"/> exposes the same
/// rule and the same message so that save can refuse for the same reason, in the
/// same words, before the configuration is written.
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
        var misconfiguration = await FindMisconfigurationAsync(key => settings.GetAsync(key));
        if (misconfiguration is not null)
            throw new InvalidOperationException(misconfiguration);
    }

    /// <summary>
    /// The same rule, reusable before the configuration is written rather than only
    /// after it is read back at startup (issue #143). Startup validation cannot see a
    /// credential that did not exist at startup, so a credential saved at runtime
    /// through the settings UI would 401 every webhook until the next restart — and
    /// then stop that restart, long after the change that caused it.
    ///
    /// Returns the message describing the first provider that would have credentials
    /// but no webhook secret in the state <paramref name="resolve"/> describes, or
    /// <c>null</c> when every configured provider can receive webhooks. Callers get
    /// the identical text the startup refusal uses, so neither path can drift.
    /// </summary>
    public static async Task<string?> FindMisconfigurationAsync(Func<string, Task<string?>> resolve)
    {
        foreach (var provider in Providers)
        {
            var configuredWith = await FirstConfiguredCredentialAsync(resolve, provider);
            if (configuredWith is null) continue;

            var secretKey = $"Integrations:{provider.Section}:WebhookSecret";
            var secret = await resolve(secretKey);
            if (!string.IsNullOrWhiteSpace(secret)) continue;

            return BuildMessage(provider, configuredWith, secretKey);
        }

        return null;
    }

    /// <summary>
    /// A resolver over the state that would RESULT from applying <paramref name="pending"/>
    /// to what <paramref name="settings"/> already holds.
    ///
    /// A settings update only writes the keys it carries, so a client id and its
    /// webhook secret may legitimately arrive in two separate requests. Validating
    /// the request alone would reject that second request and would equally miss a
    /// credential already stored; validating the resulting state gets both right.
    /// </summary>
    public static Func<string, Task<string?>> ResolveAfterApplying(
        ISettingsService settings, IReadOnlyDictionary<string, string> pending)
        => key => pending.TryGetValue(key, out var pendingValue)
            ? Task.FromResult<string?>(pendingValue)
            : settings.GetAsync(key);

    private static async Task<string?> FirstConfiguredCredentialAsync(
        Func<string, Task<string?>> resolve, Provider provider)
    {
        foreach (var key in provider.CredentialKeys)
            if (!string.IsNullOrWhiteSpace(await resolve(key)))
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
