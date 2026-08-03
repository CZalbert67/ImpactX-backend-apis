using System.Security.Claims;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs.FamilySubscriptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/v1/family-subscriptions")]
[Authorize]
[RequireClientCapability(ClientTypePolicy.Web, ClientTypePolicy.Mobile)]
public class FamilySubscriptionsController : ControllerBase
{
    private readonly IFamilySubscriptionService _service;

    public FamilySubscriptionsController(IFamilySubscriptionService service)
    {
        _service = service;
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await _service.GetCurrentAsync(GetUserId(), cancellationToken);
        return result is null ? NoContent() : Ok(result);
    }

    [HttpPost("activate")]
    [ProducesResponseType(typeof(FamilySubscriptionSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activate(
        [FromBody] ActivateFamilySubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.ActivateAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetCurrent), result);
    }

    [HttpPost("change-plan")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePlan(
        [FromBody] ChangeFamilyPlanRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.ChangePlanAsync(GetUserId(), request, cancellationToken));
    }

    [HttpPost("renew")]
    public async Task<IActionResult> Renew(CancellationToken cancellationToken)
    {
        return Ok(await _service.RenewAsync(GetUserId(), cancellationToken));
    }

    [HttpPost("cancel")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(CancellationToken cancellationToken)
    {
        await _service.CancelAsync(GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("members")]
    public async Task<IActionResult> GetMembers(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetMembersAsync(GetUserId(), cancellationToken));
    }

    [HttpDelete("members/{publicMembershipId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveMember(
        string publicMembershipId,
        CancellationToken cancellationToken)
    {
        await _service.RemoveMemberAsync(GetUserId(), publicMembershipId, cancellationToken);
        return NoContent();
    }

    [HttpPost("leave")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Leave(CancellationToken cancellationToken)
    {
        await _service.LeaveAsync(GetUserId(), cancellationToken);
        return NoContent();
    }

    [HttpGet("invitations")]
    public async Task<IActionResult> GetInvitations(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetInvitationsAsync(GetUserId(), cancellationToken));
    }

    [HttpGet("invitations/incoming")]
    public async Task<IActionResult> GetIncomingInvitations(
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetIncomingInvitationsAsync(
            GetUserId(),
            cancellationToken));
    }

    [HttpPost("invitations")]
    [EnableRateLimiting("monitor-invite-create")]
    [ProducesResponseType(typeof(CreateFamilyInvitationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateInvitation(
        [FromBody] CreateFamilyInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateInvitationAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetInvitations), result);
    }

    [HttpPost("invitations/{publicInvitationId}/accept")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AcceptInvitation(
        string publicInvitationId,
        CancellationToken cancellationToken)
    {
        await _service.AcceptInvitationAsync(GetUserId(), publicInvitationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("invitations/{publicInvitationId}/reject")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RejectInvitation(
        string publicInvitationId,
        CancellationToken cancellationToken)
    {
        await _service.RejectInvitationAsync(GetUserId(), publicInvitationId, cancellationToken);
        return NoContent();
    }

    [HttpPost("invitations/redeem")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RedeemInvitation(
        [FromBody] RedeemFamilyInvitationRequest request,
        CancellationToken cancellationToken)
    {
        await _service.RedeemInvitationCodeAsync(GetUserId(), request, cancellationToken);
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
