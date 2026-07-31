using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash);
    Task AddAsync(PasswordResetToken resetToken);
    Task UpdateAsync(PasswordResetToken resetToken);
    Task<int> InvalidateAllByUsuarioIdAsync(Guid usuarioId, DateTime invalidatedAt, CancellationToken cancellationToken = default);
}
