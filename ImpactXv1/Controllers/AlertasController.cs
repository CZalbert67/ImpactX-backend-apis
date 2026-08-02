using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ImpactX.Core.Pagination;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/alerts")]
[Route("api/v1/alerts")]
[Authorize]
public class AlertasController : ControllerBase
{
    private readonly IAlertService _alertService;

    public AlertasController(IAlertService alertService)
    {
        _alertService = alertService;
    }

    [HttpPost("detect")]
    [RequireClientCapability(ClientTypePolicy.Mobile, ClientTypePolicy.Wearable)]
    [EnableRateLimiting("alert-detect")]
    public async Task<IActionResult> Detect([FromBody] DetectAlertRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.DetectAsync(usuarioId, request);
        return CreatedAtAction(nameof(GetStatus), new { id = result.Id }, result);
    }

    [HttpPost("sos")]
    [RequireClientCapability(ClientTypePolicy.Mobile, ClientTypePolicy.Wearable)]
    [EnableRateLimiting("alert-sos")]
    public async Task<IActionResult> SendSos([FromBody] SosRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.SendSosAsync(usuarioId, request);
        return Ok(result);
    }

    [HttpPost("{id:guid}/confirm-ok")]
    [RequireClientCapability(ClientTypePolicy.Mobile, ClientTypePolicy.Wearable)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConfirmOk(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.ConfirmOkAsync(usuarioId, id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/bypass-critical")]
    [RequireClientCapability(ClientTypePolicy.Mobile, ClientTypePolicy.Wearable)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> BypassCritical(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.BypassCriticalAsync(usuarioId, id);
        return Ok(result);
    }

    [HttpPost("{id:guid}/retry")]
    [RequireClientCapability(ClientTypePolicy.Mobile, ClientTypePolicy.Wearable)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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

    [HttpGet]
    public async Task<IActionResult> GetAlerts(int? pageSize, string? continuationToken)
    {
        var usuarioId = GetUsuarioId();
        var page = await _alertService.GetAlertsPagedAsync(usuarioId, pageSize, continuationToken);
        return Ok(page);
    }

    [HttpPost("{id:guid}/close")]
    [RequireClientCapability(ClientTypePolicy.Mobile, ClientTypePolicy.Wearable)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Close(Guid id, [FromBody] CloseAlertRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _alertService.CloseAsync(usuarioId, id, request);
        return Ok(result);
    }

    [HttpPost("sync-offline")]
    [RequireClientCapability(ClientTypePolicy.Mobile, ClientTypePolicy.Wearable)]
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
