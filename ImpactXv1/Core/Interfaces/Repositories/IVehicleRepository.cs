using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByPublicIdAsync(Guid ownerId, string publicVehicleId);
    Task<IReadOnlyCollection<Vehicle>> GetAllByOwnerAsync(Guid ownerId);
    Task AddAsync(Vehicle vehicle);
    Task UpdateAsync(Vehicle vehicle);
    Task SoftDeleteAsync(Vehicle vehicle);
    Task<int> CountActiveByOwnerAsync(Guid ownerId);
    Task<Vehicle?> GetPrimaryActiveAsync(Guid ownerId);
    Task SetPrimaryAsync(Guid ownerId, string publicVehicleId);
}
