using System.Security.Claims;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs.QuickMessages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/v1/quick-messages")]
[Authorize]
[RequireClientCapability(ClientTypePolicy.Web, ClientTypePolicy.Mobile)]
public class QuickMessagesController : ControllerBase
{
    private readonly IQuickMessageService _service;

    public QuickMessagesController(IQuickMessageService service)
    {
        _service = service;
    }

    [HttpGet("templates")]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetTemplatesAsync(GetUserId(), cancellationToken));
    }

    [HttpPost("templates")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(typeof(QuickMessageTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateTemplate(
        [FromBody] UpsertQuickMessageTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateTemplateAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetTemplates), result);
    }

    [HttpPut("templates/{publicTemplateId}")]
    [EnableRateLimiting("monitor-invitation-action")]
    public async Task<IActionResult> UpdateTemplate(
        string publicTemplateId,
        [FromBody] UpsertQuickMessageTemplateRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpdateTemplateAsync(
            GetUserId(),
            publicTemplateId,
            request,
            cancellationToken));
    }

    [HttpDelete("templates/{publicTemplateId}")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteTemplate(
        string publicTemplateId,
        CancellationToken cancellationToken)
    {
        await _service.DeleteTemplateAsync(GetUserId(), publicTemplateId, cancellationToken);
        return NoContent();
    }

    [HttpPost("send")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(typeof(QuickMessageDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Send(
        [FromBody] SendQuickMessageRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.SendAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetHistory), result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetHistory(
        string? otherPublicProfileId,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetHistoryAsync(
            GetUserId(),
            otherPublicProfileId,
            cancellationToken));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken)
    {
        var count = await _service.GetUnreadCountAsync(GetUserId(), cancellationToken);
        return Ok(new { unreadCount = count });
    }

    [HttpPatch("{publicMessageId}/read")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(
        string publicMessageId,
        CancellationToken cancellationToken)
    {
        await _service.MarkReadAsync(GetUserId(), publicMessageId, cancellationToken);
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
