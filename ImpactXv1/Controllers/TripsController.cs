using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/trips")]
[Route("api/v1/trips")]
[Authorize]
public class TripsController : ControllerBase
{
    private readonly IViajeService _viajeService;

    public TripsController(IViajeService viajeService)
    {
        _viajeService = viajeService;
    }

    [HttpPost("start")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start([FromBody] StartTripRequest request)
    {
        var usuarioId = GetUsuarioId();
        var viaje = await _viajeService.StartAsync(usuarioId, request);
        return CreatedAtAction(nameof(GetActive), null, viaje);
    }

    [HttpPost("{id:guid}/pause")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Pause(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var result = await _viajeService.PauseAsync(usuarioId, id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/resume")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resume(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var result = await _viajeService.ResumeAsync(usuarioId, id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/finish")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Finish(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var viaje = await _viajeService.FinishAsync(usuarioId, id);
        return Ok(viaje);
    }

    [HttpPatch("{id:guid}/telemetry")]
    [EnableRateLimiting("telemetry-ingestion")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateTelemetry(Guid id, [FromBody] TelemetryUpdateRequest request)
    {
        var usuarioId = GetUsuarioId();
        var puntos = await _viajeService.UpdateTelemetryAsync(usuarioId, id, request);
        return Ok(new { sincronizados = puntos.Count, puntos });
    }

    [HttpPost("{id:guid}/telemetry")]
    [EnableRateLimiting("telemetry-ingestion")]
    [RequestSizeLimit(ImpactX.Core.Telemetry.TelemetryIngestionLimits.MaxBodyBytes)]
    [ProducesResponseType(typeof(TelemetryIngestionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> IngestTelemetry(Guid id, [FromBody] TelemetryBatchRequest request, CancellationToken cancellationToken)
    {
        var usuarioId = GetUsuarioId();
        var result = await _viajeService.IngestTelemetryAsync(usuarioId, id, request, cancellationToken);
        return Ok(result);
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

    [HttpGet]
    public async Task<IActionResult> GetTrips(int? pageSize, string? continuationToken)
    {
        var usuarioId = GetUsuarioId();
        var page = await _viajeService.GetTripsPagedAsync(usuarioId, pageSize, continuationToken);
        return Ok(page);
    }

    [HttpGet("{id:guid}/telemetry")]
    public async Task<IActionResult> GetTelemetry(Guid id, int? pageSize, string? continuationToken)
    {
        var usuarioId = GetUsuarioId();
        var page = await _viajeService.GetTelemetryPagedAsync(usuarioId, id, pageSize, continuationToken);
        return Ok(page);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
