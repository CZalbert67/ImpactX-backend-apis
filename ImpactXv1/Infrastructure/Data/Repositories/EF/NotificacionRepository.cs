using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class NotificacionRepository : INotificacionRepository
{
    private readonly ApplicationDbContext _context;

    public NotificacionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Notificacion>> GetByUserAsync(Guid usuarioId)
    {
        return await _context.Notificaciones
            .Where(n => n.UsuarioId == usuarioId)
            .OrderByDescending(n => n.CreadoEn)
            .ToListAsync();
    }

    public async Task<PagedResult<Notificacion>> GetByUserPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        return await EfPageReader.ReadSinglePageAsync(
            _context.Notificaciones
                .Where(n => n.UsuarioId == usuarioId)
                .OrderByDescending(n => n.CreadoEn),
            pageSize, continuationToken, cancellationToken);
    }

    public async Task<Notificacion?> GetByIdAsync(Guid id)
    {
        return await _context.Notificaciones.FindAsync(id);
    }

    public async Task<Notificacion?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        return await _context.Notificaciones
            .FirstOrDefaultAsync(n => n.UsuarioId == usuarioId && n.Id == id);
    }

    public async Task<int> CountUnreadByUserAsync(Guid usuarioId)
    {
        return await _context.Notificaciones
            .CountAsync(n => n.UsuarioId == usuarioId && !n.Leida);
    }

    public async Task<Notificacion?> GetByIdempotencyKeyAsync(string key, Guid? recipientUserId = null, CancellationToken cancellationToken = default)
    {
        return await _context.Notificaciones
            .Where(n => n.ClaveIdempotencia == key)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Notificacion notificacion)
    {
        await _context.Notificaciones.AddAsync(notificacion);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Notificacion notificacion)
    {
        _context.Notificaciones.Update(notificacion);
        await _context.SaveChangesAsync();
    }

    public async Task MarkAllAsReadAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        // Proceso completo incremental: página por página, sin acumular todas
        // las notificaciones en memoria.
        var now = DateTime.UtcNow;
        var offset = 0;
        const int pageSize = PaginationDefaults.MaxPageSize;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pendientes = await _context.Notificaciones
                .Where(n => n.UsuarioId == usuarioId && !n.Leida)
                .OrderByDescending(n => n.CreadoEn)
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            if (pendientes.Count == 0)
                break;

            foreach (var n in pendientes)
            {
                n.Leida = true;
                n.LeidaEn = now;
            }

            await _context.SaveChangesAsync(cancellationToken);

            offset += pendientes.Count;
            if (pendientes.Count < pageSize)
                break;
        }
    }

    public async Task DeleteAsync(Notificacion notificacion)
    {
        _context.Notificaciones.Remove(notificacion);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAllByUserAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        // Proceso completo incremental: página por página, sin acumular todas
        // las notificaciones en memoria.
        var offset = 0;
        const int pageSize = PaginationDefaults.MaxPageSize;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var notificaciones = await _context.Notificaciones
                .Where(n => n.UsuarioId == usuarioId)
                .OrderByDescending(n => n.CreadoEn)
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            if (notificaciones.Count == 0)
                break;

            _context.Notificaciones.RemoveRange(notificaciones);
            await _context.SaveChangesAsync(cancellationToken);

            offset += notificaciones.Count;
            if (notificaciones.Count < pageSize)
                break;
        }
    }
}
