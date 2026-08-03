using System.Security.Claims;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Models.DTOs.Monitoring;
using ImpactX.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/v1/monitoring-relationships")]
[Authorize]
[RequireClientCapability(ClientTypePolicy.Web, ClientTypePolicy.Mobile)]
public class MonitoringRelationshipsController : ControllerBase
{
    private readonly IMonitoringRelationshipService _service;
    private readonly IAlertService _alertService;
    private readonly IIncidentService _incidentService;
    private readonly IViajeService _viajeService;
    private readonly IRutaService _rutaService;

    public MonitoringRelationshipsController(
        IMonitoringRelationshipService service,
        IAlertService alertService,
        IIncidentService incidentService,
        IViajeService viajeService,
        IRutaService rutaService)
    {
        _service = service;
        _alertService = alertService;
        _incidentService = incidentService;
        _viajeService = viajeService;
        _rutaService = rutaService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _service.GetRelationshipsAsync(GetUserId(), cancellationToken));
    }

    [HttpPost("invitations")]
    [EnableRateLimiting("monitor-invite-create")]
    [ProducesResponseType(typeof(CreateMonitoringInvitationResponse), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateInvitation(
        [FromBody] CreateMonitoringInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateInvitationAsync(GetUserId(), request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), result);
    }

    [HttpPost("invitations/accept")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Accept(
        [FromBody] AcceptMonitoringInvitationRequest request,
        CancellationToken cancellationToken)
    {
        await _service.AcceptAsync(GetUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpPost("invitations/reject")]
    [EnableRateLimiting("monitor-invitation-action")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reject(
        [FromBody] RespondMonitoringInvitationRequest request,
        CancellationToken cancellationToken)
    {
        await _service.RejectAsync(GetUserId(), request, cancellationToken);
        return NoContent();
    }

    [HttpGet("{publicRelationshipId}/alerts")]
    public async Task<IActionResult> GetAuthorizedAlerts(
        string publicRelationshipId,
        int? pageSize,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        var monitoredUserId = await _service.ResolveAuthorizedMonitoredUserIdAsync(
            GetUserId(),
            publicRelationshipId,
            MonitoringResourcePermission.CriticalAlerts,
            cancellationToken);
        return Ok(await _alertService.GetAlertsPagedAsync(
            monitoredUserId,
            pageSize,
            continuationToken));
    }

    [HttpGet("{publicRelationshipId}/incidents")]
    public async Task<IActionResult> GetAuthorizedIncidents(
        string publicRelationshipId,
        [FromQuery] IncidentFilterRequest filter,
        CancellationToken cancellationToken)
    {
        var monitoredUserId = await _service.ResolveAuthorizedMonitoredUserIdAsync(
            GetUserId(),
            publicRelationshipId,
            MonitoringResourcePermission.Incidents,
            cancellationToken);
        return Ok(await _incidentService.GetIncidentsAsync(monitoredUserId, filter));
    }

    [HttpGet("{publicRelationshipId}/routes/frequent")]
    public async Task<IActionResult> GetAuthorizedFrequentRoutes(
        string publicRelationshipId,
        int? pageSize,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        var monitoredUserId = await _service.ResolveAuthorizedMonitoredUserIdAsync(
            GetUserId(),
            publicRelationshipId,
            MonitoringResourcePermission.Routes,
            cancellationToken);
        return Ok(await _rutaService.GetFrequentPagedAsync(
            monitoredUserId,
            pageSize,
            continuationToken));
    }

    [HttpGet("{publicRelationshipId}/routes/history")]
    public async Task<IActionResult> GetAuthorizedRouteHistory(
        string publicRelationshipId,
        int? pageSize,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        var monitoredUserId = await _service.ResolveAuthorizedMonitoredUserIdAsync(
            GetUserId(),
            publicRelationshipId,
            MonitoringResourcePermission.Routes,
            cancellationToken);
        return Ok(await _rutaService.GetHistoryPagedAsync(
            monitoredUserId,
            pageSize,
            continuationToken));
    }

    [HttpGet("{publicRelationshipId}/trips")]
    public async Task<IActionResult> GetAuthorizedTrips(
        string publicRelationshipId,
        int? pageSize,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        var monitoredUserId = await _service.ResolveAuthorizedMonitoredUserIdAsync(
            GetUserId(),
            publicRelationshipId,
            MonitoringResourcePermission.Routes,
            cancellationToken);
        return Ok(await _viajeService.GetTripsPagedAsync(
            monitoredUserId,
            pageSize,
            continuationToken));
    }

    [HttpGet("{publicRelationshipId}/trips/{tripId:guid}/telemetry")]
    public async Task<IActionResult> GetAuthorizedTelemetry(
        string publicRelationshipId,
        Guid tripId,
        int? pageSize,
        string? continuationToken,
        CancellationToken cancellationToken)
    {
        var monitoredUserId = await _service.ResolveAuthorizedMonitoredUserIdAsync(
            GetUserId(),
            publicRelationshipId,
            MonitoringResourcePermission.Telemetry,
            cancellationToken);
        return Ok(await _viajeService.GetTelemetryPagedAsync(
            monitoredUserId,
            tripId,
            pageSize,
            continuationToken));
    }

    [HttpGet("{publicRelationshipId}/medical-profile")]
    public async Task<IActionResult> GetAuthorizedMedicalProfile(
        string publicRelationshipId,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.GetAuthorizedMedicalProfileAsync(
            GetUserId(),
            publicRelationshipId,
            cancellationToken));
    }

    [HttpPatch("{publicRelationshipId}/permissions")]
    public async Task<IActionResult> UpdatePermissions(
        string publicRelationshipId,
        [FromBody] UpdateMonitoringPermissionsRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _service.UpdatePermissionsAsync(
            GetUserId(),
            publicRelationshipId,
            request,
            cancellationToken));
    }

    [HttpPost("{publicRelationshipId}/block")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Block(
        string publicRelationshipId,
        CancellationToken cancellationToken)
    {
        await _service.BlockAsync(GetUserId(), publicRelationshipId, cancellationToken);
        return NoContent();
    }

    [HttpDelete("{publicRelationshipId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke(
        string publicRelationshipId,
        CancellationToken cancellationToken)
    {
        await _service.RevokeAsync(GetUserId(), publicRelationshipId, cancellationToken);
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
