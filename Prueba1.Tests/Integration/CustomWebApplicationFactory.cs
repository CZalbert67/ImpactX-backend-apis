using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Prueba1.Tests.Integration;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("UseInMemoryDatabase", "true");

        builder.UseSetting("Jwt:Secret", "test-secret-key-that-is-at-least-32-characters-long-for-hmac");
        builder.UseSetting("Jwt:Issuer", "ImpactX-Test");
        builder.UseSetting("Jwt:Audience", "ImpactX-Client-Test");
    }
}
