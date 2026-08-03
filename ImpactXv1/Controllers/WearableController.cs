using System.Security.Claims;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/wearable")]
[Route("api/v1/wearable")]
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

    [HttpGet("all")]
    public async Task<IActionResult> GetWearables(int? pageSize, string? continuationToken)
    {
        var usuarioId = GetUsuarioId();
        var page = await _wearableService.GetWearablesPagedAsync(usuarioId, pageSize, continuationToken);
        return Ok(page);
    }

    [HttpPost("pair")]
    [RequireClientCapability(ClientTypePolicy.Mobile)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Pair([FromBody] PairWearableRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _wearableService.PairAsync(usuarioId, request);
        return Ok(result);
    }

    [HttpPost("pair/confirm")]
    [RequireClientCapability(ClientTypePolicy.Mobile)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> PairConfirm([FromBody] PairConfirmRequest request)
    {
        var usuarioId = GetUsuarioId();
        var wearable = await _wearableService.PairConfirmAsync(usuarioId, request);
        return Ok(wearable);
    }

    /// <summary>
    /// Sincronización legacy de estado del wearable. No sustituye la ingesta
    /// de telemetría asociada a un viaje.
    /// </summary>
    [HttpPost("sync")]
    [RequireClientCapability(ClientTypePolicy.Wearable)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Sync([FromBody] SyncTelemetryRequest request)
    {
        var usuarioId = GetUsuarioId();
        var puntos = await _wearableService.SyncAsync(usuarioId, request);
        return Ok(new { sincronizados = puntos.Count, puntos });
    }

    [HttpPost("calibration")]
    [RequireClientCapability(ClientTypePolicy.Mobile, ClientTypePolicy.Wearable)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Calibrate([FromBody] CalibrationRequest request)
    {
        var usuarioId = GetUsuarioId();
        var wearable = await _wearableService.CalibrateAsync(usuarioId, request);
        return Ok(wearable);
    }

    [HttpDelete("unlink")]
    [RequireClientCapability(ClientTypePolicy.Mobile)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Unlink()
    {
        var usuarioId = GetUsuarioId();
        await _wearableService.UnlinkAsync(usuarioId);
        return Ok(new { mensaje = "Wearable desvinculado exitosamente." });
    }

    [HttpPut("permissions")]
    [RequireClientCapability(ClientTypePolicy.Mobile)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    [HttpPost("sensors/diagnostics")]
    [RequireClientCapability(ClientTypePolicy.Wearable)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReportSensorDiagnostics([FromBody] WearableDiagnosticsReportRequest request)
    {
        var usuarioId = GetUsuarioId();
        var wearable = await _wearableService.ReportDiagnosticsAsync(usuarioId, request);
        return Ok(wearable);
    }

    [HttpPatch("battery")]
    [RequireClientCapability(ClientTypePolicy.Wearable)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateBattery([FromBody] BatteryUpdateRequest request)
    {
        var usuarioId = GetUsuarioId();
        var wearable = await _wearableService.UpdateBatteryAsync(usuarioId, request);
        return Ok(wearable);
    }

    [HttpPost("heartbeat")]
    [RequireClientCapability(ClientTypePolicy.Wearable)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Heartbeat([FromBody] WearableHeartbeatRequest request)
    {
        var usuarioId = GetUsuarioId();
        var wearable = await _wearableService.RegisterHeartbeatAsync(usuarioId, request);
        return Ok(wearable);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
