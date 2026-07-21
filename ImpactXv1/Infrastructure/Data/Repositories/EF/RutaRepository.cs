using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class RutaRepository : IRutaRepository
{
    private readonly ApplicationDbContext _context;

    public RutaRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Ruta>> GetByUserAsync(Guid usuarioId)
    {
        return await _context.Rutas
            .Where(r => r.UsuarioId == usuarioId)
            .OrderByDescending(r => r.CreadoEn)
            .ToListAsync();
    }

    public async Task<List<Ruta>> GetFrequentByUserAsync(Guid usuarioId)
    {
        return await _context.Rutas
            .Where(r => r.UsuarioId == usuarioId && r.EsFrecuente)
            .OrderByDescending(r => r.UsadaEn)
            .ToListAsync();
    }

    public async Task<List<Ruta>> GetHistoryByUserAsync(Guid usuarioId)
    {
        return await _context.Rutas
            .Where(r => r.UsuarioId == usuarioId && !r.EsFrecuente)
            .OrderByDescending(r => r.CreadoEn)
            .Take(50)
            .ToListAsync();
    }

    public async Task<Ruta?> GetByIdAsync(Guid id)
    {
        return await _context.Rutas.FindAsync(id);
    }

    public async Task<Ruta?> GetSelectedTodayAsync(Guid usuarioId)
    {
        var todayStart = DateTime.UtcNow.Date;
        return await _context.Rutas
            .Where(r => r.UsuarioId == usuarioId && r.SeleccionadaHoy)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(Ruta ruta)
    {
        await _context.Rutas.AddAsync(ruta);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Ruta ruta)
    {
        _context.Rutas.Update(ruta);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Ruta ruta)
    {
        _context.Rutas.Remove(ruta);
        await _context.SaveChangesAsync();
    }
}
