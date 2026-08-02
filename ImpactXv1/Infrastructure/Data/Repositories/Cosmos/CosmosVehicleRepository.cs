using System.Net;
using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using Microsoft.Azure.Cosmos;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosVehicleRepository : IVehicleRepository
{
    private readonly Container _container;

    public CosmosVehicleRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Vehicles;
    }

    public async Task<Vehicle?> GetByPublicIdAsync(
        Guid ownerUserId,
        string publicVehicleId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.ownerUserId = @ownerUserId " +
            "AND c.publicVehicleId = @publicVehicleId AND c.activo = true")
            .WithParameter("@ownerUserId", ownerUserId.ToString())
            .WithParameter("@publicVehicleId", publicVehicleId);

        using var iterator = _container.GetItemQueryIterator<Vehicle>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(ownerUserId),
                MaxItemCount = 1
            });

        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }

    public async Task<IReadOnlyList<Vehicle>> GetAllByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.ownerUserId = @ownerUserId AND c.activo = true")
            .WithParameter("@ownerUserId", ownerUserId.ToString());

        var vehicles = new List<Vehicle>();
        using var iterator = _container.GetItemQueryIterator<Vehicle>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(ownerUserId),
                MaxItemCount = 100
            });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            vehicles.AddRange(response);
        }

        return vehicles
            .OrderByDescending(vehicle => vehicle.EsPrincipal)
            .ThenBy(vehicle => vehicle.CreatedAtUtc)
            .ThenBy(vehicle => vehicle.PublicVehicleId)
            .ToList();
    }

    public async Task<bool> ExistsByPublicIdAsync(
        string publicVehicleId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.publicVehicleId = @publicVehicleId")
            .WithParameter("@publicVehicleId", publicVehicleId);

        using var iterator = _container.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });

        if (!iterator.HasMoreResults)
        {
            return false;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault() > 0;
    }

    public async Task<int> CountActiveByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.ownerUserId = @ownerUserId " +
            "AND c.activo = true")
            .WithParameter("@ownerUserId", ownerUserId.ToString());

        using var iterator = _container.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(ownerUserId),
                MaxItemCount = 1
            });

        if (!iterator.HasMoreResults)
        {
            return 0;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }

    public async Task AddAsync(
        Vehicle vehicle,
        bool makePrimary,
        CancellationToken cancellationToken = default)
    {
        var partitionKey = CosmosPartitionKeys.For(vehicle.OwnerUserId);
        var batch = _container.CreateTransactionalBatch(partitionKey);

        if (makePrimary)
        {
            var currentPrimaries = await GetPrimaryVehiclesAsync(
                vehicle.OwnerUserId,
                cancellationToken);
            var now = vehicle.CreatedAtUtc;

            foreach (var current in currentPrimaries)
            {
                current.EsPrincipal = false;
                current.UpdatedAtUtc = now;
                batch.ReplaceItem(current.Id.ToString(), current);
            }

            vehicle.EsPrincipal = true;
        }

        batch.CreateItem(vehicle);
        await ExecuteBatchAsync(batch, cancellationToken);
    }

    public async Task UpdateAsync(
        Vehicle vehicle,
        CancellationToken cancellationToken = default)
    {
        await _container.ReplaceItemAsync(
            vehicle,
            vehicle.Id.ToString(),
            CosmosPartitionKeys.For(vehicle.OwnerUserId),
            cancellationToken: cancellationToken);
    }

    public async Task SetPrimaryAsync(
        Guid ownerUserId,
        string publicVehicleId,
        CancellationToken cancellationToken = default)
    {
        var activeVehicles = await GetAllByOwnerAsync(ownerUserId, cancellationToken);
        var selected = activeVehicles.FirstOrDefault(
            vehicle => vehicle.PublicVehicleId == publicVehicleId);
        if (selected is null)
        {
            throw new NotFoundException("Vehículo no encontrado.");
        }

        var batch = _container.CreateTransactionalBatch(CosmosPartitionKeys.For(ownerUserId));
        var now = DateTime.UtcNow;
        var hasOperations = false;

        foreach (var vehicle in activeVehicles)
        {
            var shouldBePrimary = vehicle.Id == selected.Id;
            if (vehicle.EsPrincipal != shouldBePrimary)
            {
                vehicle.EsPrincipal = shouldBePrimary;
                vehicle.UpdatedAtUtc = now;
                batch.ReplaceItem(vehicle.Id.ToString(), vehicle);
                hasOperations = true;
            }
        }

        if (hasOperations)
        {
            await ExecuteBatchAsync(batch, cancellationToken);
        }
    }

    public async Task SoftDeleteAsync(
        Guid ownerUserId,
        string publicVehicleId,
        CancellationToken cancellationToken = default)
    {
        var activeVehicles = await GetAllByOwnerAsync(ownerUserId, cancellationToken);
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

        var batch = _container.CreateTransactionalBatch(CosmosPartitionKeys.For(ownerUserId));
        batch.ReplaceItem(selected.Id.ToString(), selected);

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
                batch.ReplaceItem(replacement.Id.ToString(), replacement);
            }
        }

        await ExecuteBatchAsync(batch, cancellationToken);
    }

    private async Task<IReadOnlyList<Vehicle>> GetPrimaryVehiclesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.ownerUserId = @ownerUserId " +
            "AND c.activo = true AND c.esPrincipal = true")
            .WithParameter("@ownerUserId", ownerUserId.ToString());

        var vehicles = new List<Vehicle>();
        using var iterator = _container.GetItemQueryIterator<Vehicle>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(ownerUserId),
                MaxItemCount = 100
            });

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            vehicles.AddRange(response);
        }

        return vehicles;
    }

    private static async Task ExecuteBatchAsync(
        TransactionalBatch batch,
        CancellationToken cancellationToken)
    {
        using var response = await batch.ExecuteAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var message = response.StatusCode == HttpStatusCode.Conflict
                ? "Conflicto al persistir el vehículo."
                : "No fue posible persistir el cambio de vehículo de forma atómica.";
            throw new InvalidOperationException(message);
        }
    }
}
