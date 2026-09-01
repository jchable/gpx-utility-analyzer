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
    /// An account identifier (Strava <c>owner_id</c> / <c>subscription_id</c>, Garmin
    /// <c>userId</c>). Providers disagree here — Strava sends numbers, Garmin sends
    /// opaque strings — so both are accepted and anything else is a drop.
    /// </summary>
    public static string? ReadAccountId(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var element)) return null;

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetInt64(out var value)
                => value.ToString(CultureInfo.InvariantCulture),
            JsonValueKind.String => element.GetString(),
            _ => null,
        };
    }

    /// <summary>A string field, or null when absent or of any other kind.</summary>
    public static string? ReadString(JsonElement parent, string property)
        => parent.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : null;
}
