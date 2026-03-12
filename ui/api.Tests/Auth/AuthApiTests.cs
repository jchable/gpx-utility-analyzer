using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GpxAnalyzer.Api.Tests.Helpers;

namespace GpxAnalyzer.Api.Tests.Auth;

/// <summary>
/// Integration tests for the authentication endpoints.
/// Each test gets its own ApiFactory (and therefore its own isolated SQLite DB)
/// because xUnit creates a new test class instance per [Fact].
/// </summary>
public class AuthApiTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    // ─── Registration ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Register_FirstUser_GetsAdminRole()
    {
        var result = await TestHelpers.RegisterAsync(_client, "admin@test.com", displayName: "Admin User");

        Assert.Equal(HttpStatusCode.OK, HttpStatusCode.OK); // implicitly checked by EnsureSuccessStatusCode
        Assert.Equal("Admin", result.User.Role);
        Assert.Equal("admin@test.com", result.User.Email);
        Assert.Equal("Admin User", result.User.DisplayName);
        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
    }

    [Fact]
    public async Task Register_SecondUser_GetsUserRole()
    {
        await TestHelpers.RegisterAsync(_client, "admin@test.com");  // first → Admin
        var result = await TestHelpers.RegisterAsync(_client, "user@test.com", displayName: "Regular User");

        Assert.Equal("User", result.User.Role);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        await TestHelpers.RegisterAsync(_client, "duplicate@test.com");

        var resp = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = "duplicate@test.com", password = "Test12345", displayName = "Copy" });

        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("EMAIL_TAKEN", json!.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Register_MissingFields_Returns400()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = "", password = "", displayName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    // ─── Login ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        await TestHelpers.RegisterAsync(_client, "alice@test.com", "Alice12345");

        var result = await TestHelpers.LoginAsync(_client, "alice@test.com", "Alice12345");

        Assert.NotEmpty(result.AccessToken);
        Assert.NotEmpty(result.RefreshToken);
        Assert.Equal("alice@test.com", result.User.Email);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        await TestHelpers.RegisterAsync(_client, "alice@test.com", "Alice12345");

        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "alice@test.com", password = "WrongPassword" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("INVALID_CREDENTIALS", json!.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@test.com", password = "Test12345" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ─── Protected endpoints ──────────────────────────────────────────────────

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/activities?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidToken_Returns401()
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");

        var resp = await _client.GetAsync("/api/activities?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_Returns200()
    {
        var auth = await TestHelpers.RegisterAsync(_client, "alice@test.com");
        var authClient = TestHelpers.CreateAuthorizedClient(_factory, auth.AccessToken);

        var resp = await authClient.GetAsync("/api/activities?page=1&pageSize=20");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    // ─── /me endpoint ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUser()
    {
        var auth = await TestHelpers.RegisterAsync(_client, "alice@test.com", displayName: "Alice Runner");
        var authClient = TestHelpers.CreateAuthorizedClient(_factory, auth.AccessToken);

        var resp = await authClient.GetAsync("/api/auth/me");
        resp.EnsureSuccessStatusCode();

        var json = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        Assert.Equal("alice@test.com", json!.RootElement.GetProperty("email").GetString());
        Assert.Equal("Alice Runner", json.RootElement.GetProperty("displayName").GetString());
        Assert.NotEmpty(json.RootElement.GetProperty("id").GetString()!);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var resp = await _client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    // ─── Refresh token ────────────────────────────────────────────────────────

    [Fact]
    public async Task RefreshToken_ValidToken_ReturnsNewTokenPair()
    {
        var auth = await TestHelpers.RegisterAsync(_client, "alice@test.com");

        var resp = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = auth.RefreshToken });
        resp.EnsureSuccessStatusCode();

        var result = await resp.Content.ReadFromJsonAsync<JsonDocument>();
        var newAccess = result!.RootElement.GetProperty("accessToken").GetString();
        var newRefresh = result.RootElement.GetProperty("refreshToken").GetString();

        Assert.NotEmpty(newAccess!);
        Assert.NotEmpty(newRefresh!);
        // Both tokens are rotated: refresh is cryptographically random, access has a unique jti claim
        Assert.NotEqual(auth.RefreshToken, newRefresh);
        Assert.NotEqual(auth.AccessToken, newAccess);
    }

    [Fact]
    public async Task RefreshToken_InvalidToken_Returns401()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = "invalid-refresh-token" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task RefreshToken_UsedTwice_SecondCallReturns401()
    {
        var auth = await TestHelpers.RegisterAsync(_client, "alice@test.com");

        // First refresh — OK
        var first = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = auth.RefreshToken });
        first.EnsureSuccessStatusCode();

        // Second refresh with the original token — must be revoked
        var second = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = auth.RefreshToken });

        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    // ─── Logout ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var auth = await TestHelpers.RegisterAsync(_client, "alice@test.com");
        var authClient = TestHelpers.CreateAuthorizedClient(_factory, auth.AccessToken);

        // Logout
        var logoutResp = await authClient.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logoutResp.StatusCode);

        // Refreshing with the old token must now fail
        var refreshResp = await _client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = auth.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResp.StatusCode);
    }
}
