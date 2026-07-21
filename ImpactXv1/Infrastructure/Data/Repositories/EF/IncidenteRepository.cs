using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class IncidenteRepository : IIncidenteRepository
{
    private readonly ApplicationDbContext _context;

    public IncidenteRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Incidente?> GetByIdAsync(Guid id)
    {
        return await _context.Incidentes.FindAsync(id);
    }

    public async Task<List<Incidente>> GetByUserAsync(Guid usuarioId)
    {
        return await _context.Incidentes
            .Where(i => i.UsuarioId == usuarioId)
            .OrderByDescending(i => i.CreadoEn)
            .ToListAsync();
    }

    public async Task<List<Incidente>> GetFilteredAsync(Guid usuarioId, string? severidad, DateTime? desde, DateTime? hasta, int pagina, int tamano)
    {
        var query = _context.Incidentes.Where(i => i.UsuarioId == usuarioId);

        if (!string.IsNullOrWhiteSpace(severidad))
            query = query.Where(i => i.Severidad == severidad);

        if (desde.HasValue)
            query = query.Where(i => i.CreadoEn >= desde.Value);

        if (hasta.HasValue)
            query = query.Where(i => i.CreadoEn <= hasta.Value);

        return await query
            .OrderByDescending(i => i.CreadoEn)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .ToListAsync();
    }

    public async Task<int> CountFilteredAsync(Guid usuarioId, string? severidad, DateTime? desde, DateTime? hasta)
    {
        var query = _context.Incidentes.Where(i => i.UsuarioId == usuarioId);

        if (!string.IsNullOrWhiteSpace(severidad))
            query = query.Where(i => i.Severidad == severidad);

        if (desde.HasValue)
            query = query.Where(i => i.CreadoEn >= desde.Value);

        if (hasta.HasValue)
            query = query.Where(i => i.CreadoEn <= hasta.Value);

        return await query.CountAsync();
    }

    public async Task AddAsync(Incidente incidente)
    {
        await _context.Incidentes.AddAsync(incidente);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Incidente incidente)
    {
        _context.Incidentes.Update(incidente);
        await _context.SaveChangesAsync();
    }

    public async Task<int> CountByUserAsync(Guid usuarioId)
    {
        return await _context.Incidentes.CountAsync(i => i.UsuarioId == usuarioId);
    }
}
