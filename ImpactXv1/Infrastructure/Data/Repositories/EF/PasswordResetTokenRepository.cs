using Microsoft.EntityFrameworkCore;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;

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
        // Proceso completo incremental: página por página, sin acumular todos
        // los tokens en memoria.
        var invalidated = 0;
        var offset = 0;
        const int pageSize = PaginationDefaults.MaxPageSize;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var activeTokens = await _context.PasswordResetTokens
                .Where(p => p.UsuarioId == usuarioId && p.UsedAt == null && p.ExpiresAt > DateTime.UtcNow)
                .OrderBy(p => p.Id)
                .Skip(offset)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            if (activeTokens.Count == 0)
                break;

            foreach (var token in activeTokens)
            {
                token.UsedAt = invalidatedAt;
            }

            invalidated += await _context.SaveChangesAsync(cancellationToken);

            offset += activeTokens.Count;
            if (activeTokens.Count < pageSize)
                break;
        }

        return invalidated;
    }
}
