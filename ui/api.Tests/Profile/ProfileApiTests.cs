using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GpxAnalyzer.Api.Tests.Helpers;

namespace GpxAnalyzer.Api.Tests.Profile;

/// <summary>
/// Integration tests for GET/PUT /api/profile and POST /api/profile/change-password.
/// Each test class instance gets a fresh isolated SQLite database.
/// </summary>
[Collection("Integration")]
public class ProfileApiTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        var anon = _factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(anon, "alice@test.com", displayName: "Alice Runner");
        _client = TestHelpers.CreateAuthorizedClient(_factory, auth.AccessToken);
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    // ─── GET /api/profile ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetProfile_Authenticated_ReturnsProfile()
    {
        var resp = await _client.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("alice@test.com", json!.RootElement.GetProperty("email").GetString());
        Assert.Equal("Alice Runner", json.RootElement.GetProperty("displayName").GetString());
    }

    [Fact]
    public async Task GetProfile_Unauthenticated_Returns401()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.GetAsync("/api/profile");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetProfile_BiometricFieldsNullByDefault()
    {
        var resp = await _client.GetAsync("/api/profile");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var root = json!.RootElement;

        Assert.Equal(JsonValueKind.Null, root.GetProperty("weightKg").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("heightCm").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("maxHeartRate").ValueKind);
    }

    // ─── PUT /api/profile ──────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateProfile_ValidData_PersistsChanges()
    {
        var payload = new
        {
            displayName = "Alice Updated",
            bio = "Trail runner",
            city = "Chamonix",
            weightKg = 62.5,
            heightCm = 168.0,
            sex = "female",
            maxHeartRate = 185,
            restingHeartRate = 52,
            ftp = 250,
            vo2Max = 55.0,
            preferredUnits = "metric",
        };

        var resp = await _client.PutAsJsonAsync("/api/profile", payload);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var root = json!.RootElement;

        Assert.Equal("Alice Updated", root.GetProperty("displayName").GetString());
        Assert.Equal("Trail runner", root.GetProperty("bio").GetString());
        Assert.Equal("Chamonix", root.GetProperty("city").GetString());
        Assert.Equal(62.5, root.GetProperty("weightKg").GetDouble());
        Assert.Equal(168.0, root.GetProperty("heightCm").GetDouble());
        Assert.Equal("female", root.GetProperty("sex").GetString());
        Assert.Equal(185, root.GetProperty("maxHeartRate").GetInt32());
        Assert.Equal(52, root.GetProperty("restingHeartRate").GetInt32());
        Assert.Equal(250, root.GetProperty("ftp").GetInt32());
        Assert.Equal(55.0, root.GetProperty("vo2Max").GetDouble());
    }

    [Fact]
    public async Task UpdateProfile_PartialUpdate_OnlyChangesProvided()
    {
        // Set initial values
        await _client.PutAsJsonAsync("/api/profile", new { displayName = "Alice", city = "Paris" });

        // Partial update: only city
        var resp = await _client.PutAsJsonAsync("/api/profile", new { city = "Lyon" });

        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal("Lyon", json!.RootElement.GetProperty("city").GetString());
        // displayName unchanged (null patch = no change)
    }

    [Fact]
    public async Task UpdateProfile_ComputedFieldsReturned()
    {
        // Set enough data for computed fields (age, bmi, estimatedMaxHR)
        await _client.PutAsJsonAsync("/api/profile", new
        {
            dateOfBirth = "1990-05-15",
            weightKg = 70.0,
            heightCm = 175.0,
        });

        var resp = await _client.GetAsync("/api/profile");
        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var root = json!.RootElement;

        // Age should be computed and > 0
        var age = root.GetProperty("age");
        Assert.NotEqual(JsonValueKind.Null, age.ValueKind);
        Assert.True(age.GetInt32() > 0);

        // BMI should be computed
        var bmi = root.GetProperty("bmi");
        Assert.NotEqual(JsonValueKind.Null, bmi.ValueKind);
        Assert.True(bmi.GetDouble() > 0);
    }

    [Fact]
    public async Task UpdateProfile_Unauthenticated_Returns401()
    {
        var anon = _factory.CreateClient();
        var resp = await anon.PutAsJsonAsync("/api/profile", new { displayName = "Hack" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ─── POST /api/profile/change-password ────────────────────────────────────

    [Fact]
    public async Task ChangePassword_ValidCredentials_Returns204()
    {
        var resp = await _client.PostAsJsonAsync("/api/profile/change-password", new
        {
            currentPassword = "Test12345",
            newPassword = "NewPass9876",
        });

        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/profile/change-password", new
        {
            currentPassword = "WrongPassword",
            newPassword = "NewPass9876",
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("WRONG_PASSWORD", json!.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChangePassword_MissingFields_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/profile/change-password", new
        {
            currentPassword = "",
            newPassword = "",
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("MISSING_FIELDS", json!.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ChangePassword_AfterChange_OldPasswordNoLongerWorks()
    {
        // Change password
        await _client.PostAsJsonAsync("/api/profile/change-password", new
        {
            currentPassword = "Test12345",
            newPassword = "NewPass9876",
        });

        // Try logging in with old password → should fail
        var anon = _factory.CreateClient();
        var loginResp = await anon.PostAsJsonAsync("/api/auth/login", new
        {
            email = "alice@test.com",
            password = "Test12345",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, loginResp.StatusCode);
    }

    [Fact]
    public async Task ChangePassword_AfterChange_NewPasswordWorks()
    {
        // Change password
        await _client.PostAsJsonAsync("/api/profile/change-password", new
        {
            currentPassword = "Test12345",
            newPassword = "NewPass9876",
        });

        // Login with new password → should succeed
        var anon = _factory.CreateClient();
        var loginResp = await anon.PostAsJsonAsync("/api/auth/login", new
        {
            email = "alice@test.com",
            password = "NewPass9876",
        });

        Assert.Equal(HttpStatusCode.OK, loginResp.StatusCode);
    }

    // ─── Isolation: profile data is per-user ──────────────────────────────────

    [Fact]
    public async Task Profile_DataIsIsolatedPerUser()
    {
        var anon = _factory.CreateClient();
        var bobAuth = await TestHelpers.RegisterAsync(anon, "bob@test.com", displayName: "Bob Hiker");
        var bobClient = TestHelpers.CreateAuthorizedClient(_factory, bobAuth.AccessToken);

        // Alice updates her profile
        await _client.PutAsJsonAsync("/api/profile", new { city = "Chamonix", weightKg = 62.0 });

        // Bob sees his own (default) profile
        var bobResp = await bobClient.GetAsync("/api/profile");
        var bobJson = await bobResp.Content.ReadFromJsonAsync<JsonDocument>();

        Assert.Equal("Bob Hiker", bobJson!.RootElement.GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, bobJson.RootElement.GetProperty("weightKg").ValueKind);
    }
}
