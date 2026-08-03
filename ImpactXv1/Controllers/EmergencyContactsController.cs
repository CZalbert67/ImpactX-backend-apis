using System.Security.Claims;
using ImpactX.Core.Pagination;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/v1/contacts")]
[Authorize]
[RequireClientCapability(ClientTypePolicy.Web, ClientTypePolicy.Mobile)]
public class EmergencyContactsController : ControllerBase
{
    private readonly IEmergencyContactService _service;

    public EmergencyContactsController(IEmergencyContactService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        int? pageSize,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        var page = await _service.GetContactsPagedAsync(
            GetUserId(),
            pageSize,
            continuationToken,
            cancellationToken);
        PagedResultHttp.ApplyContinuationToken(Response, page);
        return Ok(page.Items);
    }

    [HttpGet("sync")]
    public async Task<IActionResult> GetSync(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetSyncAsync(GetUserId(), cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(
        string id,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetByPublicIdAsync(GetUserId(), id, cancellationToken));
    }

    [HttpPost("invitations")]
    [EnableRateLimiting("monitor-invite-create")]
    [ProducesResponseType(typeof(CreateEmergencyContactInvitationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateInvitation(
        [FromBody] CreateEmergencyContactInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateInvitationAsync(
            GetUserId(),
            request,
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Contact.PublicContactId }, result);
    }

    [HttpPost("invitations/accept")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] RespondEmergencyContactInvitationRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AcceptInvitationAsync(GetUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpPost("invitations/reject")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectInvitation(
        [FromBody] RespondEmergencyContactInvitationRequest request,
        CancellationToken cancellationToken)
    {
        await _service.RejectInvitationAsync(GetUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpPatch("{id}")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateEmergencyContactRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpdateAsync(GetUserId(), id, request, cancellationToken));
    }

    [HttpPatch("{id}/primary")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MakePrimary(
        string id,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.MakePrimaryAsync(GetUserId(), id, cancellationToken));
    }

    [HttpPost("{id}/block")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Block(
        string id,
        CancellationToken cancellationToken)
    {
        await _service.BlockAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Revoke(
        string id,
        CancellationToken cancellationToken)
    {
        await _service.RevokeAsync(GetUserId(), id, cancellationToken);
        return NoContent();
    }

    private Guid GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var userId))
        {
            throw new UnauthorizedAccessException("Usuario no autenticado.");
        }

        return userId;
    }
}
