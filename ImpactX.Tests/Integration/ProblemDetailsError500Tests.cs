using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Infrastructure.Data;
using ImpactX.Infrastructure.Data.Repositories.EF;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace ImpactX.Tests.Integration;

public class ProblemDetailsError500Tests : IDisposable
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    private sealed class ThrowingTokenService : ITokenService
    {
        public string GenerateAccessToken(Usuario usuario)
            => throw new InvalidOperationException("Unexpected token generation failure.");

        public string GenerateRefreshToken()
            => throw new InvalidOperationException("Unexpected token generation failure.");

        public string GeneratePasswordResetToken()
            => throw new InvalidOperationException("Unexpected token generation failure.");

        public string? GetPrincipalIdFromExpiredToken(string token) => null;
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task InternalServerError_Real500_ReturnsProblemDetails()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseCosmosDb", "false");
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.UseSetting("Jwt:Secret", TestJwtConfiguration.Secret);
                builder.UseSetting("Jwt:Issuer", "ImpactX-Test");
                builder.UseSetting("Jwt:Audience", "ImpactX-Client-Test");
                builder.UseSetting("RateLimiting:Auth:RegisterPerMinute", "1000");

                builder.ConfigureServices(services =>
                {
                    services.AddScoped<ITokenService>(_ => new ThrowingTokenService());
                });
            });
        _client = _factory.CreateClient();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "500 Error Tester",
            correo = $"error500_{Guid.NewGuid()}@test.com",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var type = root.GetProperty("type").GetString();
        Assert.Contains("internal-server-error", type, StringComparison.OrdinalIgnoreCase);

        var title = root.GetProperty("title").GetString();
        Assert.Equal("Internal Server Error", title);

        Assert.Equal(500, root.GetProperty("status").GetInt32());

        if (root.TryGetProperty("detail", out var detail))
        {
            Assert.Null(detail.GetString());
        }

        Assert.True(root.TryGetProperty("traceId", out _));
        Assert.True(root.TryGetProperty("correlationId", out _));

        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", body, StringComparison.Ordinal);
        Assert.DoesNotContain("Unexpected token generation failure", body, StringComparison.Ordinal);
        Assert.DoesNotContain("InvalidOperationException", body, StringComparison.Ordinal);
    }

    private sealed class ThrowingRepository : IUsuarioRepository
    {
        private readonly IUsuarioRepository _inner;
        public ThrowingRepository(IUsuarioRepository inner) => _inner = inner;

        public Task<Usuario?> GetByIdAsync(Guid id) => _inner.GetByIdAsync(id);
        public Task<Usuario?> GetByCorreoAsync(string correo) => _inner.GetByCorreoAsync(correo);
        public Task<Usuario?> GetByUsernameAsync(string username) => _inner.GetByUsernameAsync(username);
        public Task<Usuario?> GetByPublicProfileIdAsync(string publicProfileId) => _inner.GetByPublicProfileIdAsync(publicProfileId);
        public Task<List<Usuario>> SearchAsync(string query, string? by = null) => _inner.SearchAsync(query, by);
        public Task<bool> ExistsByCorreoAsync(string correo) => _inner.ExistsByCorreoAsync(correo);
        public Task<bool> ExistsByUsernameAsync(string username) => _inner.ExistsByUsernameAsync(username);
        public Task<bool> ExistsByPublicProfileIdAsync(string publicProfileId) => _inner.ExistsByPublicProfileIdAsync(publicProfileId);
        public Task<bool> ExistsByUsernameIncludingHistoryAsync(string username) => _inner.ExistsByUsernameIncludingHistoryAsync(username);
        public Task<bool> ExistsByUsernameHistoryExcludingUsuarioAsync(string username, Guid usuarioId) => _inner.ExistsByUsernameHistoryExcludingUsuarioAsync(username, usuarioId);
        public async Task AddAsync(Usuario usuario)
        {
            await _inner.AddAsync(usuario);
            throw new ArgumentException("Secret internal argument error.");
        }
        public Task UpdateAsync(Usuario usuario) => _inner.UpdateAsync(usuario);
        public Task DeleteAsync(Usuario usuario) => _inner.DeleteAsync(usuario);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ArgumentException_Returns500_NotExposeMessage()
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("UseCosmosDb", "false");
                builder.UseSetting("UseInMemoryDatabase", "true");
                builder.UseSetting("Jwt:Secret", TestJwtConfiguration.Secret);
                builder.UseSetting("Jwt:Issuer", "ImpactX-Test");
                builder.UseSetting("Jwt:Audience", "ImpactX-Client-Test");
                builder.UseSetting("RateLimiting:Auth:RegisterPerMinute", "1000");

                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IUsuarioRepository));
                    if (descriptor != null) services.Remove(descriptor);
                    services.AddScoped<IUsuarioRepository>(sp =>
                    {
                        var inner = ActivatorUtilities.CreateInstance<UsuarioRepository>(sp);
                        return new ThrowingRepository(inner);
                    });
                });
            });
        _client = _factory.CreateClient();

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            nombre = "ArgException Tester",
            correo = $"argex_{Guid.NewGuid()}@test.com",
            password = "Password123!"
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var type = root.GetProperty("type").GetString();
        Assert.Contains("internal-server-error", type, StringComparison.OrdinalIgnoreCase);

        Assert.Equal(500, root.GetProperty("status").GetInt32());

        if (root.TryGetProperty("detail", out var detail))
        {
            Assert.Null(detail.GetString());
        }

        Assert.True(root.TryGetProperty("traceId", out _));
        Assert.True(root.TryGetProperty("correlationId", out _));

        Assert.DoesNotContain("Secret internal argument error", body, StringComparison.Ordinal);
        Assert.DoesNotContain("ArgumentException", body, StringComparison.Ordinal);
        Assert.DoesNotContain("stackTrace", body, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        _factory?.Dispose();
    }
}
