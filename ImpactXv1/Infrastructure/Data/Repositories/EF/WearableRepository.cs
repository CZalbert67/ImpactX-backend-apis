using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class WearableRepository : IWearableRepository
{
    private readonly ApplicationDbContext _context;

    public WearableRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Wearable?> GetByIdAsync(Guid id)
    {
        return await _context.Wearables.FindAsync(id);
    }

    public async Task<Wearable?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        return await _context.Wearables
            .FirstOrDefaultAsync(w => w.UsuarioId == usuarioId && w.Id == id);
    }

    public async Task<Wearable?> GetByUsuarioIdAsync(Guid usuarioId)
    {
        return await _context.Wearables
            .Where(w => w.UsuarioId == usuarioId && w.Estado == "Vinculado")
            .FirstOrDefaultAsync();
    }

    public async Task<List<Wearable>> GetAllByUsuarioIdAsync(Guid usuarioId)
    {
        return await _context.Wearables
            .Where(w => w.UsuarioId == usuarioId)
            .ToListAsync();
    }

    public async Task<PagedResult<Wearable>> GetAllByUsuarioIdPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        return await EfPageReader.ReadSinglePageAsync(
            _context.Wearables
                .Where(w => w.UsuarioId == usuarioId)
                .OrderByDescending(w => w.VinculadoEn),
            pageSize, continuationToken, cancellationToken);
    }

    public async Task<Wearable?> GetByPairingTokenAsync(string token)
    {
        return await _context.Wearables
            .Where(w => w.PairingToken == token)
            .FirstOrDefaultAsync();
    }

    public async Task<Wearable?> GetByDispositivoIdAsync(string dispositivoId)
    {
        return await _context.Wearables
            .Where(w => w.DispositivoId == dispositivoId)
            .FirstOrDefaultAsync();
    }

    public async Task AddAsync(Wearable wearable)
    {
        await _context.Wearables.AddAsync(wearable);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Wearable wearable)
    {
        _context.Wearables.Update(wearable);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Wearable wearable)
    {
        _context.Wearables.Remove(wearable);
        await _context.SaveChangesAsync();
    }
}
