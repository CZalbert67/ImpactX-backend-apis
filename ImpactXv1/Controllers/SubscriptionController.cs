using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Core.Pagination;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/subscription")]
[Authorize]
[RequireClientCapability(ClientTypePolicy.Web, ClientTypePolicy.Mobile)]
public class SubscriptionController : ControllerBase
{
    private readonly IPlanService _planService;

    public SubscriptionController(IPlanService planService)
    {
        _planService = planService;
    }

    [HttpGet]
    [HttpGet("/api/v1/subscriptions")]
    public async Task<IActionResult> GetCurrentSubscription()
    {
        var usuarioId = GetUsuarioId();
        var suscripcion = await _planService.GetCurrentSubscriptionAsync(usuarioId);
        if (suscripcion is null)
            return Ok(new { estado = "Sin suscripción", plan = "Free" });
        return Ok(suscripcion);
    }

    [HttpGet("history")]
    [HttpGet("/api/v1/subscriptions/history")]
    public async Task<IActionResult> GetSubscriptionHistory(int? pageSize, string? continuationToken)
    {
        var usuarioId = GetUsuarioId();
        var page = await _planService.GetSubscriptionHistoryPagedAsync(usuarioId, pageSize, continuationToken);
        PagedResultHttp.ApplyContinuationToken(Response, page);
        return Ok(page.Items);
    }

    [HttpPost("change-plan")]
    [HttpPost("/api/v1/subscriptions/change-plan")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest request)
    {
        try
        {
            var usuarioId = GetUsuarioId();
            var result = await _planService.ChangePlanAsync(usuarioId, request);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpPost("cancel")]
    [HttpPost("/api/v1/subscriptions/cancel")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelSubscription([FromBody] CancelSubscriptionRequest? request)
    {
        try
        {
            var usuarioId = GetUsuarioId();
            var result = await _planService.CancelSubscriptionAsync(usuarioId, request);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpGet("payments")]
    [HttpGet("/api/v1/subscriptions/payments")]
    public async Task<IActionResult> GetPayments(int? pageSize, string? continuationToken)
    {
        var usuarioId = GetUsuarioId();
        var page = await _planService.GetPaymentsPagedAsync(usuarioId, pageSize, continuationToken);
        PagedResultHttp.ApplyContinuationToken(Response, page);
        return Ok(page.Items);
    }

    [HttpGet("payments/{id:guid}/receipt")]
    [HttpGet("/api/payments/{id:guid}/receipt")]
    [HttpGet("/api/v1/subscriptions/payments/{id:guid}/receipt")]
    public async Task<IActionResult> GetPaymentReceipt(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var pago = await _planService.GetPaymentReceiptAsync(id, usuarioId);
        if (pago is null)
            return NotFound(new { mensaje = "Pago no encontrado." });
        return Ok(pago);
    }

    [HttpPost("expire")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Obsolete("Operación interna. La expiración se ejecuta desde procesos de backend, no por clientes.")]
    public IActionResult ExpireSubscriptions()
    {
        return Forbid();
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
