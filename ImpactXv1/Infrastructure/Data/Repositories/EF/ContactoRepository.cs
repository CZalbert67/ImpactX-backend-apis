using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class ContactoRepository : IContactoRepository
{
    private readonly ApplicationDbContext _context;

    public ContactoRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ContactoEmergencia>> GetByUserAsync(Guid usuarioId)
    {
        return await _context.ContactosEmergencia
            .Where(c => c.UsuarioId == usuarioId)
            .OrderByDescending(c => c.EsPrincipal)
            .ThenBy(c => c.CreadoEn)
            .ToListAsync();
    }

    public async Task<ContactoEmergencia?> GetByIdAsync(Guid id)
    {
        return await _context.ContactosEmergencia.FindAsync(id);
    }

    public async Task<ContactoEmergencia?> GetPrincipalAsync(Guid usuarioId)
    {
        return await _context.ContactosEmergencia
            .Where(c => c.UsuarioId == usuarioId && c.EsPrincipal)
            .FirstOrDefaultAsync();
    }

    public async Task<int> CountByUserAsync(Guid usuarioId)
    {
        return await _context.ContactosEmergencia
            .CountAsync(c => c.UsuarioId == usuarioId);
    }

    public async Task<bool> ExistsByTelefonoAsync(Guid usuarioId, string telefono)
    {
        return await _context.ContactosEmergencia
            .AnyAsync(c => c.UsuarioId == usuarioId && c.Telefono == telefono);
    }

    public async Task AddAsync(ContactoEmergencia contacto)
    {
        await _context.ContactosEmergencia.AddAsync(contacto);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(ContactoEmergencia contacto)
    {
        _context.ContactosEmergencia.Update(contacto);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(ContactoEmergencia contacto)
    {
        _context.ContactosEmergencia.Remove(contacto);
        await _context.SaveChangesAsync();
    }
}
