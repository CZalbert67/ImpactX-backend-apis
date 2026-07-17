using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly Container _container;

    public CosmosRefreshTokenRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.RefreshTokens;
    }

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.token = @token")
            .WithParameter("@token", token);

        using var iterator = _container.GetItemQueryIterator<RefreshToken>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<List<RefreshToken>> GetActiveByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.revokedAt = null AND c.expiresAt > @now")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@now", DateTime.UtcNow.ToString("O"));

        var tokens = new List<RefreshToken>();
        using var iterator = _container.GetItemQueryIterator<RefreshToken>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            tokens.AddRange(response);
        }
        return tokens;
    }

    public async Task AddAsync(RefreshToken refreshToken)
    {
        refreshToken.Id = Guid.NewGuid();
        await _container.CreateItemAsync(refreshToken,
            new PartitionKey(refreshToken.UsuarioId.ToString()));
    }

    public async Task UpdateAsync(RefreshToken refreshToken)
    {
        await _container.UpsertItemAsync(refreshToken,
            new PartitionKey(refreshToken.UsuarioId.ToString()));
    }

    public async Task DeleteAsync(RefreshToken refreshToken)
    {
        await _container.DeleteItemAsync<RefreshToken>(
            refreshToken.Id.ToString(),
            new PartitionKey(refreshToken.UsuarioId.ToString()));
    }
}
