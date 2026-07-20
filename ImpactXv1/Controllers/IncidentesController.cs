using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/incidents")]
[Authorize]
public class IncidentesController : ControllerBase
{
    private readonly IIncidentService _incidentService;

    public IncidentesController(IIncidentService incidentService)
    {
        _incidentService = incidentService;
    }

    [HttpGet]
    public async Task<IActionResult> GetIncidents([FromQuery] IncidentFilterRequest filter)
    {
        var usuarioId = GetUsuarioId();
        var result = await _incidentService.GetIncidentsAsync(usuarioId, filter);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetIncidentDetail(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var result = await _incidentService.GetIncidentDetailAsync(usuarioId, id);
        return Ok(result);
    }

    [HttpPatch("{id:guid}/mark-false-alarm")]
    public async Task<IActionResult> MarkFalseAlarm(Guid id, [FromBody] MarkFalseAlarmRequest request)
    {
        var usuarioId = GetUsuarioId();
        await _incidentService.MarkAsFalseAlarmAsync(usuarioId, id, request);
        return Ok(new { mensaje = "Incidente marcado como falsa alarma." });
    }

    [HttpPatch("{id:guid}/note")]
    public async Task<IActionResult> UpdateNote(Guid id, [FromBody] NoteRequest request)
    {
        var usuarioId = GetUsuarioId();
        await _incidentService.UpdateNoteAsync(usuarioId, id, request);
        return Ok(new { mensaje = "Nota actualizada." });
    }

    [HttpGet("{id:guid}/map")]
    public async Task<IActionResult> GetMapData(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var result = await _incidentService.GetMapDataAsync(usuarioId, id);
        return Ok(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string formato = "pdf")
    {
        var usuarioId = GetUsuarioId();
        var data = await _incidentService.ExportAsync(usuarioId, formato);

        var contentType = formato.ToLower() == "csv"
            ? "text/csv"
            : "application/pdf";

        var fileName = formato.ToLower() == "csv"
            ? "incidentes.csv"
            : "incidentes.txt";

        return File(data, contentType, fileName);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
