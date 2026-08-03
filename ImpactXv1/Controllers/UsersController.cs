using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
[RequireClientCapability(ClientTypePolicy.Web, ClientTypePolicy.Mobile)]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    [HttpGet("/api/v1/profile")]
    public async Task<IActionResult> GetProfile()
    {
        var usuarioId = GetUsuarioId();
        var profile = await _userService.GetProfileAsync(usuarioId);
        return Ok(profile);
    }

    [HttpPut("me")]
    [HttpPut("/api/v1/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileRequest request)
    {
        var usuarioId = GetUsuarioId();
        var profile = await _userService.UpdateProfileAsync(usuarioId, request);
        return Ok(profile);
    }

    [HttpGet("me/preferences")]
    [HttpGet("/api/v1/profile/preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        var usuarioId = GetUsuarioId();
        var preferences = await _userService.GetPreferencesAsync(usuarioId);
        return Ok(preferences);
    }

    [HttpPut("me/preferences")]
    [HttpPut("/api/v1/profile/preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateUserPreferencesRequest request)
    {
        var usuarioId = GetUsuarioId();
        var preferences = await _userService.UpdatePreferencesAsync(usuarioId, request);
        return Ok(preferences);
    }

    [HttpGet("driver-profile")]
    [HttpGet("/api/v1/profile/driver")]
    public async Task<IActionResult> GetDriverProfile()
    {
        var usuarioId = GetUsuarioId();
        var profile = await _userService.GetDriverProfileAsync(usuarioId);
        return Ok(profile);
    }

    [HttpPut("driver-profile")]
    [HttpPut("/api/v1/profile/driver")]
    public async Task<IActionResult> UpdateDriverProfile([FromBody] UpdateDriverProfileRequest request)
    {
        var usuarioId = GetUsuarioId();
        var profile = await _userService.UpdateDriverProfileAsync(usuarioId, request);
        return Ok(profile);
    }

    [HttpGet("driver-profile/medical")]
    [HttpGet("/api/v1/profile/medical")]
    public async Task<IActionResult> GetMedicalProfile()
    {
        var usuarioId = GetUsuarioId();
        var medical = await _userService.GetMedicalProfileAsync(usuarioId);
        return Ok(medical);
    }

    [HttpPut("driver-profile/medical")]
    [HttpPut("/api/v1/profile/medical")]
    public async Task<IActionResult> UpdateMedicalProfile([FromBody] UpdateMedicalProfileRequest request)
    {
        var usuarioId = GetUsuarioId();
        var medical = await _userService.UpdateMedicalProfileAsync(usuarioId, request);
        return Ok(medical);
    }

    [HttpPut("me/fcm-token")]
    [EnableRateLimiting("fcm-token")]
    public async Task<IActionResult> UpdateFcmToken([FromBody] UpdateFcmTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            return BadRequest(new { mensaje = "El token FCM es obligatorio." });

        if (request.Token.Length > 1000)
            return BadRequest(new { mensaje = "El token FCM no puede exceder los 1000 caracteres." });

        var usuarioId = GetUsuarioId();
        await _userService.UpdateFcmTokenAsync(usuarioId, request);
        return NoContent();
    }

    [HttpDelete("me/fcm-token")]
    [EnableRateLimiting("fcm-token")]
    public async Task<IActionResult> DeleteFcmToken()
    {
        var usuarioId = GetUsuarioId();
        await _userService.DeleteFcmTokenAsync(usuarioId);
        return NoContent();
    }

    [HttpGet("search")]
    [HttpGet("/api/v1/profile/search")]
    [EnableRateLimiting("monitor-invite-details")]
    public async Task<IActionResult> SearchUsers([FromQuery] string q, [FromQuery] string? by = null)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<UserSearchResultDto>());

        var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? excludeId = usuarioIdClaim is not null ? Guid.Parse(usuarioIdClaim) : null;

        var results = await _userService.SearchUsersAsync(q, by, excludeId);
        return Ok(results);
    }

    [HttpPost("me/onboarding/legal-acceptance")]
    [HttpPost("/api/v1/profile/onboarding/legal-acceptance")]
    public async Task<IActionResult> AcceptLegalDocuments([FromBody] AcceptLegalDocumentsRequest request)
    {
        var usuarioId = GetUsuarioId();
        var onboarding = await _userService.AcceptLegalDocumentsAsync(usuarioId, request);
        return Ok(onboarding);
    }

    [HttpGet("me/username")]
    [HttpGet("/api/v1/profile/username")]
    public async Task<IActionResult> GetUsername()
    {
        var usuarioId = GetUsuarioId();
        var profile = await _userService.GetProfileAsync(usuarioId);
        return Ok(new { profile.PublicProfileId, profile.Username });
    }

    [HttpPut("me/username")]
    [HttpPut("/api/v1/profile/username")]
    public async Task<IActionResult> UpdateUsername([FromBody] UpdateUsernameRequest request)
    {
        var usuarioId = GetUsuarioId();
        var profile = await _userService.UpdateUsernameAsync(usuarioId, request);
        return Ok(profile);
    }

    [HttpGet("me/onboarding")]
    [HttpGet("/api/v1/profile/onboarding")]
    public async Task<IActionResult> GetOnboarding()
    {
        var usuarioId = GetUsuarioId();
        var onboarding = await _userService.GetOnboardingAsync(usuarioId);
        return Ok(onboarding);
    }

    [HttpPut("me/onboarding")]
    [HttpPut("/api/v1/profile/onboarding")]
    public async Task<IActionResult> UpdateOnboarding([FromBody] UpdateOnboardingRequest request)
    {
        var usuarioId = GetUsuarioId();
        var onboarding = await _userService.UpdateOnboardingAsync(usuarioId, request);
        return Ok(onboarding);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
