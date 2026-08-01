using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Infrastructure.Data;

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
        // Cross-partition justificada: búsqueda por token sin usuarioId
        // conocido; la partición es /usuarioId. Detención temprana.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.token = @token")
            .WithParameter("@token", token);

        using var iterator = _container.GetItemQueryIterator<RefreshToken>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
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
        using var iterator = _container.GetItemQueryIterator<RefreshToken>(query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 100
            });
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
            CosmosPartitionKeys.For(refreshToken.UsuarioId));
    }

    public async Task UpdateAsync(RefreshToken refreshToken)
    {
        await _container.ReplaceItemAsync(refreshToken,
            refreshToken.Id.ToString(),
            CosmosPartitionKeys.For(refreshToken.UsuarioId));
    }

    public async Task DeleteAsync(RefreshToken refreshToken)
    {
        await _container.DeleteItemAsync<RefreshToken>(
            refreshToken.Id.ToString(),
            CosmosPartitionKeys.For(refreshToken.UsuarioId));
    }

    public async Task RevokeAllByUsuarioIdAsync(Guid usuarioId, DateTime revokedAt, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.revokedAt = null")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var tokens = new List<RefreshToken>();
        using var iterator = _container.GetItemQueryIterator<RefreshToken>(query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 100
            });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            tokens.AddRange(response);
        }

        foreach (var token in tokens)
        {
            token.RevokedAt = revokedAt;
            await _container.ReplaceItemAsync(token,
                token.Id.ToString(),
                CosmosPartitionKeys.For(token.UsuarioId),
                cancellationToken: cancellationToken);
        }
    }
}
