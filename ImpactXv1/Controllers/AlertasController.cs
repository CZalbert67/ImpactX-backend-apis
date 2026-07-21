using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize]
public class AlertasController : ControllerBase
{
    private readonly IAlertService _alertService;

    public AlertasController(IAlertService alertService)
    {
        _alertService = alertService;
    }

    [HttpPost("detect")]
    public async Task<IActionResult> Detect([FromBody] DetectAlertRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.DetectAsync(usuarioId, request);
        return CreatedAtAction(nameof(GetStatus), new { id = result.Id }, result);
    }

    [HttpPost("sos")]
    public async Task<IActionResult> SendSos([FromBody] SosRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.SendSosAsync(usuarioId, request);
        return Ok(result);
    }

    [HttpPost("{id:guid}/confirm-ok")]
    public async Task<IActionResult> ConfirmOk(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.ConfirmOkAsync(usuarioId, id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/bypass-critical")]
    public async Task<IActionResult> BypassCritical(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.BypassCriticalAsync(usuarioId, id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.RetryAsync(usuarioId, id);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetStatus(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.GetStatusAsync(usuarioId, id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseAlertRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.CloseAsync(usuarioId, id, request);
        return Ok(result);
    }

    [HttpPost("sync-offline")]
    public async Task<IActionResult> SyncOffline([FromBody] SyncOfflineRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.SyncOfflineAsync(usuarioId, request);
        return Ok(result);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
