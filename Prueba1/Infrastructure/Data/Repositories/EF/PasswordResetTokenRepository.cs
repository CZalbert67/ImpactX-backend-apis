using Microsoft.EntityFrameworkCore;
using Prueba1.Core.Domain;
using Prueba1.Core.Interfaces.Repositories;

namespace Prueba1.Infrastructure.Data.Repositories.EF;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly ApplicationDbContext _context;

    public PasswordResetTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PasswordResetToken?> GetByTokenAsync(string token)
    {
        return await _context.PasswordResetTokens
            .FirstOrDefaultAsync(p => p.Token == token);
    }

    public async Task AddAsync(PasswordResetToken resetToken)
    {
        await _context.PasswordResetTokens.AddAsync(resetToken);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(PasswordResetToken resetToken)
    {
        _context.PasswordResetTokens.Update(resetToken);
        await _context.SaveChangesAsync();
    }
}
