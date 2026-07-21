using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class PagoRepository : IPagoRepository
{
    private readonly ApplicationDbContext _context;

    public PagoRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Pago>> GetByUserAsync(Guid usuarioId)
    {
        return await _context.Set<Pago>()
            .Where(p => p.UsuarioId == usuarioId)
            .OrderByDescending(p => p.FechaPago)
            .ToListAsync();
    }

    public async Task<Pago?> GetByIdAsync(Guid id)
    {
        return await _context.Set<Pago>().FindAsync(id);
    }

    public async Task AddAsync(Pago pago)
    {
        await _context.Set<Pago>().AddAsync(pago);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Pago pago)
    {
        _context.Set<Pago>().Update(pago);
        await _context.SaveChangesAsync();
    }
}
