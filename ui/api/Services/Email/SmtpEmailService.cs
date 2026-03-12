namespace GpxAnalyzer.Api.Services.Email;

using MailKit.Net.Smtp;
using MimeKit;

public class SmtpEmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<SmtpEmailService> _logger;

    public SmtpEmailService(IConfiguration config, ILogger<SmtpEmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        var smtp = _config.GetSection("Email:Smtp");
        var host = smtp["Host"] ?? "localhost";
        var port = int.TryParse(smtp["Port"], out var p) ? p : 587;
        var useSsl = bool.TryParse(smtp["UseSsl"], out var ssl) && ssl;
        var username = smtp["Username"] ?? "";
        var password = smtp["Password"] ?? "";
        var fromEmail = smtp["From"] ?? "noreply@gpx-analyzer.app";
        var fromName = smtp["FromName"] ?? "GPX Analyzer";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(fromName, fromEmail));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(host, port, useSsl, ct);
        if (!string.IsNullOrEmpty(username))
            await client.AuthenticateAsync(username, password, ct);
        await client.SendAsync(message, ct);
        await client.DisconnectAsync(true, ct);

        _logger.LogInformation("[Email] Sent to={To} subject={Subject}", to, subject);
    }
}
