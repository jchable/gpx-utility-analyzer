namespace GpxAnalyzer.Api.Controllers;

using System.Security.Cryptography;
using GpxAnalyzer.Api.Auth;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Entities;
using GpxAnalyzer.Api.Services.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class IntegrationsController : ControllerBase
{
    private const string StatePurpose = "integrations.oauth.state.v1";
    private static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly IEnumerable<IActivityImporter> _importers;
    private readonly IDataProtectionProvider _dataProtection;

    public IntegrationsController(
        AppDbContext db,
        IEnumerable<IActivityImporter> importers,
        IDataProtectionProvider dataProtection)
    {
        _db = db;
        _importers = importers;
        _dataProtection = dataProtection;
    }

    [HttpGet]
    public async Task<ActionResult<List<IntegrationDto>>> GetIntegrations()
    {
        var userId = User.GetUserId();
        var integrations = await _db.Integrations.Where(i => i.UserId == userId).ToListAsync();
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
        if (importer is null) return NotFound(new { code = "UNKNOWN_PROVIDER" });

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/integrations/{provider}/callback";

        // Bind the flow to the caller: the callback arrives as a browser navigation
        // with no Authorization header, so the user id has to travel in `state`.
        var nonce = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var userId = User.GetUserId();
        _db.OAuthStates.Add(new OAuthState
        {
            Nonce = nonce,
            UserId = userId,
            Provider = provider,
            ExpiresAt = DateTime.UtcNow.Add(StateLifetime),
        });
        await _db.SaveChangesAsync();
        var protector = _dataProtection.CreateProtector(StatePurpose);
        var state = protector.Protect(
            $"{userId}|{provider}|{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}|{nonce}");

        var authUrl = await importer.GetAuthorizationUrlAsync(callbackUrl, state);

        return Ok(new { authUrl });
    }

    [HttpGet("{provider}/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback(
        string provider,
        [FromQuery] string? code = null,
        [FromQuery] string? state = null,
        [FromQuery] string? oauth_token = null,
        [FromQuery] string? oauth_verifier = null)
    {
        var importer = _importers.FirstOrDefault(i => i.ProviderName == provider);
        if (importer is null) return NotFound();

        var stateData = TryReadState(state, provider);
        if (stateData is null)
            return BadRequest(new { code = "INVALID_OAUTH_STATE" });

        var (userId, nonce) = stateData.Value;
        // Consume before exchanging the provider code. A callback can be retried,
        // but the signed state must never authorize a second account binding.
        var consumed = await _db.OAuthStates
            .Where(s => s.Nonce == nonce && s.UserId == userId &&
                s.Provider == provider && s.ExpiresAt >= DateTime.UtcNow)
            .ExecuteDeleteAsync();
        if (consumed != 1)
            return BadRequest(new { code = "INVALID_OAUTH_STATE" });

        // Build the exchange code: OAuth 2 uses "code", OAuth 1.0a uses "oauth_token|oauth_verifier"
        var exchangeCode = !string.IsNullOrEmpty(code)
            ? code
            : !string.IsNullOrEmpty(oauth_token) && !string.IsNullOrEmpty(oauth_verifier)
                ? $"{oauth_token}|{oauth_verifier}"
                : null;

        if (exchangeCode is null) return BadRequest(new { code = "MISSING_OAUTH_PARAMS" });

        var callbackUrl = $"{Request.Scheme}://{Request.Host}/api/integrations/{provider}/callback";
        var tokenInfo = await importer.ExchangeCodeAsync(exchangeCode, callbackUrl);

        var existing = await _db.Integrations.FirstOrDefaultAsync(i => i.UserId == userId && i.Provider == provider);
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
                UserId = userId,
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

    private (Guid UserId, string Nonce)? TryReadState(string? state, string provider)
    {
        if (string.IsNullOrEmpty(state)) return null;

        string plain;
        try { plain = _dataProtection.CreateProtector(StatePurpose).Unprotect(state); }
        catch (CryptographicException) { return null; }

        var parts = plain.Split('|');
        if (parts.Length != 4) return null;
        if (!Guid.TryParse(parts[0], out var userId)) return null;
        if (!string.Equals(parts[1], provider, StringComparison.Ordinal)) return null;
        if (!long.TryParse(parts[2], out var issuedAt)) return null;
        if (parts[3].Length != 64) return null;

        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(issuedAt);
        return age >= TimeSpan.Zero && age <= StateLifetime
            ? (userId, parts[3])
            : null;
    }

    [HttpDelete("{provider}")]
    public async Task<IActionResult> Disconnect(string provider)
    {
        var integration = await _db.Integrations.FirstOrDefaultAsync(i => i.UserId == User.GetUserId() && i.Provider == provider);
        if (integration is null) return NotFound();

        _db.Integrations.Remove(integration);
        await _db.SaveChangesAsync();

        return NoContent();
    }
}
