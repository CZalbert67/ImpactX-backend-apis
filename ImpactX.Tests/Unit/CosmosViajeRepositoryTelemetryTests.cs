using System.Net;
using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Infrastructure.Data;
using ImpactX.Infrastructure.Data.Repositories.Cosmos;
using Microsoft.Azure.Cosmos;
using Moq;

namespace ImpactX.Tests.Unit;

public class CosmosViajeRepositoryTelemetryTests
{
    private readonly Mock<Container> _viajesContainer;
    private readonly Mock<Container> _telemetryContainer;
    private readonly CosmosViajeRepository _repo;

    public CosmosViajeRepositoryTelemetryTests()
    {
        _viajesContainer = new Mock<Container>();
        _telemetryContainer = new Mock<Container>();
        _repo = new CosmosViajeRepository(_viajesContainer.Object, _telemetryContainer.Object);
    }

    private static ViajeTelemetry Evento(Guid viajeId, Guid eventId, double lat = 19.43, double lng = -99.13, DateTime? timestamp = null) => new()
    {
        Id = eventId,
        ViajeId = viajeId,
        UsuarioId = Guid.NewGuid(),
        Timestamp = timestamp ?? DateTime.UtcNow.AddMinutes(-1),
        Lat = lat,
        Lng = lng,
        Velocidad = 50,
    };

    private static Mock<TransactionalBatchResponse> BatchResponse(bool success)
    {
        var response = new Mock<TransactionalBatchResponse>();
        response.SetupGet(r => r.IsSuccessStatusCode).Returns(success);
        return response;
    }

    private static Mock<TransactionalBatch> BatchMock(params Mock<TransactionalBatchResponse>[] responses)
    {
        var batch = new Mock<TransactionalBatch>();
        var sequence = batch.SetupSequence(b => b.ExecuteAsync(It.IsAny<CancellationToken>()));
        foreach (var response in responses)
        {
            sequence.ReturnsAsync(response.Object);
        }
        return batch;
    }

    private static ItemResponse<ViajeTelemetry> ReadResponse(ViajeTelemetry resource)
    {
        var response = new Mock<ItemResponse<ViajeTelemetry>>();
        response.SetupGet(r => r.Resource).Returns(resource);
        return response.Object;
    }

    private static ItemResponse<ViajeTelemetry> EmptyRead()
    {
        var response = new Mock<ItemResponse<ViajeTelemetry>>();
        response.SetupGet(r => r.Resource).Returns((ViajeTelemetry)null!);
        return response.Object;
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task GetTelemetryByEventIdAsync_UsesPointReadWithViajePartitionKey()
    {
        var viajeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var esperado = Evento(viajeId, eventId);

        _telemetryContainer
            .Setup(c => c.ReadItemAsync<ViajeTelemetry>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReadResponse(esperado));

        var resultado = await _repo.GetTelemetryByEventIdAsync(viajeId, eventId);

        Assert.Same(esperado, resultado);
        _telemetryContainer.Verify(c => c.ReadItemAsync<ViajeTelemetry>(
            eventId.ToString(),
            CosmosPartitionKeys.For(viajeId),
            It.IsAny<ItemRequestOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTelemetryByEventIdAsync_NotFound_ReturnsNull()
    {
        _telemetryContainer
            .Setup(c => c.ReadItemAsync<ViajeTelemetry>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new CosmosException("not found", HttpStatusCode.NotFound, 0, "", 0));

        var resultado = await _repo.GetTelemetryByEventIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(resultado);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AddTelemetryBatchAsync_UsesSinglePartitionTransactionalBatch()
    {
        var viajeId = Guid.NewGuid();
        var eventos = new[] { Evento(viajeId, Guid.NewGuid()), Evento(viajeId, Guid.NewGuid()) };

        var batch = BatchMock(BatchResponse(success: true));

        _telemetryContainer.Setup(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>())).Returns(batch.Object);

        var resultado = await _repo.AddTelemetryBatchAsync(viajeId, eventos);

        Assert.Equal(2, resultado.Insertados);
        Assert.Equal(0, resultado.Duplicados);
        _telemetryContainer.Verify(c => c.CreateTransactionalBatch(CosmosPartitionKeys.For(viajeId)), Times.Once);
        batch.Verify(b => b.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddTelemetryBatchAsync_Empty_NoBatchCreated()
    {
        var resultado = await _repo.AddTelemetryBatchAsync(Guid.NewGuid(), []);

        Assert.Equal(0, resultado.Insertados);
        Assert.Equal(0, resultado.Duplicados);
        _telemetryContainer.Verify(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>()), Times.Never);
    }

    [Fact]
    public async Task AddTelemetryBatchAsync_ForwardsCancellationToken()
    {
        var viajeId = Guid.NewGuid();
        var eventos = new[] { Evento(viajeId, Guid.NewGuid()) };
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var batch = BatchMock(BatchResponse(success: true));
        _telemetryContainer.Setup(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>())).Returns(batch.Object);

        await _repo.AddTelemetryBatchAsync(viajeId, eventos, token);

        batch.Verify(b => b.ExecuteAsync(token), Times.Once);
    }

    [Fact]
    public async Task AddTelemetryBatchAsync_AllInserted_WhenBatchSucceeds()
    {
        var viajeId = Guid.NewGuid();
        var eventos = Enumerable.Range(0, 50).Select(_ => Evento(viajeId, Guid.NewGuid())).ToList();

        var batch = BatchMock(BatchResponse(success: true));
        _telemetryContainer.Setup(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>())).Returns(batch.Object);

        var resultado = await _repo.AddTelemetryBatchAsync(viajeId, eventos);

        Assert.Equal(50, resultado.Insertados);
        Assert.Equal(0, resultado.Duplicados);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AddTelemetryBatchAsync_FailedBatch_NeverCountsIndividualOkAsInserted()
    {
        // Batch global fallido con una operación individual 200: el 200 NO
        // implica escritura persistida (el batch es atómico y fue revertido).
        // La clasificación se hace por point-read; el resultado nunca cuenta
        // la operación "OK" como insertada.
        var viajeId = Guid.NewGuid();
        var okId = Guid.NewGuid();
        var dupId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);
        var eventos = new[] { Evento(viajeId, okId, timestamp: timestamp), Evento(viajeId, dupId, timestamp: timestamp) };
        var existenteOk = Evento(viajeId, okId, timestamp: timestamp);
        var existenteDup = Evento(viajeId, dupId, timestamp: timestamp);

        var response = new Mock<TransactionalBatchResponse>();
        response.SetupGet(r => r.IsSuccessStatusCode).Returns(false);
        response.Setup(r => r.GetOperationResultAtIndex<ViajeTelemetry>(0))
            .Returns(BatchResult(HttpStatusCode.OK).Object);
        response.Setup(r => r.GetOperationResultAtIndex<ViajeTelemetry>(1))
            .Returns(BatchResult(HttpStatusCode.Conflict).Object);
        var batch = BatchMock(response);
        _telemetryContainer.Setup(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>())).Returns(batch.Object);
        _telemetryContainer
            .Setup(c => c.ReadItemAsync<ViajeTelemetry>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, PartitionKey _, ItemRequestOptions _, CancellationToken _) =>
                id == okId.ToString() ? ReadResponse(existenteOk) : ReadResponse(existenteDup));

        var resultado = await _repo.AddTelemetryBatchAsync(viajeId, eventos);

        // La operación con estado 200 dentro del batch fallido NO se cuenta.
        Assert.Equal(0, resultado.Insertados);
        Assert.Equal(2, resultado.Duplicados);
        batch.Verify(b => b.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AddTelemetryBatchAsync_ConflictWithFailedDependency_ResolvedByPointReads()
    {
        // Operaciones no responsables del fallo aparecen como FailedDependency:
        // no son fatales ni se interpretan como insertadas; el point-read
        // clasifica (existente idéntico → duplicado; inexistente → reintento).
        var viajeId = Guid.NewGuid();
        var dupId = Guid.NewGuid();
        var nuevoId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);
        var eventos = new[] { Evento(viajeId, dupId, timestamp: timestamp), Evento(viajeId, nuevoId, timestamp: timestamp) };
        var existente = Evento(viajeId, dupId, timestamp: timestamp);

        var failed = new Mock<TransactionalBatchResponse>();
        failed.SetupGet(r => r.IsSuccessStatusCode).Returns(false);
        failed.Setup(r => r.GetOperationResultAtIndex<ViajeTelemetry>(0))
            .Returns(BatchResult(HttpStatusCode.Conflict).Object);
        failed.Setup(r => r.GetOperationResultAtIndex<ViajeTelemetry>(1))
            .Returns(BatchResult(HttpStatusCode.FailedDependency).Object);
        var retry = BatchResponse(success: true);

        var batch = BatchMock(failed, retry);
        _telemetryContainer.Setup(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>())).Returns(batch.Object);
        _telemetryContainer
            .Setup(c => c.ReadItemAsync<ViajeTelemetry>(dupId.ToString(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReadResponse(existente));
        _telemetryContainer
            .Setup(c => c.ReadItemAsync<ViajeTelemetry>(nuevoId.ToString(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyRead());

        var resultado = await _repo.AddTelemetryBatchAsync(viajeId, eventos);

        Assert.Equal(1, resultado.Insertados);
        Assert.Equal(1, resultado.Duplicados);
        Assert.Equal(2, resultado.Insertados + resultado.Duplicados);
        _telemetryContainer.Verify(c => c.CreateTransactionalBatch(CosmosPartitionKeys.For(viajeId)), Times.Exactly(2));
        _telemetryContainer.Verify(c => c.ReadItemAsync<ViajeTelemetry>(
            dupId.ToString(), CosmosPartitionKeys.For(viajeId),
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _telemetryContainer.Verify(c => c.ReadItemAsync<ViajeTelemetry>(
            nuevoId.ToString(), CosmosPartitionKeys.For(viajeId),
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        // El reintento reconstruye el batch SOLO con el evento nuevo.
        batch.Verify(b => b.CreateItem(It.Is<ViajeTelemetry>(e => e.Id == nuevoId)), Times.Exactly(2));
        batch.Verify(b => b.CreateItem(It.Is<ViajeTelemetry>(e => e.Id == dupId)), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AddTelemetryBatchAsync_RaceDuplicateIdenticalPlusNew_RetriesOnlyTheNew()
    {
        // Carrera: el primer batch falla; los point-reads clasifican (1
        // duplicado idéntico + 1 nuevo); el segundo batch contiene SOLO el
        // evento nuevo; resultado final 1 insertado y 1 duplicado.
        var viajeId = Guid.NewGuid();
        var dupId = Guid.NewGuid();
        var nuevoId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);
        var eventos = new[] { Evento(viajeId, dupId, timestamp: timestamp), Evento(viajeId, nuevoId, timestamp: timestamp) };
        var existente = Evento(viajeId, dupId, timestamp: timestamp);

        var failed = BatchResponse(success: false);
        var retry = BatchResponse(success: true);
        var batch = BatchMock(failed, retry);
        _telemetryContainer.Setup(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>())).Returns(batch.Object);
        _telemetryContainer
            .Setup(c => c.ReadItemAsync<ViajeTelemetry>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, PartitionKey pk, ItemRequestOptions _, CancellationToken _) =>
                id == dupId.ToString() ? ReadResponse(existente) : EmptyRead());

        var resultado = await _repo.AddTelemetryBatchAsync(viajeId, eventos);

        Assert.Equal(1, resultado.Insertados);
        Assert.Equal(1, resultado.Duplicados);
        Assert.Equal(2, resultado.Insertados + resultado.Duplicados);
        _telemetryContainer.Verify(c => c.CreateTransactionalBatch(CosmosPartitionKeys.For(viajeId)), Times.Exactly(2));
        _telemetryContainer.Verify(c => c.ReadItemAsync<ViajeTelemetry>(
            dupId.ToString(), CosmosPartitionKeys.For(viajeId),
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        _telemetryContainer.Verify(c => c.ReadItemAsync<ViajeTelemetry>(
            nuevoId.ToString(), CosmosPartitionKeys.For(viajeId),
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
        // El reintento reconstruye el batch SOLO con el evento nuevo.
        batch.Verify(b => b.CreateItem(It.Is<ViajeTelemetry>(e => e.Id == nuevoId)), Times.Exactly(2));
        batch.Verify(b => b.CreateItem(It.Is<ViajeTelemetry>(e => e.Id == dupId)), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AddTelemetryBatchAsync_ConflictDifferentContent_ThrowsConflictWithoutRetry()
    {
        var viajeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var evento = Evento(viajeId, eventId, lat: 19.43);
        var existente = Evento(viajeId, eventId, lat: 20.0);

        var failed = BatchResponse(success: false);
        var batch = BatchMock(failed);
        _telemetryContainer.Setup(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>())).Returns(batch.Object);
        _telemetryContainer
            .Setup(c => c.ReadItemAsync<ViajeTelemetry>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReadResponse(existente));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _repo.AddTelemetryBatchAsync(viajeId, new[] { evento }));

        // 409 sin reintentar el batch y sin revelar EventId ni contenido.
        batch.Verify(b => b.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.DoesNotContain(eventId.ToString(), ex.Message);
        Assert.DoesNotContain("20", ex.Message);
    }

    [Fact]
    public async Task AddTelemetryBatchAsync_AllIdentical_NoSecondBatch()
    {
        var viajeId = Guid.NewGuid();
        var ids = Enumerable.Range(0, 3).Select(_ => Guid.NewGuid()).ToArray();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);
        var eventos = ids.Select(id => Evento(viajeId, id, timestamp: timestamp)).ToArray();
        var existentes = ids.Select(id => Evento(viajeId, id, timestamp: timestamp)).ToArray();

        var failed = BatchResponse(success: false);
        var batch = BatchMock(failed);
        _telemetryContainer.Setup(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>())).Returns(batch.Object);
        _telemetryContainer
            .Setup(c => c.ReadItemAsync<ViajeTelemetry>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, PartitionKey _, ItemRequestOptions _, CancellationToken _) =>
                ReadResponse(existentes[Array.FindIndex(ids, x => x.ToString() == id)]));

        var resultado = await _repo.AddTelemetryBatchAsync(viajeId, eventos);

        Assert.Equal(0, resultado.Insertados);
        Assert.Equal(3, resultado.Duplicados);
        Assert.Equal(3, resultado.Insertados + resultado.Duplicados);
        _telemetryContainer.Verify(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>()), Times.Once);
    }

    [Fact]
    public async Task AddTelemetryBatchAsync_SecondRetryRaces_ThirdAttemptSucceeds()
    {
        // Intento 1: carrera con dupA; intento 2 (solo pendientes): carrera
        // con dupB; intento 3: confirmado. Máximo 1 inicial + 2 reintentos.
        var viajeId = Guid.NewGuid();
        var aId = Guid.NewGuid();
        var bId = Guid.NewGuid();
        var cId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);
        var eventos = new[] { Evento(viajeId, aId, timestamp: timestamp), Evento(viajeId, bId, timestamp: timestamp), Evento(viajeId, cId, timestamp: timestamp) };

        var failed1 = BatchResponse(success: false);
        var failed2 = BatchResponse(success: false);
        var ok = BatchResponse(success: true);
        var batch = BatchMock(failed1, failed2, ok);
        _telemetryContainer.Setup(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>())).Returns(batch.Object);
        _telemetryContainer
            .Setup(c => c.ReadItemAsync<ViajeTelemetry>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, PartitionKey _, ItemRequestOptions _, CancellationToken _) =>
            {
                if (id == aId.ToString() || id == bId.ToString())
                    return ReadResponse(Evento(viajeId, Guid.Parse(id), timestamp: timestamp));
                return EmptyRead();
            });

        var resultado = await _repo.AddTelemetryBatchAsync(viajeId, eventos);

        Assert.Equal(1, resultado.Insertados);
        Assert.Equal(2, resultado.Duplicados);
        Assert.Equal(3, resultado.Insertados + resultado.Duplicados);
        batch.Verify(b => b.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        _telemetryContainer.Verify(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>()), Times.Exactly(3));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AddTelemetryBatchAsync_RetriesExhausted_ThrowsSafeError()
    {
        // Tres intentos fallidos con eventos todavía inexistentes: excepción
        // segura; nunca se afirma una inserción; sin ciclo infinito.
        var viajeId = Guid.NewGuid();
        var eventos = new[] { Evento(viajeId, Guid.NewGuid()) };

        var batch = BatchMock(BatchResponse(success: false), BatchResponse(success: false), BatchResponse(success: false));
        _telemetryContainer.Setup(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>())).Returns(batch.Object);
        _telemetryContainer
            .Setup(c => c.ReadItemAsync<ViajeTelemetry>(It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyRead());

        var ex = await Assert.ThrowsAsync<CosmosException>(() =>
            _repo.AddTelemetryBatchAsync(viajeId, eventos));

        batch.Verify(b => b.ExecuteAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
        _telemetryContainer.Verify(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>()), Times.Exactly(3));
        Assert.DoesNotContain(eventos[0].Id.ToString(), ex.Message);
    }

    [Fact]
    public async Task AddTelemetryBatchAsync_PropagatesCancellationTokenToPointReadsAndExecute()
    {
        var viajeId = Guid.NewGuid();
        var dupId = Guid.NewGuid();
        var nuevoId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);
        var eventos = new[] { Evento(viajeId, dupId, timestamp: timestamp), Evento(viajeId, nuevoId, timestamp: timestamp) };
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var batch = BatchMock(BatchResponse(success: false), BatchResponse(success: true));
        _telemetryContainer.Setup(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>())).Returns(batch.Object);
        _telemetryContainer
            .Setup(c => c.ReadItemAsync<ViajeTelemetry>(dupId.ToString(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReadResponse(Evento(viajeId, dupId, timestamp: timestamp)));
        _telemetryContainer
            .Setup(c => c.ReadItemAsync<ViajeTelemetry>(nuevoId.ToString(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(EmptyRead());

        var resultado = await _repo.AddTelemetryBatchAsync(viajeId, eventos, token);

        Assert.Equal(1, resultado.Insertados);
        Assert.Equal(1, resultado.Duplicados);
        batch.Verify(b => b.ExecuteAsync(token), Times.Exactly(2));
        _telemetryContainer.Verify(c => c.ReadItemAsync<ViajeTelemetry>(
            dupId.ToString(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), token), Times.Once);
        _telemetryContainer.Verify(c => c.ReadItemAsync<ViajeTelemetry>(
            nuevoId.ToString(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), token), Times.Once);
    }

    [Fact]
    public async Task AddTelemetryBatchAsync_NeverUsesUpsertOrReplace()
    {
        var viajeId = Guid.NewGuid();
        var eventos = new[] { Evento(viajeId, Guid.NewGuid()) };

        var batch = BatchMock(BatchResponse(success: true));
        _telemetryContainer.Setup(c => c.CreateTransactionalBatch(It.IsAny<PartitionKey>())).Returns(batch.Object);

        await _repo.AddTelemetryBatchAsync(viajeId, eventos);

        _telemetryContainer.Verify(c => c.UpsertItemAsync(It.IsAny<ViajeTelemetry>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
        _telemetryContainer.Verify(c => c.ReplaceItemAsync(It.IsAny<ViajeTelemetry>(), It.IsAny<string>(), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static Mock<TransactionalBatchOperationResult<ViajeTelemetry>> BatchResult(HttpStatusCode status)
    {
        var result = new Mock<TransactionalBatchOperationResult<ViajeTelemetry>>();
        result.SetupGet(r => r.StatusCode).Returns(status);
        result.SetupGet(r => r.IsSuccessStatusCode).Returns(status == HttpStatusCode.OK);
        return result;
    }
}
