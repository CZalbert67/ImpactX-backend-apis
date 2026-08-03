using System.Security.Claims;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/incidents")]
[Route("api/v1/incidents")]
[Authorize]
[RequireClientCapability(ClientTypePolicy.Web, ClientTypePolicy.Mobile)]
public class IncidentesController : ControllerBase
{
    private readonly IIncidentService _incidentService;

    public IncidentesController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetIncidents([FromQuery] IncidentFilterRequest filter)
        => Ok(await _incidentService.GetIncidentsAsync(GetUsuarioId(), filter));

    [HttpGet("active")]
    [ProducesResponseType(typeof(List<IncidenteListItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveIncidents()
        => Ok(await _incidentService.GetActiveIncidentsAsync(GetUsuarioId()));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetIncidentDetail(Guid id)
        => Ok(await _incidentService.GetIncidentDetailAsync(GetUsuarioId(), id));

    [HttpPost("{id:guid}/confirm-ok")]
    [RequireClientCapability(ClientTypePolicy.Mobile)]
    [ProducesResponseType(typeof(IncidentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmOk(Guid id)
        => Ok(await _incidentService.ConfirmOkAsync(GetUsuarioId(), id));

    [HttpPost("{id:guid}/close")]
    [ProducesResponseType(typeof(IncidentActionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(Guid id, [FromBody] IncidentCloseRequest request)
        => Ok(await _incidentService.CloseAsync(GetUsuarioId(), id, request));

    [HttpPatch("{id:guid}/mark-false-alarm")]
    public async Task<IActionResult> MarkFalseAlarm(Guid id, [FromBody] MarkFalseAlarmRequest request)
    {
        await _incidentService.MarkAsFalseAlarmAsync(GetUsuarioId(), id, request);
        return Ok(new { mensaje = "Incidente marcado como falsa alarma." });
    }

    [HttpPatch("{id:guid}/note")]
    public async Task<IActionResult> UpdateNote(Guid id, [FromBody] NoteRequest request)
    {
        await _incidentService.UpdateNoteAsync(GetUsuarioId(), id, request);
        return Ok(new { mensaje = "Nota actualizada." });
    }

    [HttpGet("{id:guid}/map")]
    public async Task<IActionResult> GetMapData(Guid id)
        => Ok(await _incidentService.GetMapDataAsync(GetUsuarioId(), id));

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string formato = "csv")
    {
        var data = await _incidentService.ExportAsync(GetUsuarioId(), formato);
        var csv = string.Equals(formato, "csv", StringComparison.OrdinalIgnoreCase);
        return File(data, csv ? "text/csv" : "text/plain", csv ? "incidentes.csv" : "incidentes.txt");
    }

    private Guid GetUsuarioId()
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
