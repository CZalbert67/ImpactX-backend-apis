using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

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

    public async Task<Notificacion?> GetByIdAsync(Guid id)
    {
        return await _context.Notificaciones.FindAsync(id);
    }

    public async Task<int> CountUnreadByUserAsync(Guid usuarioId)
    {
        return await _context.Notificaciones
            .CountAsync(n => n.UsuarioId == usuarioId && !n.Leida);
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

    public async Task MarkAllAsReadAsync(Guid usuarioId)
    {
        var now = DateTime.UtcNow;
        var pendientes = await _context.Notificaciones
            .Where(n => n.UsuarioId == usuarioId && !n.Leida)
            .ToListAsync();

        foreach (var n in pendientes)
        {
            n.Leida = true;
            n.LeidaEn = now;
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Notificacion notificacion)
    {
        _context.Notificaciones.Remove(notificacion);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAllByUserAsync(Guid usuarioId)
    {
        var notificaciones = await _context.Notificaciones
            .Where(n => n.UsuarioId == usuarioId)
            .ToListAsync();

        _context.Notificaciones.RemoveRange(notificaciones);
        await _context.SaveChangesAsync();
    }
}
