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
[Authorize]
[Route("api/profile")]
public class ProfileController(UserManager<ApplicationUser> userManager, AppDbContext db) : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly AppDbContext _db = db;

    [HttpGet]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.GetUserId();
        var user = await _userManager.Users
            .Include(u => u.AthleteProfile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return NotFound();

        return Ok(MapToDto(user));
    }

    [HttpPut]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var userId = User.GetUserId();
        var user = await _userManager.Users
            .Include(u => u.AthleteProfile)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null) return NotFound();

        // Update user fields
        if (dto.DisplayName is not null) user.DisplayName = dto.DisplayName;
        if (dto.Bio is not null) user.Bio = dto.Bio;
        if (dto.City is not null) user.City = dto.City;
        if (dto.PreferredUnits is not null) user.PreferredUnits = dto.PreferredUnits;
        if (dto.Language is not null) user.Language = dto.Language;
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        // Upsert AthleteProfile
        var profile = user.AthleteProfile;
        if (profile is null)
        {
            profile = new AthleteProfile { UserId = userId };
            _db.AthleteProfiles.Add(profile);
        }

        if (dto.WeightKg.HasValue) profile.WeightKg = dto.WeightKg;
        if (dto.HeightCm.HasValue) profile.HeightCm = dto.HeightCm;
        if (dto.Sex is not null) profile.Sex = dto.Sex;
        if (dto.DateOfBirth.HasValue) profile.DateOfBirth = dto.DateOfBirth;
        if (dto.MaxHeartRate.HasValue) profile.MaxHeartRate = dto.MaxHeartRate;
        if (dto.RestingHeartRate.HasValue) profile.RestingHeartRate = dto.RestingHeartRate;
        if (dto.Ftp.HasValue) profile.Ftp = dto.Ftp;
        if (dto.Vo2Max.HasValue) profile.Vo2Max = dto.Vo2Max;
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Reload to get updated navigation
        user.AthleteProfile = profile;
        return Ok(MapToDto(user));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.CurrentPassword) || string.IsNullOrWhiteSpace(dto.NewPassword))
            return BadRequest(new { code = "MISSING_FIELDS" });

        var userId = User.GetUserId();
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Code).ToList();
            if (errors.Any(e => e.Contains("Password") || e.Contains("Incorrect")))
                return BadRequest(new { code = "WRONG_PASSWORD" });
            return BadRequest(new { code = "PASSWORD_CHANGE_FAILED", errors });
        }

        return NoContent();
    }

    private static UserProfileDto MapToDto(ApplicationUser user)
    {
        var p = user.AthleteProfile;
        return new UserProfileDto
        {
            Id = user.Id.ToString(),
            Email = user.Email ?? "",
            DisplayName = user.DisplayName,
            Bio = user.Bio,
            City = user.City,
            PreferredUnits = user.PreferredUnits,
            Language = user.Language,
            ProfilePhotoPath = user.ProfilePhotoPath,

            WeightKg = p?.WeightKg,
            HeightCm = p?.HeightCm,
            Sex = p?.Sex,
            DateOfBirth = p?.DateOfBirth,
            MaxHeartRate = p?.MaxHeartRate,
            RestingHeartRate = p?.RestingHeartRate,
            Ftp = p?.Ftp,
            Vo2Max = p?.Vo2Max,

            Age = p?.Age,
            EstimatedMaxHR = p?.EstimatedMaxHR,
            Bmi = p?.Bmi,
        };
    }
}
