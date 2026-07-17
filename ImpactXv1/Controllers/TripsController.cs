using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/trips")]
[Authorize]
public class TripsController : ControllerBase
{
    private readonly IViajeService _viajeService;

    public TripsController(IViajeService viajeService)
    {
        _viajeService = viajeService;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartTripRequest request)
    {
        var usuarioId = GetUsuarioId();
        var viaje = await _viajeService.StartAsync(usuarioId, request);
        return CreatedAtAction(nameof(GetActive), null, viaje);
    }

    [HttpPost("{id:guid}/pause")]
    public async Task<IActionResult> Pause(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var result = await _viajeService.PauseAsync(usuarioId, id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var result = await _viajeService.ResumeAsync(usuarioId, id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/finish")]
    public async Task<IActionResult> Finish(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var viaje = await _viajeService.FinishAsync(usuarioId, id);
        return Ok(viaje);
    }

    [HttpPatch("{id:guid}/telemetry")]
    public async Task<IActionResult> UpdateTelemetry(Guid id, [FromBody] TelemetryUpdateRequest request)
    {
        var usuarioId = GetUsuarioId();
        var puntos = await _viajeService.UpdateTelemetryAsync(usuarioId, id, request);
        return Ok(new { sincronizados = puntos.Count, puntos });
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive()
    {
        var usuarioId = GetUsuarioId();
        var viaje = await _viajeService.GetActiveAsync(usuarioId);
        if (viaje is null)
            return Ok(new { mensaje = "No hay un viaje activo." });
        return Ok(viaje);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
