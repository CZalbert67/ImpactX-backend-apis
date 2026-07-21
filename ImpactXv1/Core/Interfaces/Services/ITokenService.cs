using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Services;

public interface ITokenService
{
    string GenerateAccessToken(Usuario usuario);
    string GenerateRefreshToken();
    string GeneratePasswordResetToken();
    string? GetPrincipalIdFromExpiredToken(string token);
}