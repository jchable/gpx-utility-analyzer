namespace GpxAiAnalyzer.Tests.Providers;

using GpxAiAnalyzer.Core.Providers;

public class GeminiProviderTests
{
    private readonly GeminiProvider _provider = new();

    [Fact]
    public void Name_ReturnsGemini()
    {
        Assert.Equal("gemini", _provider.Name);
    }

    [Fact]
    public void CreateClient_NullApiKey_NoEnvVar_Throws()
    {
        // Temporarily clear env vars to ensure they don't interfere
        var savedGemini = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var savedGoogle = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
            Environment.SetEnvironmentVariable("GOOGLE_API_KEY", null);

            var options = new ProviderOptions { ApiKey = null };
            var ex = Assert.Throws<InvalidOperationException>(() => _provider.CreateClient(options));
            Assert.Contains("API key required", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", savedGemini);
            Environment.SetEnvironmentVariable("GOOGLE_API_KEY", savedGoogle);
        }
    }

    [Fact]
    public void CreateClient_EmptyStringApiKey_NoEnvVar_Throws()
    {
        var savedGemini = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var savedGoogle = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
            Environment.SetEnvironmentVariable("GOOGLE_API_KEY", null);

            // This was the bug: empty string was not caught by ?? operator
            var options = new ProviderOptions { ApiKey = "" };
            var ex = Assert.Throws<InvalidOperationException>(() => _provider.CreateClient(options));
            Assert.Contains("API key required", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", savedGemini);
            Environment.SetEnvironmentVariable("GOOGLE_API_KEY", savedGoogle);
        }
    }

    [Fact]
    public void CreateClient_WhitespaceApiKey_NoEnvVar_Throws()
    {
        var savedGemini = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        var savedGoogle = Environment.GetEnvironmentVariable("GOOGLE_API_KEY");
        try
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
            Environment.SetEnvironmentVariable("GOOGLE_API_KEY", null);

            var options = new ProviderOptions { ApiKey = "   " };
            var ex = Assert.Throws<InvalidOperationException>(() => _provider.CreateClient(options));
            Assert.Contains("API key required", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", savedGemini);
            Environment.SetEnvironmentVariable("GOOGLE_API_KEY", savedGoogle);
        }
    }

    [Fact]
    public void CreateClient_ValidApiKey_ReturnsClient()
    {
        var options = new ProviderOptions { ApiKey = "fake-key-for-test" };
        var client = _provider.CreateClient(options);
        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_EmptyEndpoint_UsesDefault_NoException()
    {
        // Before the fix, this would throw UriFormatException: "Invalid URI: The URI is empty."
        var options = new ProviderOptions { ApiKey = "fake-key", Endpoint = "" };
        var client = _provider.CreateClient(options);
        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_NullEndpoint_UsesDefault_NoException()
    {
        var options = new ProviderOptions { ApiKey = "fake-key", Endpoint = null };
        var client = _provider.CreateClient(options);
        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_EmptyModel_UsesDefault_NoException()
    {
        var options = new ProviderOptions { ApiKey = "fake-key", Model = "" };
        var client = _provider.CreateClient(options);
        Assert.NotNull(client);
    }

    [Fact]
    public void CreateClient_CustomEndpoint_NoException()
    {
        var options = new ProviderOptions
        {
            ApiKey = "fake-key",
            Endpoint = "https://custom-endpoint.example.com/v1/"
        };
        var client = _provider.CreateClient(options);
        Assert.NotNull(client);
    }
}
