using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/analytics")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly IAnalyticsService _analyticsService;

    public AnalyticsController(IAnalyticsService analyticsService)
    {
        _analyticsService = analyticsService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var usuarioId = GetUsuarioId();
        var result = await _analyticsService.GetDashboardAsync(usuarioId);
        return Ok(result);
    }

    [HttpGet("incidents/trend")]
    public async Task<IActionResult> GetIncidentTrend([FromQuery] TrendFilterRequest filter)
    {
        var usuarioId = GetUsuarioId();
        var result = await _analyticsService.GetIncidentTrendAsync(usuarioId, filter);
        return Ok(result);
    }

    [HttpGet("trips/summary")]
    public async Task<IActionResult> GetTripSummary()
    {
        var usuarioId = GetUsuarioId();
        var result = await _analyticsService.GetTripSummaryAsync(usuarioId);
        return Ok(result);
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return Guid.Parse(claim!.Value);
    }
}
