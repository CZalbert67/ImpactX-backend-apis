using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Identity;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class UsuarioRepository : IUsuarioRepository
{
    private readonly ApplicationDbContext _context;

    public UsuarioRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Usuario?> GetByIdAsync(Guid id)
    {
        return await _context.Usuarios.FindAsync(id);
    }

    public async Task<Usuario?> GetByCorreoAsync(string correo)
    {
        var normalized = EmailNormalizer.Normalize(correo);
        if (normalized.Length == 0)
            return null;

        return await _context.Usuarios.FirstOrDefaultAsync(u =>
            u.CorreoNormalizado == normalized || u.Correo.ToLower() == normalized);
    }

    public async Task<Usuario?> GetByUsernameAsync(string username)
    {
        var normalized = UsernamePolicy.Normalize(username);
        if (normalized is null)
            return null;

        return await _context.Usuarios.FirstOrDefaultAsync(u =>
            u.Username == normalized || u.Username.ToLower() == normalized);
    }

    public async Task<Usuario?> GetByPublicProfileIdAsync(string publicProfileId)
    {
        return await _context.Usuarios.FirstOrDefaultAsync(u => u.PublicProfileId == publicProfileId);
    }

    public async Task<List<Usuario>> SearchAsync(string query, string? by = null)
    {
        var lower = query.ToLowerInvariant();
        var mode = by?.Trim().ToLowerInvariant();

        var baseQuery = mode switch
        {
            "username" => _context.Usuarios.Where(u => u.Username.ToLower().Contains(lower)),
            "publicprofileid" => _context.Usuarios.Where(u => u.PublicProfileId.ToLower().Contains(lower)),
            _ => _context.Usuarios.Where(u => u.Username.ToLower().Contains(lower)
                     || u.Nombre.ToLower().Contains(lower)
                     || u.PublicProfileId.ToLower().Contains(lower))
        };

        return await baseQuery.Take(20).ToListAsync();
    }

    public async Task<bool> ExistsByCorreoAsync(string correo)
    {
        var normalized = EmailNormalizer.Normalize(correo);
        if (normalized.Length == 0)
            return false;

        return await _context.Usuarios.AnyAsync(u =>
            u.CorreoNormalizado == normalized || u.Correo.ToLower() == normalized);
    }

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        var normalized = UsernamePolicy.Normalize(username);
        if (normalized is null)
            return false;

        return await _context.Usuarios.AnyAsync(u => u.Username.ToLower() == normalized);
    }

    public async Task<bool> ExistsByPublicProfileIdAsync(string publicProfileId)
    {
        return await _context.Usuarios.AnyAsync(u => u.PublicProfileId == publicProfileId);
    }

    public async Task<bool> ExistsByUsernameIncludingHistoryAsync(string username)
    {
        var normalized = UsernamePolicy.Normalize(username);
        if (normalized is null)
            return false;

        return await _context.Usuarios.AnyAsync(u =>
            u.Username.ToLower() == normalized || u.UsernamesAnteriores.Contains(normalized));
    }

    public async Task<bool> ExistsByUsernameHistoryExcludingUsuarioAsync(string username, Guid usuarioId)
    {
        var normalized = UsernamePolicy.Normalize(username);
        if (normalized is null)
            return false;

        return await _context.Usuarios.AnyAsync(u =>
            u.Id != usuarioId && u.UsernamesAnteriores.Contains(normalized));
    }

    public async Task AddAsync(Usuario usuario)
    {
        await _context.Usuarios.AddAsync(usuario);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Usuario usuario)
    {
        _context.Usuarios.Update(usuario);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Usuario usuario)
    {
        _context.Usuarios.Remove(usuario);
        await _context.SaveChangesAsync();
    }
}
