using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/settings")]
[Route("api/v1/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly ISettingsService _settingsService;

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetSettings()
    {
        var usuarioId = GetUsuarioId();
        var result = await _settingsService.GetSettingsAsync(usuarioId);
        return Ok(result);
    }

    [HttpPut]
    public async Task<IActionResult> UpdateSettings([FromBody] UpdateSettingsRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _settingsService.UpdateSettingsAsync(usuarioId, request);
        return Ok(result);
    }

    [HttpPost("2fa/setup")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Setup2Fa()
    {
        var usuarioId = GetUsuarioId();
        var result = await _settingsService.Setup2FaAsync(usuarioId);
        return Ok(result);
    }

    [HttpPost("2fa/enable")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enable2Fa([FromBody] Enable2FaRequest request)
    {
        var usuarioId = GetUsuarioId();
        await _settingsService.Enable2FaAsync(usuarioId, request);
        return Ok(new { mensaje = "2FA activado correctamente." });
    }

    [HttpDelete("2fa")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Disable2Fa([FromBody] Disable2FaRequest request)
    {
        var usuarioId = GetUsuarioId();
        await _settingsService.Disable2FaAsync(usuarioId, request);
        return Ok(new { mensaje = "2FA desactivado." });
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
