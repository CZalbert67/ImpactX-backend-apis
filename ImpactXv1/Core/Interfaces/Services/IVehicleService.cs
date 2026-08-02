using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ImpactX.Models.DTOs.Vehicles;

namespace ImpactX.Core.Interfaces.Services;

public interface IVehicleService
{
    Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default);

    Task<VehicleDto> GetVehicleAsync(
        Guid usuarioId,
        string publicVehicleId,
        CancellationToken cancellationToken = default);

    Task<VehicleDto> CreateVehicleAsync(
        Guid usuarioId,
        CreateVehicleRequest request,
        CancellationToken cancellationToken = default);

    Task<VehicleDto> UpdateVehicleAsync(
        Guid usuarioId,
        string publicVehicleId,
        UpdateVehicleRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteVehicleAsync(
        Guid usuarioId,
        string publicVehicleId,
        CancellationToken cancellationToken = default);

    Task SetPrimaryVehicleAsync(
        Guid usuarioId,
        string publicVehicleId,
        CancellationToken cancellationToken = default);
}
