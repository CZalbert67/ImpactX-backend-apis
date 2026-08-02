using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Services;

public interface IClientAwareTokenService : ITokenService
{
    string GenerateAccessToken(Usuario usuario, string client);
}
