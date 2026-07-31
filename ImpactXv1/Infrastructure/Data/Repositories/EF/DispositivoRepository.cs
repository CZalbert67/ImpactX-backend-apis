using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

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
        var dispositivos = await _context.Dispositivos
            .Where(d => d.UsuarioId == usuarioId)
            .ToListAsync(cancellationToken);

        _context.Dispositivos.RemoveRange(dispositivos);
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
