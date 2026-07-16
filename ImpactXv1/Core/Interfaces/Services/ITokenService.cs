using Prueba1.Core.Domain;

namespace Prueba1.Core.Interfaces.Services;

public interface ITokenService
{
    string GenerateAccessToken(Usuario usuario);
    string GenerateRefreshToken();
    string GeneratePasswordResetToken();
    string? GetPrincipalIdFromExpiredToken(string token);
}