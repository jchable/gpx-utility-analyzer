namespace GpxAnalyzer.Api.Dto;

/// <summary>User-scoped settings (analysis preferences).</summary>
public class AppSettingsDto
{
    public AnalysisSettingsDto Analysis { get; set; } = new();
}

/// <summary>Global settings (admin only): AI provider + integration credentials.</summary>
public class GlobalSettingsDto
{
    public AiProviderSettingsDto AiProvider { get; set; } = new();
    public IntegrationCredentialsDto Integrations { get; set; } = new();
}

public class AnalysisSettingsDto
{
    public string Preset { get; set; } = "trail";
    public string Smoothing { get; set; } = "medium";
    public string TrackSmoothing { get; set; } = "medium";
    public string ElevationAlgorithm { get; set; } = "threshold";
    public bool FixAnomalies { get; set; }
    public bool AutoDetectActivityType { get; set; }
}

public class AiProviderSettingsDto
{
    public string Name { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public bool HasApiKey { get; set; }
    public string Model { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public List<string> AvailableProviders { get; set; } = [];
}

public class IntegrationCredentialsDto
{
    public StravaCredentialsDto Strava { get; set; } = new();
    public GarminCredentialsDto Garmin { get; set; } = new();
}

public class StravaCredentialsDto
{
    public string ClientId { get; set; } = "";
    public bool HasClientSecret { get; set; }
    public string ClientSecret { get; set; } = "";

    /// <summary>
    /// Required once the provider has credentials: without it every inbound webhook
    /// is rejected with 401 and imports stop silently. Like the client secret it is
    /// reported as a boolean and never echoed back — it travels in the callback
    /// URL's query string, which makes it a credential in its own right.
    /// </summary>
    public bool HasWebhookSecret { get; set; }

    public string WebhookSecret { get; set; } = "";
}

public class GarminCredentialsDto
{
    public string ConsumerKey { get; set; } = "";
    public bool HasConsumerSecret { get; set; }
    public string ConsumerSecret { get; set; } = "";

    /// <inheritdoc cref="StravaCredentialsDto.HasWebhookSecret"/>
    public bool HasWebhookSecret { get; set; }

    public string WebhookSecret { get; set; } = "";
}
