namespace GpxAnalyzer.Api.Services.Email;

public class NoOpEmailService : IEmailService
{
    private readonly ILogger<NoOpEmailService> _logger;

    public NoOpEmailService(ILogger<NoOpEmailService> logger) => _logger = logger;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        _logger.LogInformation("[Email NoOp] To={To} Subject={Subject}", to, subject);
        return Task.CompletedTask;
    }
}
