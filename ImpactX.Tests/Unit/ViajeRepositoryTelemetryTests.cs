using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Infrastructure.Data;
using ImpactX.Infrastructure.Data.Repositories.EF;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Tests.Unit;

public class ViajeRepositoryTelemetryTests
{
    private static ViajeRepository CreateRepo(out ApplicationDbContext context)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"viaje-repo-telemetry-{Guid.NewGuid():N}")
            .Options;
        context = new ApplicationDbContext(options);
        return new ViajeRepository(context);
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

    [Fact]
    public async Task GetTelemetryByEventIdAsync_ReturnsPointWithinViaje()
    {
        var repo = CreateRepo(out var context);
        var viajeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var evento = Evento(viajeId, eventId);
        await context.ViajeTelemetries.AddAsync(evento);
        await context.SaveChangesAsync();

        var resultado = await repo.GetTelemetryByEventIdAsync(viajeId, eventId);

        Assert.NotNull(resultado);
        Assert.Equal(eventId, resultado!.Id);
    }

    [Fact]
    public async Task GetTelemetryByEventIdAsync_SameEventIdOtherViaje_ReturnsNull()
    {
        var repo = CreateRepo(out var context);
        var eventId = Guid.NewGuid();
        await context.ViajeTelemetries.AddAsync(Evento(Guid.NewGuid(), eventId));
        await context.SaveChangesAsync();

        var resultado = await repo.GetTelemetryByEventIdAsync(Guid.NewGuid(), eventId);

        Assert.Null(resultado);
    }

    [Fact]
    public async Task AddTelemetryBatchAsync_InsertsNewEvents()
    {
        var repo = CreateRepo(out var context);
        var viajeId = Guid.NewGuid();
        var eventos = new[] { Evento(viajeId, Guid.NewGuid()), Evento(viajeId, Guid.NewGuid()) };

        var resultado = await repo.AddTelemetryBatchAsync(viajeId, eventos);

        Assert.Equal(2, resultado.Insertados);
        Assert.Equal(0, resultado.Duplicados);
        Assert.Equal(2, await context.ViajeTelemetries.CountAsync(t => t.ViajeId == viajeId));
    }

    [Fact]
    public async Task AddTelemetryBatchAsync_IdenticalDuplicate_NotInserted()
    {
        var repo = CreateRepo(out var context);
        var viajeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);
        await context.ViajeTelemetries.AddAsync(Evento(viajeId, eventId, timestamp: timestamp));
        await context.SaveChangesAsync();

        var resultado = await repo.AddTelemetryBatchAsync(viajeId, new[] { Evento(viajeId, eventId, timestamp: timestamp) });

        Assert.Equal(0, resultado.Insertados);
        Assert.Equal(1, resultado.Duplicados);
        Assert.Equal(1, await context.ViajeTelemetries.CountAsync(t => t.ViajeId == viajeId && t.Id == eventId));
    }

    [Fact]
    public async Task AddTelemetryBatchAsync_MixedBatch_PartialDuplicatesWithoutDuplication()
    {
        var repo = CreateRepo(out var context);
        var viajeId = Guid.NewGuid();
        var dupId = Guid.NewGuid();
        var nuevoId = Guid.NewGuid();
        var timestamp = DateTime.UtcNow.AddMinutes(-1);
        await context.ViajeTelemetries.AddAsync(Evento(viajeId, dupId, timestamp: timestamp));
        await context.SaveChangesAsync();

        var resultado = await repo.AddTelemetryBatchAsync(viajeId,
            new[] { Evento(viajeId, dupId, timestamp: timestamp), Evento(viajeId, nuevoId, timestamp: timestamp) });

        Assert.Equal(1, resultado.Insertados);
        Assert.Equal(1, resultado.Duplicados);
        Assert.Equal(2, await context.ViajeTelemetries.CountAsync(t => t.ViajeId == viajeId));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task AddTelemetryBatchAsync_DifferentContentSameEventId_ThrowsConflict_NoOverwrite()
    {
        var repo = CreateRepo(out var context);
        var viajeId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        await context.ViajeTelemetries.AddAsync(Evento(viajeId, eventId, lat: 19.43));
        await context.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() =>
            repo.AddTelemetryBatchAsync(viajeId, new[] { Evento(viajeId, eventId, lat: 20.0) }));

        var persistido = await context.ViajeTelemetries.SingleAsync(t => t.Id == eventId);
        Assert.Equal(19.43, persistido.Lat);
    }

    [Fact]
    public async Task AddTelemetryBatchAsync_Empty_NoOp()
    {
        var repo = CreateRepo(out var context);

        var resultado = await repo.AddTelemetryBatchAsync(Guid.NewGuid(), []);

        Assert.Equal(0, resultado.Insertados);
        Assert.Equal(0, resultado.Duplicados);
        Assert.Equal(0, await context.ViajeTelemetries.CountAsync());
    }
}
