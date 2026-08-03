using System.Security.Claims;
using ImpactX.Core.Pagination;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        var subscription = await _planService.GetCurrentSubscriptionAsync(GetUsuarioId());
        return subscription is null
            ? Ok(new { estado = "Sin suscripción", plan = "Free" })
            : Ok(subscription);
    }

    [HttpGet("effective")]
    [HttpGet("/api/v1/subscriptions/effective")]
    [ProducesResponseType(typeof(EffectiveSubscriptionDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEffectiveSubscription(CancellationToken cancellationToken)
    {
        return Ok(await _planService.GetEffectiveSubscriptionAsync(GetUsuarioId(), cancellationToken));
    }

    [HttpGet("history")]
    [HttpGet("/api/v1/subscriptions/history")]
    public async Task<IActionResult> GetSubscriptionHistory(int? pageSize, string? continuationToken)
    {
        var page = await _planService.GetSubscriptionHistoryPagedAsync(GetUsuarioId(), pageSize, continuationToken);
        PagedResultHttp.ApplyContinuationToken(Response, page);
        return Ok(page.Items);
    }

    [HttpPost("change-plan")]
    [HttpPost("/api/v1/subscriptions/change-plan")]
    [ProducesResponseType(typeof(SuscripcionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePlan([FromBody] ChangePlanRequest request)
    {
        return Ok(await _planService.ChangePlanAsync(GetUsuarioId(), request));
    }

    [HttpPost("activate")]
    [HttpPost("/api/v1/subscriptions/activate")]
    [ProducesResponseType(typeof(SubscriptionPaymentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Activate([FromBody] ChangePlanRequest request)
    {
        return Ok(await _planService.ActivateAsync(GetUsuarioId(), request));
    }

    [HttpPost("renew")]
    [HttpPost("/api/v1/subscriptions/renew")]
    [ProducesResponseType(typeof(SubscriptionPaymentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Renew([FromBody] RenewSubscriptionRequest request)
    {
        return Ok(await _planService.RenewAsync(GetUsuarioId(), request));
    }

    [HttpPost("cancel")]
    [HttpPost("/api/v1/subscriptions/cancel")]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CancelSubscription([FromBody] CancelSubscriptionRequest? request)
    {
        return Ok(await _planService.CancelSubscriptionAsync(GetUsuarioId(), request));
    }

    [HttpGet("payments")]
    [HttpGet("/api/v1/subscriptions/payments")]
    public async Task<IActionResult> GetPayments(int? pageSize, string? continuationToken)
    {
        var page = await _planService.GetPaymentsPagedAsync(GetUsuarioId(), pageSize, continuationToken);
        PagedResultHttp.ApplyContinuationToken(Response, page);
        return Ok(page.Items);
    }

    [HttpGet("payments/{id:guid}/receipt")]
    [HttpGet("/api/payments/{id:guid}/receipt")]
    [HttpGet("/api/v1/subscriptions/payments/{id:guid}/receipt")]
    public async Task<IActionResult> GetPaymentReceipt(Guid id)
    {
        var payment = await _planService.GetPaymentReceiptAsync(id, GetUsuarioId());
        return payment is null ? NotFound(new { mensaje = "Pago no encontrado." }) : Ok(payment);
    }

    [HttpPost("expire")]
    [ApiExplorerSettings(IgnoreApi = true)]
    [Obsolete("Operación interna. La expiración se ejecuta desde procesos de backend, no por clientes.")]
    public IActionResult ExpireSubscriptions() => Forbid();

    private Guid GetUsuarioId()
        => Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
}
