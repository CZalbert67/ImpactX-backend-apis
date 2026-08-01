using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/monitors")]
[Route("api/v1/monitors")]
[Authorize]
public class MonitorsController : ControllerBase
{
    private readonly IMonitorService _monitorService;

    public MonitorsController(IMonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMonitors(int? pageSize, string? continuationToken)
    {
        var usuarioId = GetUsuarioId();
        var page = await _monitorService.GetMonitorsPagedAsync(usuarioId, pageSize, continuationToken);
        PagedResultHttp.ApplyContinuationToken(Response, page);
        return Ok(page.Items);
    }

    [HttpPost("invite")]
    [EnableRateLimiting("monitor-invite-create")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Invite([FromBody] InviteMonitorRequest request)
    {
        var usuarioId = GetUsuarioId();
        var result = await _monitorService.InviteAsync(usuarioId, request);
        return Ok(result);
    }

    [HttpPost("{id:guid}/resend")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResendInvite(Guid id)
    {
        var usuarioId = GetUsuarioId();
        await _monitorService.ResendInviteAsync(usuarioId, id);
        return Ok(new { mensaje = "Invitación reenviada." });
    }

    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RestoreMonitor(Guid id)
    {
        var usuarioId = GetUsuarioId();
        await _monitorService.RestoreMonitorAsync(usuarioId, id);
        return Ok(new { mensaje = "Monitor restaurado exitosamente." });
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RevokeMonitor(Guid id)
    {
        var usuarioId = GetUsuarioId();
        await _monitorService.RevokeMonitorAsync(usuarioId, id);
        return Ok(new { mensaje = "Monitor revocado exitosamente." });
    }

    [AllowAnonymous]
    [HttpPost("invite/details")]
    [EnableRateLimiting("monitor-invite-details")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GetInvitation([FromBody] InvitationTokenRequest request)
    {
        var info = await _monitorService.GetInvitationByTokenAsync(request.Token);
        return Ok(info);
    }

    [HttpPost("invite/accept")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptInvitation([FromBody] InvitationTokenRequest request)
    {
        var monitorUsuarioId = GetUsuarioId();
        await _monitorService.AcceptInvitationAsync(request.Token, monitorUsuarioId);
        return Ok(new { mensaje = "Invitación aceptada." });
    }

    [HttpPost("invite/reject")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
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
