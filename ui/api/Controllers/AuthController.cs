namespace GpxAnalyzer.Api.Controllers;

using GpxAnalyzer.Api.Auth;
using GpxAnalyzer.Api.Data;
using GpxAnalyzer.Api.Dto;
using GpxAnalyzer.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly TokenService _tokenService;
    private readonly JwtSettings _jwtSettings;
    private readonly AppDbContext _context;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        TokenService tokenService,
        JwtSettings jwtSettings,
        AppDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _jwtSettings = jwtSettings;
        _context = context;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
            return BadRequest(new { code = "MISSING_FIELDS" });

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? dto.Email : dto.DisplayName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Code).ToList();
            if (errors.Contains("DuplicateEmail") || errors.Contains("DuplicateUserName"))
                return Conflict(new { code = "EMAIL_TAKEN" });
            return BadRequest(new { code = "REGISTRATION_FAILED", errors });
        }

        // First user gets Admin role, others get User
        var userCount = await _userManager.Users.CountAsync();
        var role = userCount == 1 ? "Admin" : "User";
        await _userManager.AddToRoleAsync(user, role);

        return Ok(await GenerateAuthResponse(user, role));
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null || !user.IsActive)
            return Unauthorized(new { code = "INVALID_CREDENTIALS" });

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
            return Unauthorized(new { code = "INVALID_CREDENTIALS" });

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "User";

        return Ok(await GenerateAuthResponse(user, role));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenDto dto)
    {
        var storedToken = await _context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == dto.RefreshToken);

        if (storedToken == null || !storedToken.IsActive)
            return Unauthorized(new { code = "INVALID_REFRESH_TOKEN" });

        // Revoke the old token
        storedToken.RevokedAt = DateTime.UtcNow;
        storedToken.RevokedByIp = GetIpAddress();

        var user = storedToken.User;
        var roles = await _userManager.GetRolesAsync(user);
        var role = roles.FirstOrDefault() ?? "User";

        // Generate new tokens
        var newRefreshToken = _tokenService.GenerateRefreshToken();
        storedToken.ReplacedByToken = newRefreshToken;

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreatedByIp = GetIpAddress()
        });

        await _context.SaveChangesAsync();

        var accessToken = _tokenService.GenerateAccessToken(user, roles);

        return Ok(new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            User = new UserInfoDto
            {
                Id = user.Id.ToString(),
                Email = user.Email ?? "",
                DisplayName = user.DisplayName,
                Role = role
            }
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.GetUserId();

        // Revoke all active refresh tokens for this user
        var activeTokens = await _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedByIp = GetIpAddress();
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me()
    {
        var userId = User.GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);

        return Ok(new UserInfoDto
        {
            Id = user.Id.ToString(),
            Email = user.Email ?? "",
            DisplayName = user.DisplayName,
            Role = roles.FirstOrDefault() ?? "User"
        });
    }

    private async Task<AuthResponseDto> GenerateAuthResponse(ApplicationUser user, string role)
    {
        var roles = new List<string> { role };
        var accessToken = _tokenService.GenerateAccessToken(user, roles);
        var refreshTokenValue = _tokenService.GenerateRefreshToken();

        _context.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            CreatedByIp = GetIpAddress()
        });

        await _context.SaveChangesAsync();

        return new AuthResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes),
            User = new UserInfoDto
            {
                Id = user.Id.ToString(),
                Email = user.Email ?? "",
                DisplayName = user.DisplayName,
                Role = role
            }
        };
    }

    private string? GetIpAddress()
        => HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
}
