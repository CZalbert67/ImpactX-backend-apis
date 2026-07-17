using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

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
        try
        {
            var response = await _viajesContainer.ReadItemAsync<Viaje>(
                id.ToString(), new PartitionKey(id.ToString()));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Viaje?> GetActiveByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND c.estado = 'Activo'")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _viajesContainer.GetItemQueryIterator<Viaje>(query);
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
        using var iterator = _viajesContainer.GetItemQueryIterator<Viaje>(query);
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
        await _viajesContainer.CreateItemAsync(viaje, new PartitionKey(viaje.UsuarioId.ToString()));
    }

    public async Task UpdateAsync(Viaje viaje)
    {
        await _viajesContainer.UpsertItemAsync(viaje, new PartitionKey(viaje.UsuarioId.ToString()));
    }

    public async Task AddTelemetryAsync(ViajeTelemetry telemetry)
    {
        telemetry.Id = Guid.NewGuid();
        await _telemetryContainer.CreateItemAsync(telemetry, new PartitionKey(telemetry.ViajeId.ToString()));
    }

    public async Task<List<ViajeTelemetry>> GetTelemetryByViajeAsync(Guid viajeId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.viajeId = @viajeId ORDER BY c.timestamp ASC")
            .WithParameter("@viajeId", viajeId.ToString());

        var results = new List<ViajeTelemetry>();
        using var iterator = _telemetryContainer.GetItemQueryIterator<ViajeTelemetry>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }
}
