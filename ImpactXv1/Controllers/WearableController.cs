using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prueba1.Models.DTOs;
using Prueba1.Services;

namespace Prueba1.Controllers;

[ApiController]
[Route("api/wearable")]
[Authorize]
public class WearableController : ControllerBase
{
    private readonly IWearableService _wearableService;

    public WearableController(IWearableService wearableService)
    {
        _wearableService = wearableService;
    }

    [HttpGet]
    public async Task<IActionResult> GetWearable()
    {
        var usuarioId = GetUsuarioId();
        var wearable = await _wearableService.GetWearableAsync(usuarioId);
        if (wearable is null)
            return NotFound(new { mensaje = "No hay un wearable vinculado." });
        return Ok(wearable);
    }

    [HttpPost("pair")]
    public async Task<IActionResult> Pair([FromBody] PairWearableRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _wearableService.PairAsync(usuarioId, request);
        return Ok(result);
    }

    [HttpPost("pair/confirm")]
    public async Task<IActionResult> PairConfirm([FromBody] PairConfirmRequest request)
    {
        var usuarioId = GetUsuarioId();
        var wearable = await _wearableService.PairConfirmAsync(usuarioId, request);
        return Ok(wearable);
    }

    [HttpPost("sync")]
    public async Task<IActionResult> Sync([FromBody] SyncTelemetryRequest request)
    {
        var usuarioId = GetUsuarioId();
        var puntos = await _wearableService.SyncAsync(usuarioId, request);
        return Ok(new { sincronizados = puntos.Count, puntos });
    }

    [HttpPost("calibration")]
    public async Task<IActionResult> Calibrate([FromBody] CalibrationRequest request)
    {
        var usuarioId = GetUsuarioId();
        var wearable = await _wearableService.CalibrateAsync(usuarioId, request);
        return Ok(wearable);
    }

    [HttpDelete("unlink")]
    public async Task<IActionResult> Unlink()
    {
        var usuarioId = GetUsuarioId();
        await _wearableService.UnlinkAsync(usuarioId);
        return Ok(new { mensaje = "Wearable desvinculado exitosamente." });
    }

    [HttpPut("permissions")]
    public async Task<IActionResult> UpdatePermissions([FromBody] UpdateWearablePermissionsRequest request)
    {
        var usuarioId = GetUsuarioId();
        var wearable = await _wearableService.UpdatePermissionsAsync(usuarioId, request);
        return Ok(wearable);
    }

    [HttpGet("sensors/diagnostics")]
    public async Task<IActionResult> GetSensorDiagnostics()
    {
        var usuarioId = GetUsuarioId();
        var diagnostics = await _wearableService.GetSensorDiagnosticsAsync(usuarioId);
        return Ok(diagnostics);
    }

    [HttpPatch("battery")]
    public async Task<IActionResult> UpdateBattery([FromBody] BatteryUpdateRequest request)
    {
        var usuarioId = GetUsuarioId();
        var wearable = await _wearableService.UpdateBatteryAsync(usuarioId, request);
        return Ok(wearable);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
