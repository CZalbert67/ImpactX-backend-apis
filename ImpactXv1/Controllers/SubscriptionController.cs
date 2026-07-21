using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/subscription")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly IPlanService _planService;

    public SubscriptionController(IPlanService planService)
    {
        _planService = planService;
    }

    [HttpGet]
    public async Task<IActionResult> GetCurrentSubscription()
    {
        var usuarioId = GetUsuarioId();
        var suscripcion = await _planService.GetCurrentSubscriptionAsync(usuarioId);
        if (suscripcion is null)
            return Ok(new { estado = "Sin suscripción", plan = "Free" });
        return Ok(suscripcion);
    }

    [HttpGet("history")]
    public async Task<IActionResult> GetSubscriptionHistory()
    {
        var usuarioId = GetUsuarioId();
        var historial = await _planService.GetSubscriptionHistoryAsync(usuarioId);
        return Ok(historial);
    }

    [HttpPost("change-plan")]
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
    public async Task<IActionResult> GetPayments()
    {
        var usuarioId = GetUsuarioId();
        var pagos = await _planService.GetPaymentsAsync(usuarioId);
        return Ok(pagos);
    }

    [HttpGet("payments/{id:guid}/receipt")]
    [HttpGet("/api/payments/{id:guid}/receipt")]
    public async Task<IActionResult> GetPaymentReceipt(Guid id)
    {
        var usuarioId = GetUsuarioId();
        var pago = await _planService.GetPaymentReceiptAsync(id, usuarioId);
        if (pago is null)
            return NotFound(new { mensaje = "Pago no encontrado." });
        return Ok(pago);
    }

    [HttpPost("expire")]
    public async Task<IActionResult> ExpireSubscriptions()
    {
        var count = await _planService.ExpireSubscriptionsAsync();
        return Ok(new { expiradas = count });
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
