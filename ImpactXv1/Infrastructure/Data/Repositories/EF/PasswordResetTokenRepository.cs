using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly ApplicationDbContext _context;

    public PasswordResetTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash)
    {
        return await _context.PasswordResetTokens
            .FirstOrDefaultAsync(p => p.TokenHash == tokenHash);
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

    public async Task<int> InvalidateAllByUsuarioIdAsync(Guid usuarioId, DateTime invalidatedAt, CancellationToken cancellationToken = default)
    {
        var activeTokens = await _context.PasswordResetTokens
            .Where(p => p.UsuarioId == usuarioId && p.UsedAt == null && p.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.UsedAt = invalidatedAt;
        }

        return await _context.SaveChangesAsync(cancellationToken);
    }
}
