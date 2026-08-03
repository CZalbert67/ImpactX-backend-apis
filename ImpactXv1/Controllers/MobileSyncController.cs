using System.Security.Claims;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/v1/mobile/sync")]
[Authorize]
[RequireClientCapability(ClientTypePolicy.Mobile)]
public sealed class MobileSyncController : ControllerBase
{
    private readonly IMobileSyncService _service;

    public MobileSyncController(IMobileSyncService service)
    {
        _service = service;
    }

    [HttpGet("bootstrap")]
    [ProducesResponseType(typeof(MobileSyncSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetBootstrap(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetBootstrapAsync(GetUserId(), cancellationToken));
    }

    [HttpGet("changes")]
    [ProducesResponseType(typeof(MobileSyncChangesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChanges(
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetChangesAsync(GetUserId(), cursor, cancellationToken));
    }

    [HttpPost("push")]
    [ProducesResponseType(typeof(MobileSyncPushResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Push(
        [FromBody] MobileSyncPushRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.PushAsync(GetUserId(), request, cancellationToken));
    }

    [HttpPost("ack")]
    [ProducesResponseType(typeof(MobileSyncAckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Acknowledge(
        [FromBody] MobileSyncAckRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.AcknowledgeAsync(GetUserId(), request, cancellationToken));
    }

    private Guid GetUserId()
    {
        return Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
    }
}
