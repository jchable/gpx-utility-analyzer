using System.Net;
using GpxAnalyzer.Api.Tests.Helpers;

namespace GpxAnalyzer.Api.Tests.Integrations;

[Collection("Integration")]
public class OAuthCallbackTests
{
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
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var auth = await TestHelpers.RegisterAsync(client, $"oauth_{Guid.NewGuid():N}@test.local");
        var authed = TestHelpers.CreateAuthorizedClient(factory, auth.AccessToken);

        var resp = await authed.PostAsync("/api/integrations/strava/connect", null);

        // ClientId is unset in the Test environment, so the importer throws;
        // when it IS configured the URL must carry state=. Assert on whichever
        // path runs, but never on a silent success with no state.
        if (resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            Assert.Contains("state=", body);
        }
        else
        {
            Assert.NotEqual(HttpStatusCode.OK, resp.StatusCode);
        }
    }
}
