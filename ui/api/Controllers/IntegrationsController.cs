namespace GpxAnalyzer.Api.Controllers;

using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Services.Integrations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class IntegrationsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEnumerable<IActivityImporter> _importers;

    public IntegrationsController(AppDbContext db, IEnumerable<IActivityImporter> importers)
    {
        _db = db;
        _importers = importers;
    }

    [HttpGet]
    public async Task<ActionResult<List<IntegrationDto>>> GetIntegrations()
    {
        var integrations = await _db.Integrations.ToListAsync();
        var result = _importers.Select(importer =>
        {
            var integration = integrations.FirstOrDefault(i => i.Provider == importer.ProviderName);
            return new IntegrationDto
            {
                Provider = importer.ProviderName,
                IsConnected = integration?.IsActive ?? false,
                ExternalUserId = integration?.ExternalUserId,
                ConnectedAt = integration?.CreatedAt,
            };
        }).ToList();

        return result;
    }

    [HttpPost("{provider}/connect")]
    public async Task<ActionResult<object>> Connect(string provider)
    {
        var importer = _importers.FirstOrDefault(i => i.ProviderName == provider);
        if (importer is null) return NotFound($"Unknown provider: {provider}");

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/integrations/{provider}/callback";
        var authUrl = await importer.GetAuthorizationUrlAsync(callbackUrl);

        return Ok(new { authUrl });
    }

    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> Callback(string provider, [FromQuery] string code)
    {
        var importer = _importers.FirstOrDefault(i => i.ProviderName == provider);
        if (importer is null) return NotFound();

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/integrations/{provider}/callback";
        var tokenInfo = await importer.ExchangeCodeAsync(code, callbackUrl);

        var existing = await _db.Integrations.FirstOrDefaultAsync(i => i.Provider == provider);
        if (existing is not null)
        {
            existing.AccessToken = tokenInfo.AccessToken;
            existing.RefreshToken = tokenInfo.RefreshToken;
            existing.TokenExpiresAt = tokenInfo.ExpiresAt;
            existing.ExternalUserId = tokenInfo.ExternalUserId;
            existing.IsActive = true;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.Integrations.Add(new Integration
            {
                Id = Guid.NewGuid(),
                Provider = provider,
                AccessToken = tokenInfo.AccessToken,
                RefreshToken = tokenInfo.RefreshToken,
                TokenExpiresAt = tokenInfo.ExpiresAt,
                ExternalUserId = tokenInfo.ExternalUserId,
                IsActive = true,
            });
        }

        await _db.SaveChangesAsync();

        // Redirect back to the frontend integrations page
        return Redirect("/integrations");
    }

    [HttpDelete("{provider}")]
    public async Task<IActionResult> Disconnect(string provider)
    {
        var integration = await _db.Integrations.FirstOrDefaultAsync(i => i.Provider == provider);
        if (integration is null) return NotFound();

        _db.Integrations.Remove(integration);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
