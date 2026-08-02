using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using ImpactX.Core.Telemetry;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class ViajeRepository : IViajeRepository
{
    private readonly ApplicationDbContext _context;

    public ViajeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Viaje?> GetByIdAsync(Guid id)
    {
        return await _context.Viajes.FindAsync(id);
    }

    public async Task<Viaje?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        return await _context.Viajes
            .FirstOrDefaultAsync(v => v.UsuarioId == usuarioId && v.Id == id);
    }

    public async Task<Viaje?> GetActiveByUserAsync(Guid usuarioId)
    {
        return await _context.Viajes
            .Where(v => v.UsuarioId == usuarioId && v.Estado == "Activo")
            .FirstOrDefaultAsync();
    }

    public async Task<List<Viaje>> GetByUserAsync(Guid usuarioId)
    {
        return await _context.Viajes
            .Where(v => v.UsuarioId == usuarioId)
            .OrderByDescending(v => v.Inicio)
            .Take(50)
            .ToListAsync();
    }

    public async Task<PagedResult<Viaje>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        return await EfPageReader.ReadSinglePageAsync(
            _context.Viajes
                .Where(v => v.UsuarioId == usuarioId)
                .OrderByDescending(v => v.Inicio),
            pageSize, continuationToken, cancellationToken);
    }

    public async Task AddAsync(Viaje viaje)
    {
        await _context.Viajes.AddAsync(viaje);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Viaje viaje)
    {
        _context.Viajes.Update(viaje);
        await _context.SaveChangesAsync();
    }

    public async Task AddTelemetryAsync(ViajeTelemetry telemetry)
    {
        await _context.ViajeTelemetries.AddAsync(telemetry);
        await _context.SaveChangesAsync();
    }

    public async Task<ViajeTelemetry?> GetTelemetryByEventIdAsync(Guid viajeId, Guid eventId, CancellationToken cancellationToken = default)
    {
        // Lectura punto a punto equivalente al point-read de Cosmos:
        // filtro por viaje (partición) e id del evento, sin barridos globales.
        return await _context.ViajeTelemetries
            .FirstOrDefaultAsync(t => t.ViajeId == viajeId && t.Id == eventId, cancellationToken);
    }

    public async Task<TelemetryBatchWriteResult> AddTelemetryBatchAsync(Guid viajeId, IReadOnlyList<ViajeTelemetry> eventos, CancellationToken cancellationToken = default)
    {
        // Misma semántica observable que Cosmos: un solo SaveChangesAsync es
        // una sola transacción (todos o ninguno); los eventos que ya existen
        // se resuelven como duplicado idéntico o conflicto real (409); un
        // EventId existente nunca se sobrescribe ni se duplica.
        var pendientes = new List<ViajeTelemetry>(eventos.Count);
        var duplicados = 0;

        foreach (var evento in eventos)
        {
            var existente = await _context.ViajeTelemetries
                .FirstOrDefaultAsync(t => t.ViajeId == viajeId && t.Id == evento.Id, cancellationToken);

            if (existente is null)
            {
                pendientes.Add(evento);
            }
            else if (TelemetryEventEquality.IsIdentical(existente, evento))
            {
                duplicados++;
            }
            else
            {
                throw new ConflictException("El evento ya existe con contenido diferente.");
            }
        }

        if (pendientes.Count > 0)
        {
            await _context.ViajeTelemetries.AddRangeAsync(pendientes, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new TelemetryBatchWriteResult { Insertados = pendientes.Count, Duplicados = duplicados };
    }

    public async Task<List<ViajeTelemetry>> GetTelemetryByViajeAsync(Guid viajeId)
    {
        return await _context.ViajeTelemetries
            .Where(t => t.ViajeId == viajeId)
            .OrderBy(t => t.Timestamp)
            .ToListAsync();
    }

    public async Task<PagedResult<ViajeTelemetry>> GetTelemetryByViajePagedAsync(Guid viajeId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        return await EfPageReader.ReadSinglePageAsync(
            _context.ViajeTelemetries
                .Where(t => t.ViajeId == viajeId)
                .OrderBy(t => t.Timestamp),
            pageSize, continuationToken, cancellationToken);
    }
}
