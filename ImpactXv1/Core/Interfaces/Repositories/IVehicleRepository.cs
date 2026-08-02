using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByPublicIdAsync(
        Guid ownerUserId,
        string publicVehicleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Vehicle>> GetAllByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsByPublicIdAsync(
        string publicVehicleId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Vehicle vehicle,
        bool makePrimary,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        Vehicle vehicle,
        CancellationToken cancellationToken = default);

    Task SetPrimaryAsync(
        Guid ownerUserId,
        string publicVehicleId,
        CancellationToken cancellationToken = default);

    Task SoftDeleteAsync(
        Guid ownerUserId,
        string publicVehicleId,
        CancellationToken cancellationToken = default);
}
