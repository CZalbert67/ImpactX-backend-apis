using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class DispositivoRepository : IDispositivoRepository
{
    private readonly ApplicationDbContext _context;

    public DispositivoRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Dispositivo>> GetByUsuarioIdAsync(Guid usuarioId)
    {
        return await _context.Dispositivos
            .Where(d => d.UsuarioId == usuarioId)
            .OrderByDescending(d => d.ActualizadoEn)
            .ToListAsync();
    }

    public async Task<List<Dispositivo>> GetActiveByUsuarioIdAsync(Guid usuarioId)
    {
        return await _context.Dispositivos
            .Where(d => d.UsuarioId == usuarioId && d.Activo)
            .ToListAsync();
    }

    public async Task<PagedResult<Dispositivo>> GetByUsuarioIdPagedAsync(Guid usuarioId, int pageSize, string? continuationToken, CancellationToken cancellationToken = default)
    {
        return await EfPageReader.ReadSinglePageAsync(
            _context.Dispositivos
                .Where(d => d.UsuarioId == usuarioId)
                .OrderByDescending(d => d.ActualizadoEn),
            pageSize, continuationToken, cancellationToken);
    }

    public async Task<Dispositivo?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        return await _context.Dispositivos
            .FirstOrDefaultAsync(d => d.UsuarioId == usuarioId && d.Id == id);
    }

    public async Task<Dispositivo?> GetByDeviceIdAsync(Guid usuarioId, string deviceId)
    {
        return await _context.Dispositivos
            .FirstOrDefaultAsync(d => d.UsuarioId == usuarioId && d.DeviceId == deviceId);
    }

    public async Task<Dispositivo?> GetByTokenFcmAsync(string tokenFcm)
    {
        return await _context.Dispositivos
            .FirstOrDefaultAsync(d => d.TokenFcm == tokenFcm);
    }

    public async Task AddAsync(Dispositivo dispositivo)
    {
        await _context.Dispositivos.AddAsync(dispositivo);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Dispositivo dispositivo)
    {
        _context.Dispositivos.Update(dispositivo);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Dispositivo dispositivo)
    {
        _context.Dispositivos.Remove(dispositivo);
        await _context.SaveChangesAsync();
    }

    public async Task<int> DeleteAllByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default)
    {
        // Proceso completo incremental: página por página, sin acumular todos
        // los dispositivos en memoria.
        var deleted = 0;
        var offset = 0;
        const int pageSize = PaginationDefaults.MaxPageSize;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dispositivos = await _context.Dispositivos
                .Where(d => d.UsuarioId == usuarioId)
                .OrderBy(d => d.Id)
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            if (dispositivos.Count == 0)
                break;

            _context.Dispositivos.RemoveRange(dispositivos);
            deleted += await _context.SaveChangesAsync(cancellationToken);

            offset += dispositivos.Count;
            if (dispositivos.Count < pageSize)
                break;
        }

        return deleted;
    }
}
