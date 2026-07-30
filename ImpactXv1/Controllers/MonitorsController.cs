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
    [HttpPost("invite/details")]
    public async Task<IActionResult> GetInvitation([FromBody] InvitationTokenRequest request)
    {
        var info = await _monitorService.GetInvitationByTokenAsync(request.Token);
        return Ok(info);
    }

    [HttpPost("invite/accept")]
    public async Task<IActionResult> AcceptInvitation([FromBody] InvitationTokenRequest request)
    {
        var monitorUsuarioId = GetUsuarioId();
        await _monitorService.AcceptInvitationAsync(request.Token, monitorUsuarioId);
        return Ok(new { mensaje = "Invitación aceptada." });
    }

    [HttpPost("invite/reject")]
    public async Task<IActionResult> RejectInvitation([FromBody] InvitationTokenRequest request)
    {
        var monitorUsuarioId = GetUsuarioId();
        await _monitorService.RejectInvitationAsync(request.Token, monitorUsuarioId);
        return Ok(new { mensaje = "Invitación rechazada." });
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim == null)
            throw new UnauthorizedAccessException("Usuario no autenticado.");
        return Guid.Parse(claim.Value);
    }
}
