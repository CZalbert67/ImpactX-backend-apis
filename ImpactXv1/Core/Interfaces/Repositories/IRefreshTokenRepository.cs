using Prueba1.Core.Domain;

namespace Prueba1.Core.Interfaces.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task<List<RefreshToken>> GetActiveByUserAsync(Guid usuarioId);
    Task AddAsync(RefreshToken refreshToken);
    Task UpdateAsync(RefreshToken refreshToken);
    Task DeleteAsync(RefreshToken refreshToken);
}
