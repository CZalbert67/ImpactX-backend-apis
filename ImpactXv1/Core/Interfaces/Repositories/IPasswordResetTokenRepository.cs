using Prueba1.Core.Domain;

namespace Prueba1.Core.Interfaces.Repositories;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByTokenAsync(string token);
    Task AddAsync(PasswordResetToken resetToken);
    Task UpdateAsync(PasswordResetToken resetToken);
}
