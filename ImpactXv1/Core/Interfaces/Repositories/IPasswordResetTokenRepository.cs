using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByTokenAsync(string token);
    Task AddAsync(PasswordResetToken resetToken);
    Task UpdateAsync(PasswordResetToken resetToken);
}
