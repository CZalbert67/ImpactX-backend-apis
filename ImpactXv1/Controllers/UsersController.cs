using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetProfile()
    {
        var usuarioId = GetUsuarioId();
        var profile = await _userService.GetProfileAsync(usuarioId);
        return Ok(profile);
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateUserProfileRequest request)
    {
        var usuarioId = GetUsuarioId();
        var profile = await _userService.UpdateProfileAsync(usuarioId, request);
        return Ok(profile);
    }

    [HttpGet("me/preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        var usuarioId = GetUsuarioId();
        var preferences = await _userService.GetPreferencesAsync(usuarioId);
        return Ok(preferences);
    }

    [HttpPut("me/preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateUserPreferencesRequest request)
    {
        var usuarioId = GetUsuarioId();
        var preferences = await _userService.UpdatePreferencesAsync(usuarioId, request);
        return Ok(preferences);
    }

    [HttpGet("driver-profile")]
    public async Task<IActionResult> GetDriverProfile()
    {
        var usuarioId = GetUsuarioId();
        var profile = await _userService.GetDriverProfileAsync(usuarioId);
        return Ok(profile);
    }

    [HttpPut("driver-profile")]
    public async Task<IActionResult> UpdateDriverProfile([FromBody] UpdateDriverProfileRequest request)
    {
        var usuarioId = GetUsuarioId();
        var profile = await _userService.UpdateDriverProfileAsync(usuarioId, request);
        return Ok(profile);
    }

    [HttpGet("driver-profile/medical")]
    public async Task<IActionResult> GetMedicalProfile()
    {
        var usuarioId = GetUsuarioId();
        var medical = await _userService.GetMedicalProfileAsync(usuarioId);
        return Ok(medical);
    }

    [HttpPut("driver-profile/medical")]
    public async Task<IActionResult> UpdateMedicalProfile([FromBody] UpdateMedicalProfileRequest request)
    {
        var usuarioId = GetUsuarioId();
        var medical = await _userService.UpdateMedicalProfileAsync(usuarioId, request);
        return Ok(medical);
    }

    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return Ok(Array.Empty<UserSearchResultDto>());

        var usuarioIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        Guid? excludeId = usuarioIdClaim is not null ? Guid.Parse(usuarioIdClaim) : null;

        var results = await _userService.SearchUsersAsync(q, excludeId);
        return Ok(results);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
