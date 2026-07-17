using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class SuscripcionRepository : ISuscripcionRepository
{
    private readonly ApplicationDbContext _context;

    public SuscripcionRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Suscripcion?> GetActiveByUserAsync(Guid usuarioId)
    {
        return await _context.Set<Suscripcion>()
            .Where(s => s.UsuarioId == usuarioId && (s.Estado == "Trial" || s.Estado == "Activa"))
            .OrderByDescending(s => s.Inicio)
            .FirstOrDefaultAsync();
    }

    public async Task<List<Suscripcion>> GetHistoryByUserAsync(Guid usuarioId)
    {
        return await _context.Set<Suscripcion>()
            .Where(s => s.UsuarioId == usuarioId)
            .OrderByDescending(s => s.Inicio)
            .ToListAsync();
    }

    public async Task<Suscripcion?> GetByIdAsync(Guid id)
    {
        return await _context.Set<Suscripcion>().FindAsync(id);
    }

    public async Task AddAsync(Suscripcion suscripcion)
    {
        await _context.Set<Suscripcion>().AddAsync(suscripcion);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Suscripcion suscripcion)
    {
        _context.Set<Suscripcion>().Update(suscripcion);
        await _context.SaveChangesAsync();
    }

    public async Task<List<Suscripcion>> GetExpiredAsync()
    {
        var now = DateTime.UtcNow;
        return await _context.Set<Suscripcion>()
            .Where(s => (s.Estado == "Activa" || s.Estado == "Trial") && s.Fin != null && s.Fin <= now)
            .ToListAsync();
    }

    public async Task<List<Suscripcion>> GetTrialsEndingAsync(int daysRemaining)
    {
        var threshold = DateTime.UtcNow.AddDays(daysRemaining);
        return await _context.Set<Suscripcion>()
            .Where(s => s.Estado == "Trial" && s.TrialFin != null && s.TrialFin <= threshold)
            .ToListAsync();
    }
}
