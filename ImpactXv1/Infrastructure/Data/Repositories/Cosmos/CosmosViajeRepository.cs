using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Infrastructure.Data;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosViajeRepository : IViajeRepository
{
    private readonly Container _viajesContainer;
    private readonly Container _telemetryContainer;

    public CosmosViajeRepository(CosmosDbContext dbContext)
    {
        _viajesContainer = dbContext.Viajes;
        _telemetryContainer = dbContext.TelemetriaViaje;
    }

    public async Task<Viaje?> GetByIdAsync(Guid id)
    {
        // Cross-partition justificada: el contrato del repositorio solo
        // recibe el id del viaje y Viajes particiona por /usuarioId, por lo
        // que no hay partition key disponible. El ReadItemAsync con
        // PartitionKey(id) anterior devolvía 404 siempre (partition key
        // incorrecta); esta consulta parametrizada lo corrige.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.id = @id")
            .WithParameter("@id", id.ToString());

        using var iterator = _viajesContainer.GetItemQueryIterator<Viaje>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Viaje?> GetActiveByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND c.estado = 'Activo'")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _viajesContainer.GetItemQueryIterator<Viaje>(query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 1
            });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<List<Viaje>> GetByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.inicio DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<Viaje>();
        using var iterator = _viajesContainer.GetItemQueryIterator<Viaje>(query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 50
            });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
            if (results.Count >= 50) break;
        }
        return results;
    }

    public async Task AddAsync(Viaje viaje)
    {
        viaje.Id = Guid.NewGuid();
        await _viajesContainer.CreateItemAsync(viaje,
            CosmosPartitionKeys.For(viaje.UsuarioId));
    }

    public async Task UpdateAsync(Viaje viaje)
    {
        await _viajesContainer.ReplaceItemAsync(viaje,
            viaje.Id.ToString(),
            CosmosPartitionKeys.For(viaje.UsuarioId));
    }

    public async Task AddTelemetryAsync(ViajeTelemetry telemetry)
    {
        telemetry.Id = Guid.NewGuid();
        await _telemetryContainer.CreateItemAsync(telemetry,
            CosmosPartitionKeys.For(telemetry.ViajeId));
    }

    public async Task<List<ViajeTelemetry>> GetTelemetryByViajeAsync(Guid viajeId)
    {
        // Particionada por /viajeId: la telemetría permanece ligada al viaje.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.viajeId = @viajeId ORDER BY c.timestamp ASC")
            .WithParameter("@viajeId", viajeId.ToString());

        var results = new List<ViajeTelemetry>();
        using var iterator = _telemetryContainer.GetItemQueryIterator<ViajeTelemetry>(query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(viajeId),
                MaxItemCount = 100
            });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }
}
