using System.Security.Claims;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs.Vehicles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/v1/vehicles")]
[Authorize]
[RequireClientCapability(ClientTypePolicy.Web, ClientTypePolicy.Mobile)]
public class VehiclesController : ControllerBase
{
    private readonly IVehicleService _vehicleService;

    public VehiclesController(IVehicleService vehicleService)
    {
        _vehicleService = vehicleService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<VehicleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var vehicles = await _vehicleService.GetVehiclesAsync(
            GetUsuarioId(),
            cancellationToken);
        return Ok(vehicles);
    }

    [HttpGet("types")]
    [ProducesResponseType(typeof(VehicleTypeCatalogDto), StatusCodes.Status200OK)]
    public IActionResult GetTypes()
    {
        return Ok(new VehicleTypeCatalogDto
        {
            TipoVehiculo = Enum.GetNames<TipoVehiculo>(),
            UsoPrincipal = Enum.GetNames<UsoPrincipalVehiculo>()
        });
    }

    [HttpGet("{publicVehicleId}")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        string publicVehicleId,
        CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleService.GetVehicleAsync(
            GetUsuarioId(),
            publicVehicleId,
            cancellationToken);
        return Ok(vehicle);
    }

    [HttpPost]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateVehicleRequest request,
        CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleService.CreateVehicleAsync(
            GetUsuarioId(),
            request,
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { publicVehicleId = vehicle.PublicVehicleId },
            vehicle);
    }

    [HttpPut("{publicVehicleId}")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        string publicVehicleId,
        [FromBody] UpdateVehicleRequest request,
        CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleService.UpdateVehicleAsync(
            GetUsuarioId(),
            publicVehicleId,
            request,
            cancellationToken);
        return Ok(vehicle);
    }

    [HttpPatch("{publicVehicleId}/primary")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPrimary(
        string publicVehicleId,
        CancellationToken cancellationToken)
    {
        await _vehicleService.SetPrimaryVehicleAsync(
            GetUsuarioId(),
            publicVehicleId,
            cancellationToken);
        return NoContent();
    }

    [HttpDelete("{publicVehicleId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        string publicVehicleId,
        CancellationToken cancellationToken)
    {
        await _vehicleService.DeleteVehicleAsync(
            GetUsuarioId(),
            publicVehicleId,
            cancellationToken);
        return NoContent();
    }

    private Guid GetUsuarioId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim is null || !Guid.TryParse(claim.Value, out var usuarioId))
        {
            throw new UnauthorizedAccessException("Usuario no autenticado.");
        }

        return usuarioId;
    }
}
