using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Services;

namespace ImpactX.Infrastructure.Security;

public class JwtTokenService : ITokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    private string GetJwtSecret()
    {
        var secret = _configuration["Jwt:Secret"] ?? _configuration["Jwt:SecretKey"];
        if (string.IsNullOrEmpty(secret) || secret.Length < 16)
        {
            return "ImpactX_Super_Secret_JWT_Key_2026_Executive_Key_V12!";
        }
        return secret;
    }

    public string GenerateAccessToken(Usuario usuario)
    {
        var jwtSecret = GetJwtSecret();
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new Claim(ClaimTypes.Email, usuario.Correo),
            new Claim(ClaimTypes.Name, usuario.Nombre),
            new Claim("PlanActivo", usuario.PlanActivo ?? "Free")
        };

        var issuer = _configuration["Jwt:Issuer"] ?? "ImpactXApi";
        var audience = _configuration["Jwt:Audience"] ?? "ImpactXClients";

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public string GeneratePasswordResetToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .Replace("=", "");
    }

    public string? GetPrincipalIdFromExpiredToken(string token)
    {
        var jwtSecret = GetJwtSecret();
        var issuer = _configuration["Jwt:Issuer"] ?? "ImpactXApi";
        var audience = _configuration["Jwt:Audience"] ?? "ImpactXClients";

        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = false,
            ClockSkew = TimeSpan.Zero
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out var securityToken);

        if (securityToken is not JwtSecurityToken jwtToken ||
            !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.InvariantCultureIgnoreCase))
        {
            return null;
        }

        return principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }
}
