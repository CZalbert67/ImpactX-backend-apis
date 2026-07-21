using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/routes")]
[Authorize]
public class RoutesController : ControllerBase
{
    private readonly IRutaService _rutaService;

    public RoutesController(IRutaService rutaService)
    {
        _rutaService = rutaService;
    }

    [HttpGet("frequent")]
    public async Task<IActionResult> GetFrequent()
    {
        var usuarioId = GetUsuarioId();
        var rutas = await _rutaService.GetFrequentAsync(usuarioId);
        return Ok(rutas);
    }

    [HttpPost("frequent")]
    public async Task<IActionResult> CreateFrequent([FromBody] CreateRutaRequest request)
    {
        var usuarioId = GetUsuarioId();
        var ruta = await _rutaService.CreateAsync(usuarioId, request);
        return CreatedAtAction(nameof(GetFrequent), new { id = ruta.Id }, ruta);
    }

    [HttpPut("frequent/{id:guid}")]
    public async Task<IActionResult> UpdateFrequent(Guid id, [FromBody] UpdateRutaRequest request)
    {
        var usuarioId = GetUsuarioId();
        var ruta = await _rutaService.UpdateAsync(usuarioId, id, request);
        return Ok(ruta);
    }

    [HttpDelete("frequent/{id:guid}")]
    public async Task<IActionResult> DeleteFrequent(Guid id)
    {
        var usuarioId = GetUsuarioId();
        await _rutaService.DeleteAsync(usuarioId, id);
        return Ok(new { mensaje = "Ruta eliminada." });
    }

    [HttpPatch("select-today")]
    public async Task<IActionResult> SelectToday([FromBody] SelectTodayRequest request)
    {
        var usuarioId = GetUsuarioId();
        var ruta = await _rutaService.SelectTodayAsync(usuarioId, request);
        return Ok(ruta);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory()
    {
        var usuarioId = GetUsuarioId();
        var rutas = await _rutaService.GetHistoryAsync(usuarioId);
        return Ok(rutas);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
