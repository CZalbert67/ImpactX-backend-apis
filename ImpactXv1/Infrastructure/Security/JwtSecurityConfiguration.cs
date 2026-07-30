using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace ImpactX.Infrastructure.Security;

public static class JwtSecurityConfiguration
{
    public static string GetRequiredSecret(IConfiguration configuration)
    {
        var secret = configuration["Jwt:Secret"];

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException(
                "JWT signing key is not configured. Set Jwt:Secret via environment variable, user secrets, or Azure Key Vault.");
        }

        if (Encoding.UTF8.GetByteCount(secret) < 32)
        {
            throw new InvalidOperationException(
                "JWT signing key must be at least 32 bytes when encoded as UTF-8.");
        }

        return secret;
    }

    public static SymmetricSecurityKey GetSigningKey(IConfiguration configuration)
    {
        var secret = GetRequiredSecret(configuration);
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }
}
