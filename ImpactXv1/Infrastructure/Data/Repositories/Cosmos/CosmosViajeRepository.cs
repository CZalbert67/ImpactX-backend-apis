using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using ImpactX.Core.Telemetry;
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

    internal CosmosViajeRepository(Container viajesContainer, Container telemetryContainer)
    {
        _viajesContainer = viajesContainer;
        _telemetryContainer = telemetryContainer;
    }

    public async Task<Viaje?> GetByIdAsync(Guid id)
    {
        // Cross-partition justificada: el contrato del repositorio solo
        // recibe el id del viaje y Viajes particiona por /usuarioId, por lo
        // que no hay partition key disponible. Los servicios que conocen el
        // usuario deben usar GetByIdAsync(usuarioId, id) (point-read).
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

    public async Task<Viaje?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        try
        {
            var response = await _viajesContainer.ReadItemAsync<Viaje>(
                id.ToString(),
                CosmosPartitionKeys.For(usuarioId));
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

    public async Task<PagedResult<Viaje>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.inicio DESC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        return await CosmosPageReader.ReadSinglePageAsync<Viaje>(
            _viajesContainer, query, CosmosPartitionKeys.For(usuarioId),
            pageSize, continuationToken, cancellationToken);
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

    public async Task<ViajeTelemetry?> GetTelemetryByEventIdAsync(Guid viajeId, Guid eventId, CancellationToken cancellationToken = default)
    {
        // Point-read con id + PartitionKey(viajeId): sin consulta global.
        try
        {
            var response = await _telemetryContainer.ReadItemAsync<ViajeTelemetry>(
                eventId.ToString(),
                CosmosPartitionKeys.For(viajeId),
                cancellationToken: cancellationToken);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<TelemetryBatchWriteResult> AddTelemetryBatchAsync(Guid viajeId, IReadOnlyList<ViajeTelemetry> eventos, CancellationToken cancellationToken = default)
    {
        if (eventos.Count == 0)
            return new TelemetryBatchWriteResult();

        // Un TransactionalBatch es atómico: si IsSuccessStatusCode es false,
        // NINGUNA operación quedó persistida, aunque operaciones individuales
        // reporten 200. Los estados por operación (200/409/FailedDependency)
        // nunca declaran inserciones dentro de un batch global fallido; la
        // clasificación tras un fallo se hace por point-read (id + PK) de
        // cada EventId candidato.
        var pendientes = new List<ViajeTelemetry>(eventos);
        var duplicados = 0;

        // 1 intento inicial + 2 reintentos acotados; nunca un ciclo infinito.
        const int maxIntentos = 3;
        for (var intento = 1; intento <= maxIntentos; intento++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Todos los eventos pertenecen a la misma partición (/viajeId):
            // TransactionalBatch atómico de una sola partición (máx. 100 ops).
            var batch = _telemetryContainer.CreateTransactionalBatch(CosmosPartitionKeys.For(viajeId));
            foreach (var evento in pendientes)
            {
                batch.CreateItem(evento);
            }

            var response = await batch.ExecuteAsync(cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                // Solo un batch confirmado cuenta inserciones (atómico).
                return new TelemetryBatchWriteResult { Insertados = pendientes.Count, Duplicados = duplicados };
            }

            // Batch fallido: nada se persistió. Clasificar cada candidato con
            // point-read (id + PartitionKey(viajeId)), nunca por los estados
            // individuales del batch (FailedDependency incluye operaciones que
            // no causaron el fallo).
            var nuevosPendientes = new List<ViajeTelemetry>(pendientes.Count);
            foreach (var evento in pendientes)
            {
                var existente = await GetTelemetryByEventIdAsync(viajeId, evento.Id, cancellationToken);
                if (existente is null)
                {
                    nuevosPendientes.Add(evento);
                }
                else if (TelemetryEventEquality.IsIdentical(existente, evento))
                {
                    duplicados++;
                }
                else
                {
                    // Conflicto real de contenido: 409 sin reintentar el batch
                    // y sin revelar EventId ni contenido.
                    throw new ConflictException("El evento ya existe con contenido diferente.");
                }
            }

            if (nuevosPendientes.Count == 0)
            {
                // Todos los eventos existen idénticos: sin segundo batch.
                return new TelemetryBatchWriteResult { Insertados = 0, Duplicados = duplicados };
            }

            pendientes = nuevosPendientes;
        }

        // Reintentos agotados: excepción segura; no se afirma ninguna
        // inserción. Mensaje genérico, sin EventId ni payload.
        throw new CosmosException(
            "No se pudo confirmar la escritura por lotes de telemetría.",
            System.Net.HttpStatusCode.InternalServerError, 0, "", 0);
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

    public async Task<PagedResult<ViajeTelemetry>> GetTelemetryByViajePagedAsync(Guid viajeId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        // Particionada por /viajeId: orden cronológico ascendente compatible
        // con partición única; una sola página por llamada.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.viajeId = @viajeId ORDER BY c.timestamp ASC")
            .WithParameter("@viajeId", viajeId.ToString());

        return await CosmosPageReader.ReadSinglePageAsync<ViajeTelemetry>(
            _telemetryContainer, query, CosmosPartitionKeys.For(viajeId),
            pageSize, continuationToken, cancellationToken);
    }
}
