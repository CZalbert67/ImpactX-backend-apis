using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class VehicleRepository : IVehicleRepository
{
    private readonly ApplicationDbContext _context;

    public VehicleRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Vehicle?> GetByPublicIdAsync(
        Guid ownerUserId,
        string publicVehicleId,
        CancellationToken cancellationToken = default)
    {
        return _context.Vehicles.FirstOrDefaultAsync(
            vehicle => vehicle.OwnerUserId == ownerUserId
                && vehicle.PublicVehicleId == publicVehicleId
                && vehicle.Activo,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Vehicle>> GetAllByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Vehicles
            .Where(vehicle => vehicle.OwnerUserId == ownerUserId && vehicle.Activo)
            .OrderByDescending(vehicle => vehicle.EsPrincipal)
            .ThenBy(vehicle => vehicle.CreatedAtUtc)
            .ThenBy(vehicle => vehicle.PublicVehicleId)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsByPublicIdAsync(
        string publicVehicleId,
        CancellationToken cancellationToken = default)
    {
        return _context.Vehicles.AnyAsync(
            vehicle => vehicle.PublicVehicleId == publicVehicleId,
            cancellationToken);
    }

    public Task<int> CountActiveByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.Vehicles.CountAsync(
            vehicle => vehicle.OwnerUserId == ownerUserId && vehicle.Activo,
            cancellationToken);
    }

    public async Task AddAsync(
        Vehicle vehicle,
        bool makePrimary,
        CancellationToken cancellationToken = default)
    {
        if (makePrimary)
        {
            var currentPrimaries = await _context.Vehicles
                .Where(existing => existing.OwnerUserId == vehicle.OwnerUserId
                    && existing.Activo
                    && existing.EsPrincipal)
                .ToListAsync(cancellationToken);

            foreach (var current in currentPrimaries)
            {
                current.EsPrincipal = false;
                current.UpdatedAtUtc = vehicle.CreatedAtUtc;
            }

            vehicle.EsPrincipal = true;
        }

        await _context.Vehicles.AddAsync(vehicle, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        Vehicle vehicle,
        CancellationToken cancellationToken = default)
    {
        _context.Vehicles.Update(vehicle);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SetPrimaryAsync(
        Guid ownerUserId,
        string publicVehicleId,
        CancellationToken cancellationToken = default)
    {
        var activeVehicles = await _context.Vehicles
            .Where(vehicle => vehicle.OwnerUserId == ownerUserId && vehicle.Activo)
            .ToListAsync(cancellationToken);

        var selected = activeVehicles.FirstOrDefault(
            vehicle => vehicle.PublicVehicleId == publicVehicleId);
        if (selected is null)
        {
            throw new NotFoundException("Vehículo no encontrado.");
        }

        var now = DateTime.UtcNow;
        foreach (var vehicle in activeVehicles)
        {
            var shouldBePrimary = vehicle.Id == selected.Id;
            if (vehicle.EsPrincipal != shouldBePrimary)
            {
                vehicle.EsPrincipal = shouldBePrimary;
                vehicle.UpdatedAtUtc = now;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task SoftDeleteAsync(
        Guid ownerUserId,
        string publicVehicleId,
        CancellationToken cancellationToken = default)
    {
        var activeVehicles = await _context.Vehicles
            .Where(vehicle => vehicle.OwnerUserId == ownerUserId && vehicle.Activo)
            .OrderBy(vehicle => vehicle.CreatedAtUtc)
            .ThenBy(vehicle => vehicle.PublicVehicleId)
            .ToListAsync(cancellationToken);

        var selected = activeVehicles.FirstOrDefault(
            vehicle => vehicle.PublicVehicleId == publicVehicleId);
        if (selected is null)
        {
            throw new NotFoundException("Vehículo no encontrado.");
        }

        var now = DateTime.UtcNow;
        var wasPrimary = selected.EsPrincipal;
        selected.Activo = false;
        selected.EsPrincipal = false;
        selected.DeletedAtUtc = now;
        selected.UpdatedAtUtc = now;

        if (wasPrimary)
        {
            var replacement = activeVehicles
                .Where(vehicle => vehicle.Id != selected.Id)
                .OrderBy(vehicle => vehicle.CreatedAtUtc)
                .ThenBy(vehicle => vehicle.PublicVehicleId)
                .FirstOrDefault();

            if (replacement is not null)
            {
                replacement.EsPrincipal = true;
                replacement.UpdatedAtUtc = now;
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
