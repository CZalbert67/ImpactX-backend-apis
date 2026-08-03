using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class AlertaRepository : IAlertaRepository
{
    private readonly ApplicationDbContext _context;

    public AlertaRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Alerta?> GetByIdAsync(Guid id)
    {
        return await _context.Alertas.FindAsync(id);
    }

    public async Task<Alerta?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        return await _context.Alertas
            .FirstOrDefaultAsync(a => a.UsuarioId == usuarioId && a.Id == id);
    }

    public async Task<List<Alerta>> GetByUserAsync(Guid usuarioId)
    {
        return await _context.Alertas
            .Where(a => a.UsuarioId == usuarioId)
            .OrderByDescending(a => a.CreadoEn)
            .ToListAsync();
    }

    public async Task<PagedResult<Alerta>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        return await EfPageReader.ReadSinglePageAsync(
            _context.Alertas
                .Where(a => a.UsuarioId == usuarioId)
                .OrderByDescending(a => a.CreadoEn),
            pageSize, continuationToken, cancellationToken);
    }

    public async Task<Alerta?> GetActiveByUserAsync(Guid usuarioId)
    {
        return await _context.Alertas
            .Where(a => a.UsuarioId == usuarioId && a.Estado != "Cerrada" && a.Estado != "Atendida" && a.Estado != "FalsaAlarma")
            .OrderByDescending(a => a.CreadoEn)
            .FirstOrDefaultAsync();
    }

    public async Task<Alerta?> GetBySourceTelemetryEventIdAsync(
        Guid usuarioId,
        Guid sourceTelemetryEventId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Alertas
            .FirstOrDefaultAsync(
                a => a.UsuarioId == usuarioId
                    && a.SourceTelemetryEventId == sourceTelemetryEventId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Alerta>> GetPendingDueAsync(
        DateTime utcNow,
        int maxCount,
        CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(maxCount, 1, 500);
        return await _context.Alertas
            .Where(a => a.Estado == "Pendiente"
                && a.AutoSendAtUtc != null
                && a.AutoSendAtUtc <= utcNow)
            .OrderBy(a => a.AutoSendAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Alerta>> GetPendingByUserAsync(Guid usuarioId)
    {
        return await _context.Alertas
            .Where(a => a.UsuarioId == usuarioId && a.Estado == "Pendiente")
            .OrderByDescending(a => a.CreadoEn)
            .ToListAsync();
    }

    public async Task<List<Alerta>> GetActiveAlertsAsync(Guid usuarioId)
    {
        return await _context.Alertas
            .Where(a => a.UsuarioId == usuarioId && (a.Estado == "Pendiente" || a.Estado == "Enviada" || a.Estado == "Activa"))
            .OrderByDescending(a => a.CreadoEn)
            .ToListAsync();
    }

    public async Task AddAsync(Alerta alerta)
    {
        await _context.Alertas.AddAsync(alerta);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Alerta alerta)
    {
        _context.Alertas.Update(alerta);
        await _context.SaveChangesAsync();
    }

    public async Task<int> CountByUserAsync(Guid usuarioId)
    {
        return await _context.Alertas.CountAsync(a => a.UsuarioId == usuarioId);
    }
}
