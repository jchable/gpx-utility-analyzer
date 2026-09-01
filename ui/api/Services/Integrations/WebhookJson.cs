namespace GpxAnalyzer.Api.Services.Integrations;

using System.Globalization;
using System.Text.Json;

/// <summary>
/// Type-guarded readers for webhook payloads.
///
/// Webhook endpoints are public and unauthenticated, so the body is attacker-shaped
/// input. <c>JsonElement.GetString()</c> and <c>GetInt64()</c> throw on a value of
/// the wrong kind, and a body that is valid JSON but wrongly typed used to escape as
/// a 500 (#132) — which also makes the provider retry the same broken event. Every
/// read here answers "no" instead of throwing, so an uninterpretable body is dropped
/// the same way an unknown owner is.
/// </summary>
internal static class WebhookJson
{
    /// <summary>
    /// Longest provider-side identifier we accept. Strava sends numbers; Garmin sends
    /// UUID-shaped strings. Anything longer is not an id we know how to use.
    /// </summary>
    private const int MaxProviderIdLength = 128;

    /// <summary>
    /// A provider's numeric activity id (Strava <c>object_id</c>, Garmin
    /// <c>activityId</c>). Both document these as JSON integers, and the value goes
    /// straight into a provider API URL, so the documented type is required.
    /// </summary>
    public static bool TryReadNumericId(JsonElement parent, string property, out string id)
    {
        id = "";
        if (!parent.TryGetProperty(property, out var element)) return false;
        if (element.ValueKind != JsonValueKind.Number) return false;
        if (!element.TryGetInt64(out var value)) return false;   // rejects 1.5, 1e40, …

        id = value.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>
    /// A provider-side account identifier (Strava <c>owner_id</c> / <c>subscription_id</c>,
    /// Garmin <c>userId</c>). Providers disagree on the JSON type — Strava sends numbers,
    /// Garmin sends opaque strings — so both are accepted and anything else is a drop.
    ///
    /// <para>
    /// The accepted value is constrained to <see cref="IsWellFormedProviderId"/>'s character
    /// set. These ids are compared against stored ones, written to the database and named in
    /// operator log lines, all from an unauthenticated body: a value carrying CR/LF could
    /// forge log entries (CodeQL <c>cs/log-forging</c>). Validating once here is worth more
    /// than escaping at each of those sites, and no real provider id needs the excluded
    /// characters.
    /// </para>
    /// </summary>
    public static string? ReadProviderId(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var element)) return null;

        var value = element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var number)
                => number.ToString(CultureInfo.InvariantCulture),
            JsonValueKind.String => element.GetString(),
            _ => null,
        };

        return IsWellFormedProviderId(value) ? value : null;
    }

    /// <summary>
    /// Digits, letters and the few separators real provider ids use. Deliberately excludes
    /// every control character, whitespace and newline.
    /// </summary>
    private static bool IsWellFormedProviderId(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxProviderIdLength) return false;

        foreach (var c in value)
        {
            var ok = char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':';
            if (!ok) return false;
        }

        return true;
    }

    /// <summary>A string field, or null when absent or of any other kind.</summary>
    public static string? ReadString(JsonElement parent, string property)
        => parent.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
