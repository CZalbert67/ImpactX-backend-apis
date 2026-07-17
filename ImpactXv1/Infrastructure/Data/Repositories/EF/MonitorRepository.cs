using Microsoft.EntityFrameworkCore;
using Monitor = ImpactX.Core.Domain.Monitor;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class MonitorRepository : IMonitorRepository
{
    private readonly ApplicationDbContext _context;

    public MonitorRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Monitor>> GetByUserAsync(Guid usuarioId)
    {
        return await _context.Monitores
            .Where(m => m.UsuarioId == usuarioId)
            .OrderByDescending(m => m.CreadoEn)
            .ToListAsync();
    }

    public async Task<Monitor?> GetByIdAsync(Guid id)
    {
        return await _context.Monitores.FindAsync(id);
    }

    public async Task<List<Monitor>> GetActiveByUserAsync(Guid usuarioId)
    {
        return await _context.Monitores
            .Where(m => m.UsuarioId == usuarioId && m.Estado == "Activo")
            .ToListAsync();
    }

    public async Task<int> CountActiveByUserAsync(Guid usuarioId)
    {
        return await _context.Monitores
            .CountAsync(m => m.UsuarioId == usuarioId && m.Estado == "Activo");
    }

    public async Task<Monitor?> GetByTokenAsync(string token)
    {
        return await _context.Monitores
            .Where(m => m.TokenInvitacion == token)
            .FirstOrDefaultAsync();
    }

    public async Task<Monitor?> GetByUsuarioYMonitorAsync(Guid usuarioId, Guid monitorUsuarioId)
    {
        return await _context.Monitores
            .Where(m => m.UsuarioId == usuarioId && m.ProfileId == monitorUsuarioId.ToString())
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ExistsByUsernameAsync(Guid usuarioId, string username)
    {
        return await _context.Monitores
            .AnyAsync(m => m.UsuarioId == usuarioId && m.Username == username && m.Estado != "Revocado");
    }

    public async Task AddAsync(Monitor monitor)
    {
        await _context.Monitores.AddAsync(monitor);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Monitor monitor)
    {
        _context.Monitores.Update(monitor);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Monitor monitor)
    {
        _context.Monitores.Remove(monitor);
        await _context.SaveChangesAsync();
    }
}
