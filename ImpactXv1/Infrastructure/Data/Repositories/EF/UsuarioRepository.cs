using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
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
        return await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo);
    }

    public async Task<Usuario?> GetByUsernameAsync(string username)
    {
        return await _context.Usuarios.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<List<Usuario>> SearchAsync(string query)
    {
        var lower = query.ToLowerInvariant();
        return await _context.Usuarios
            .Where(u => u.Username.ToLower().Contains(lower)
                     || u.Nombre.ToLower().Contains(lower)
                     || u.AppId.ToLower().Contains(lower)
                     || u.Correo.ToLower().Contains(lower))
            .Take(20)
            .ToListAsync();
    }

    public async Task<bool> ExistsByCorreoAsync(string correo)
    {
        return await _context.Usuarios.AnyAsync(u => u.Correo == correo);
    }

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        return await _context.Usuarios.AnyAsync(u => u.Username == username);
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