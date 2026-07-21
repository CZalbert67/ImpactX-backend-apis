using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/monitors")]
[Authorize]
public class MonitorsController : ControllerBase
{
    private readonly IMonitorService _monitorService;

    public MonitorsController(IMonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMonitors()
    {
        var usuarioId = GetUsuarioId();
        var monitors = await _monitorService.GetMonitorsAsync(usuarioId);
        return Ok(monitors);
    }

    [HttpPost("invite")]
    public async Task<IActionResult> Invite([FromBody] InviteMonitorRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _monitorService.InviteAsync(usuarioId, request);
        return Ok(result);
    }

    [HttpPost("{id:guid}/resend")]
    public async Task<IActionResult> ResendInvite(Guid id)
    {
        var usuarioId = GetUsuarioId();
        await _monitorService.ResendInviteAsync(usuarioId, id);
        return Ok(new { mensaje = "Invitación reenviada." });
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreMonitor(Guid id)
    {
        var usuarioId = GetUsuarioId();
        await _monitorService.RestoreMonitorAsync(usuarioId, id);
        return Ok(new { mensaje = "Monitor restaurado exitosamente." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeMonitor(Guid id)
    {
        var usuarioId = GetUsuarioId();
        await _monitorService.RevokeMonitorAsync(usuarioId, id);
        return Ok(new { mensaje = "Monitor revocado exitosamente." });
    }

    [AllowAnonymous]
    [HttpGet("invite/{token}")]
    public async Task<IActionResult> GetInvitation(string token)
    {
        var info = await _monitorService.GetInvitationByTokenAsync(token);
        return Ok(info);
    }

    [AllowAnonymous]
    [HttpPost("invite/{token}/accept")]
    public async Task<IActionResult> AcceptInvitation(string token)
    {
        var monitorUsuarioId = GetUsuarioId();
        await _monitorService.AcceptInvitationAsync(token, monitorUsuarioId);
        return Ok(new { mensaje = "Invitación aceptada." });
    }

    [AllowAnonymous]
    [HttpPost("invite/{token}/reject")]
    public async Task<IActionResult> RejectInvitation(string token)
    {
        var monitorUsuarioId = GetUsuarioId();
        await _monitorService.RejectInvitationAsync(token, monitorUsuarioId);
        return Ok(new { mensaje = "Invitación rechazada." });
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
