using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class AppInviteRepository : IAppInviteRepository
{
    private readonly ApplicationDbContext _context;

    public AppInviteRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<AppInvite>> GetByUserAsync(Guid usuarioId)
    {
        return await _context.AppInvites
            .Where(i => i.UsuarioId == usuarioId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<AppInvite?> GetByIdAsync(Guid id)
    {
        return await _context.AppInvites.FindAsync(id);
    }

    public async Task<AppInvite?> GetByTokenAsync(string token)
    {
        return await _context.AppInvites
            .FirstOrDefaultAsync(i => i.Token == token);
    }

    public async Task<int> CountPendingByUserAsync(Guid usuarioId)
    {
        return await _context.AppInvites
            .CountAsync(i => i.UsuarioId == usuarioId && i.Status == "Pendiente de registro");
    }

    public async Task AddAsync(AppInvite invite)
    {
        await _context.AppInvites.AddAsync(invite);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(AppInvite invite)
    {
        _context.AppInvites.Update(invite);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(AppInvite invite)
    {
        _context.AppInvites.Remove(invite);
        await _context.SaveChangesAsync();
    }
}
