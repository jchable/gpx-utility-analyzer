using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GpxAnalyzer.Api.Tests.Helpers;

// ─── Response records ────────────────────────────────────────────────────────

public record AuthResponse(
    [property: JsonPropertyName("accessToken")]  string AccessToken,
    [property: JsonPropertyName("refreshToken")] string RefreshToken,
    [property: JsonPropertyName("expiresAt")]    string ExpiresAt,
    [property: JsonPropertyName("user")]         UserInfo User);

public record UserInfo(
    [property: JsonPropertyName("id")]          string Id,
    [property: JsonPropertyName("email")]       string Email,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("role")]        string Role);

// ─── Helpers ─────────────────────────────────────────────────────────────────

public static class TestHelpers
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Register a new user and return the auth response.</summary>
    public static async Task<AuthResponse> RegisterAsync(
        HttpClient client, string email, string password = "Test12345", string displayName = "Test User")
    {
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password, displayName });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts))!;
    }

    /// <summary>Login with existing credentials and return the auth response.</summary>
    public static async Task<AuthResponse> LoginAsync(
        HttpClient client, string email, string password = "Test12345")
    {
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email, password });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts))!;
    }

    /// <summary>Create an HttpClient pre-configured with a Bearer token.</summary>
    public static HttpClient CreateAuthorizedClient(ApiFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    /// <summary>Upload the test GPX fixture and return the created activity id.</summary>
    public static async Task<string> UploadTestGpxAsync(HttpClient authorizedClient, string activityType = "trail")
    {
        var gpxPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "test.gpx");
        await using var fs = File.OpenRead(gpxPath);

        using var form = new MultipartFormDataContent();
        form.Add(new StreamContent(fs), "file", "test.gpx");
        form.Add(new StringContent(activityType), "activityType");

        var resp = await authorizedClient.PostAsync("/api/activities/upload", form);
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>(JsonOpts);
        return json!.RootElement.GetProperty("id").GetString()!;
    }

    /// <summary>
    /// Poll the activity endpoint until status is no longer Pending / Analyzing / AiProcessing,
    /// or until the timeout elapses.  Returns the final status string.
    /// </summary>
    public static async Task<string> WaitForProcessingAsync(
        HttpClient authorizedClient, string activityId, int maxWaitMs = 15_000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(maxWaitMs);
        while (DateTime.UtcNow < deadline)
        {
            var resp = await authorizedClient.GetAsync($"/api/activities/{activityId}");
            if (!resp.IsSuccessStatusCode) return "NotFound";

            var json = await resp.Content.ReadFromJsonAsync<JsonDocument>(JsonOpts);
            var status = json!.RootElement.GetProperty("status").GetString() ?? "";

            if (status is not ("Pending" or "Analyzing" or "AiProcessing"))
                return status;

            await Task.Delay(300);
        }
        return "Timeout";
    }
}
