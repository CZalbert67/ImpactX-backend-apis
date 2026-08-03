using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ImpactX.Tests.Integration;

internal static class TestJwtBuilder
{
    internal static string Create(Guid userId, string? client = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString())
        };

        if (!string.IsNullOrWhiteSpace(client))
        {
            claims.Add(new Claim("client", client));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestJwtConfiguration.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: "ImpactX-Test",
            audience: "ImpactX-Client-Test",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
