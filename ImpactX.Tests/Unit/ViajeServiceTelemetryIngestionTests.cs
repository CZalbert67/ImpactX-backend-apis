using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Telemetry;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ImpactX.Tests.Unit;

public class ViajeServiceTelemetryIngestionTests
{
    private sealed class RecordingLogger : ILogger<ViajeService>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        public void Dispose() { }
    }

    private readonly Mock<IViajeRepository> _viajeRepo;
    private readonly RecordingLogger _logger;
    private readonly ViajeService _viajeService;

    public ViajeServiceTelemetryIngestionTests()
    {
        _viajeRepo = new Mock<IViajeRepository>();
        _logger = new RecordingLogger();
        _viajeService = new ViajeService(_viajeRepo.Object, _logger);
    }

    private static Viaje OwnActiveTrip(Guid usuarioId) => new()
    {
        Id = Guid.NewGuid(),
        UsuarioId = usuarioId,
        Estado = "Activo",
    };

    private static TelemetryEventRequest Evento(Guid eventId, double lat = 19.43, double lng = -99.13) => new()
    {
        EventId = eventId,
        Timestamp = DateTime.UtcNow,
        Lat = lat,
        Lng = lng,
        Velocidad = 50,
    };

    [Fact]
    public async Task IngestTelemetryAsync_OwnActiveTrip_InsertsSingleEvent()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        var eventId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);

        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ViajeTelemetry?)null);
        _viajeRepo.Setup(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryBatchWriteResult { Insertados = 1 });

        var request = new TelemetryBatchRequest { Eventos = [Evento(eventId)] };
        request.Eventos[0].Timestamp = timestamp;

        var result = await _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id, request);

        Assert.Equal(viaje.Id, result.ViajeId);
        Assert.Equal(1, result.Recibidos);
        Assert.Equal(1, result.Insertados);
        Assert.Equal(0, result.Duplicados);
        Assert.Equal(timestamp, result.PrimerEventoUtc);
        Assert.Equal(timestamp, result.UltimoEventoUtc);

        _viajeRepo.Verify(r => r.AddTelemetryBatchAsync(viaje.Id,
            It.Is<IReadOnlyList<ViajeTelemetry>>(list =>
                list.Count == 1 && list[0].Id == eventId && list[0].ViajeId == viaje.Id &&
                list[0].UsuarioId == usuarioId && list[0].Timestamp == timestamp),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestTelemetryAsync_MultipleEvents_InsertsAll()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        var timestamps = new[] { DateTime.UtcNow.AddMinutes(-3), DateTime.UtcNow.AddMinutes(-1) };

        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ViajeTelemetry?)null);
        _viajeRepo.Setup(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryBatchWriteResult { Insertados = 2 });

        var request = new TelemetryBatchRequest
        {
            Eventos = [Evento(Guid.NewGuid()), Evento(Guid.NewGuid())]
        };
        request.Eventos[0].Timestamp = timestamps[0];
        request.Eventos[1].Timestamp = timestamps[1];

        var result = await _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id, request);

        Assert.Equal(2, result.Insertados);
        Assert.Equal(timestamps.Min(), result.PrimerEventoUtc);
        Assert.Equal(timestamps.Max(), result.UltimoEventoUtc);
        _viajeRepo.Verify(r => r.AddTelemetryBatchAsync(viaje.Id,
            It.Is<IReadOnlyList<ViajeTelemetry>>(list => list.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task IngestTelemetryAsync_EmptyBatch_ThrowsBadRequest()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id, new TelemetryBatchRequest()));

        _viajeRepo.Verify(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestTelemetryAsync_101Events_ThrowsBadRequest()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        var request = new TelemetryBatchRequest
        {
            Eventos = Enumerable.Range(0, 101).Select(_ => Evento(Guid.NewGuid())).ToList()
        };

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id, request));
    }

    [Fact]
    public async Task IngestTelemetryAsync_DuplicateEventIdWithinBatch_ThrowsBadRequest()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        var eventId = Guid.NewGuid();
        var request = new TelemetryBatchRequest { Eventos = [Evento(eventId), Evento(eventId)] };

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id, request));
    }

    [Fact]
    public async Task IngestTelemetryAsync_NonUtcTimestamp_ThrowsBadRequest()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        var evento = Evento(Guid.NewGuid());
        evento.Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id, new TelemetryBatchRequest { Eventos = [evento] }));
    }

    [Fact]
    public async Task IngestTelemetryAsync_TripNotFound_ThrowsNotFound()
    {
        var usuarioId = Guid.NewGuid();
        var viajeId = Guid.NewGuid();
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viajeId)).ReturnsAsync((Viaje?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _viajeService.IngestTelemetryAsync(usuarioId, viajeId, new TelemetryBatchRequest { Eventos = [Evento(Guid.NewGuid())] }));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task IngestTelemetryAsync_OtherUsersTrip_ThrowsForbidden()
    {
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        var viaje = new Viaje { Id = Guid.NewGuid(), UsuarioId = otroUsuarioId, Estado = "Activo" };
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id, new TelemetryBatchRequest { Eventos = [Evento(Guid.NewGuid())] }));

        _viajeRepo.Verify(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestTelemetryAsync_FinishedTrip_ThrowsConflict()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = new Viaje { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Finalizado" };
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id, new TelemetryBatchRequest { Eventos = [Evento(Guid.NewGuid())] }));

        _viajeRepo.Verify(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestTelemetryAsync_IdenticalResend_CountsDuplicateWithoutBatch()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        var eventId = Guid.NewGuid();
        var existente = new ViajeTelemetry
        {
            Id = eventId,
            ViajeId = viaje.Id,
            UsuarioId = usuarioId,
            Timestamp = DateTime.UtcNow.AddMinutes(-2),
            Lat = 19.43,
            Lng = -99.13,
            Velocidad = 50,
            RecibidoEn = DateTime.UtcNow,
        };

        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existente);
        _viajeRepo.Setup(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryBatchWriteResult());

        var evento = Evento(eventId);
        evento.Timestamp = existente.Timestamp;

        var result = await _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id, new TelemetryBatchRequest { Eventos = [evento] });

        Assert.Equal(1, result.Recibidos);
        Assert.Equal(0, result.Insertados);
        Assert.Equal(1, result.Duplicados);
        // Sin eventos nuevos el servicio no crea ningún batch: cero
        // inserciones de un batch fallido y cero llamadas de escritura.
        _viajeRepo.Verify(r => r.AddTelemetryBatchAsync(viaje.Id,
            It.IsAny<IReadOnlyList<ViajeTelemetry>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task IngestTelemetryAsync_SameEventIdDifferentContent_ThrowsConflict()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        var eventId = Guid.NewGuid();
        var existente = new ViajeTelemetry
        {
            Id = eventId,
            ViajeId = viaje.Id,
            UsuarioId = usuarioId,
            Timestamp = DateTime.UtcNow.AddMinutes(-2),
            Lat = 19.43,
            Lng = -99.13,
            Velocidad = 50,
        };

        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), eventId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existente);

        var evento = Evento(eventId, lat: 20.0);
        evento.Timestamp = existente.Timestamp;

        await Assert.ThrowsAsync<ConflictException>(() =>
            _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id, new TelemetryBatchRequest { Eventos = [evento] }));

        _viajeRepo.Verify(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestTelemetryAsync_RepoRaceConflict_AccumulatesDuplicates()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ViajeTelemetry?)null);
        _viajeRepo.Setup(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryBatchWriteResult { Insertados = 0, Duplicados = 1 });

        var result = await _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id,
            new TelemetryBatchRequest { Eventos = [Evento(Guid.NewGuid())] });

        Assert.Equal(0, result.Insertados);
        Assert.Equal(1, result.Duplicados);
    }

    [Fact]
    public async Task IngestTelemetryAsync_Invariant_AllNew_InsertedEqualsRecibidos()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ViajeTelemetry?)null);
        _viajeRepo.Setup(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryBatchWriteResult { Insertados = 3 });

        var result = await _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id,
            new TelemetryBatchRequest { Eventos = [Evento(Guid.NewGuid()), Evento(Guid.NewGuid()), Evento(Guid.NewGuid())] });

        Assert.Equal(3, result.Recibidos);
        Assert.Equal(3, result.Insertados);
        Assert.Equal(0, result.Duplicados);
        Assert.Equal(result.Recibidos, result.Insertados + result.Duplicados);
    }

    [Fact]
    public async Task IngestTelemetryAsync_Invariant_AllDuplicates_InsertedZero()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        var eventId1 = Guid.NewGuid();
        var eventId2 = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);
        var existente1 = new ViajeTelemetry { Id = eventId1, ViajeId = viaje.Id, Timestamp = timestamp, Lat = 19.43, Lng = -99.13, Velocidad = 50 };
        var existente2 = new ViajeTelemetry { Id = eventId2, ViajeId = viaje.Id, Timestamp = timestamp, Lat = 19.43, Lng = -99.13, Velocidad = 50 };
        var evento1 = Evento(eventId1);
        evento1.Timestamp = timestamp;
        var evento2 = Evento(eventId2);
        evento2.Timestamp = timestamp;

        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ViajeTelemetry?)null);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), eventId1, It.IsAny<CancellationToken>())).ReturnsAsync(existente1);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), eventId2, It.IsAny<CancellationToken>())).ReturnsAsync(existente2);
        // Todos duplicados idénticos: el servicio no llama al batch y el
        // resultado final no cuenta inserciones de ningún batch fallido.
        var result = await _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id,
            new TelemetryBatchRequest { Eventos = [evento1, evento2] });

        Assert.Equal(2, result.Recibidos);
        Assert.Equal(0, result.Insertados);
        Assert.Equal(2, result.Duplicados);
        Assert.Equal(result.Recibidos, result.Insertados + result.Duplicados);
        _viajeRepo.Verify(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task IngestTelemetryAsync_Invariant_MixedPrecheckAndRaceDuplicates()
    {
        // 3 recibidos: 1 duplicado detectado en el pre-check, 1 duplicado por
        // carrera resuelto por el repositorio y 1 insertado en el batch
        // final. Recibidos == Insertados + Duplicados debe mantenerse.
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        var dupPrecheck = Guid.NewGuid();
        var dupCarrera = Guid.NewGuid();
        var nuevo = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);
        var existente = new ViajeTelemetry { Id = dupPrecheck, ViajeId = viaje.Id, Timestamp = timestamp, Lat = 19.43, Lng = -99.13, Velocidad = 50 };
        var eventoPrecheck = Evento(dupPrecheck);
        eventoPrecheck.Timestamp = timestamp;

        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ViajeTelemetry?)null);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), dupPrecheck, It.IsAny<CancellationToken>())).ReturnsAsync(existente);
        _viajeRepo.Setup(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryBatchWriteResult { Insertados = 1, Duplicados = 1 });

        var result = await _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id,
            new TelemetryBatchRequest { Eventos = [eventoPrecheck, Evento(dupCarrera), Evento(nuevo)] });

        Assert.Equal(3, result.Recibidos);
        Assert.Equal(1, result.Insertados);
        Assert.Equal(2, result.Duplicados);
        Assert.Equal(result.Recibidos, result.Insertados + result.Duplicados);
    }

    [Fact]
    public async Task IngestTelemetryAsync_ForwardsCancellationToken()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ViajeTelemetry?)null);
        _viajeRepo.Setup(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryBatchWriteResult { Insertados = 1 });

        await _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id,
            new TelemetryBatchRequest { Eventos = [Evento(Guid.NewGuid())] }, token);

        _viajeRepo.Verify(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), token), Times.Once);
        _viajeRepo.Verify(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), token), Times.Once);
    }

    [Fact]
    public async Task IngestTelemetryAsync_ClientTimestampPreserved_ServerReceptionSeparate()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        var eventId = Guid.NewGuid();
        var clientTimestamp = DateTime.UtcNow.AddMinutes(-10);

        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ViajeTelemetry?)null);
        _viajeRepo.Setup(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryBatchWriteResult { Insertados = 1 });

        var evento = Evento(eventId);
        evento.Timestamp = clientTimestamp;

        await _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id, new TelemetryBatchRequest { Eventos = [evento] });

        _viajeRepo.Verify(r => r.AddTelemetryBatchAsync(viaje.Id,
            It.Is<IReadOnlyList<ViajeTelemetry>>(list =>
                list.Count == 1 && list[0].Timestamp == clientTimestamp &&
                list[0].RecibidoEn != clientTimestamp && list[0].RecibidoEn.Kind == DateTimeKind.Utc),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task IngestTelemetryAsync_LogsOnlyCounts_NeverSensitiveData()
    {
        var usuarioId = Guid.NewGuid();
        var viaje = OwnActiveTrip(usuarioId);
        var eventId = Guid.NewGuid();
        var lat = 19.432601;
        var lng = -99.133208;
        var timestamp = DateTime.UtcNow.AddMinutes(-1);

        _viajeRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), viaje.Id)).ReturnsAsync(viaje);
        _viajeRepo.Setup(r => r.GetTelemetryByEventIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ViajeTelemetry?)null);
        _viajeRepo.Setup(r => r.AddTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryBatchWriteResult { Insertados = 1 });

        var evento = Evento(eventId, lat, lng);
        evento.Timestamp = timestamp;

        var result = await _viajeService.IngestTelemetryAsync(usuarioId, viaje.Id, new TelemetryBatchRequest { Eventos = [evento] });

        var joined = string.Join("\n", _logger.Messages);
        Assert.NotEmpty(_logger.Messages);
        Assert.Contains("1 recibidos", joined);
        Assert.Contains("1 insertados", joined);
        Assert.DoesNotContain(lat.ToString(System.Globalization.CultureInfo.InvariantCulture), joined);
        Assert.DoesNotContain(lng.ToString(System.Globalization.CultureInfo.InvariantCulture), joined);
        Assert.DoesNotContain(eventId.ToString(), joined);
        Assert.DoesNotContain(timestamp.ToString("o"), joined);
        Assert.DoesNotContain(viaje.Id.ToString(), joined);
        Assert.Equal(1, result.Insertados);
    }
}
