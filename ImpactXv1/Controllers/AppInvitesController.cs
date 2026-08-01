using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/v1/invites")]
[Authorize]
public class AppInvitesController : ControllerBase
{
    private readonly IAppInviteService _appInviteService;

    public AppInvitesController(IAppInviteService appInviteService)
    {
        _appInviteService = appInviteService;
    }

    [HttpGet("")]
    public async Task<IActionResult> GetInvites()
    {
        var usuarioId = GetUsuarioId();
        var invites = await _appInviteService.GetInvitesAsync(usuarioId);
        return Ok(invites);
    }

    [HttpPost("")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInvite([FromBody] CreateAppInviteRequest request)
    {
        var usuarioId = GetUsuarioId();
        var invite = await _appInviteService.CreateAsync(usuarioId, request);
        return CreatedAtAction(nameof(GetInvites), new { id = invite.Id }, invite);
    }

    [HttpGet("by-token/{token}")]
    public async Task<IActionResult> GetInviteByToken(string token)
    {
        var invite = await _appInviteService.GetByTokenAsync(token);
        return Ok(invite);
    }

    [HttpPost("accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptInvite([FromBody] AcceptAppInviteRequest request)
    {
        var usuarioId = GetUsuarioId();
        var invite = await _appInviteService.AcceptAsync(usuarioId, request);
        return Ok(invite);
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelInvite(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var invite = await _appInviteService.CancelAsync(usuarioId, id);
        return Ok(invite);
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteInvite(Guid id)
    {
        var usuarioId = GetUsuarioId();
        await _appInviteService.DeleteAsync(usuarioId, id);
        return NoContent();
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
