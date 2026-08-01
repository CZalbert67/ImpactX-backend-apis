using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Infrastructure.Data;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosPasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly Container _container;

    public CosmosPasswordResetTokenRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.PasswordResetTokens;
    }

    public async Task<PasswordResetToken?> GetByTokenHashAsync(string tokenHash)
    {
        // Cross-partition justificada: búsqueda por hash sin usuarioId
        // conocido; la partición es /usuarioId. Detención temprana.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.tokenHash = @tokenHash")
            .WithParameter("@tokenHash", tokenHash);

        using var iterator = _container.GetItemQueryIterator<PasswordResetToken>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task AddAsync(PasswordResetToken resetToken)
    {
        resetToken.Id = Guid.NewGuid();
        await _container.CreateItemAsync(resetToken,
            CosmosPartitionKeys.For(resetToken.UsuarioId));
    }

    public async Task UpdateAsync(PasswordResetToken resetToken)
    {
        await _container.ReplaceItemAsync(resetToken,
            resetToken.Id.ToString(),
            CosmosPartitionKeys.For(resetToken.UsuarioId));
    }

    public async Task<int> InvalidateAllByUsuarioIdAsync(Guid usuarioId, DateTime invalidatedAt, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.usedAt = null AND c.expiresAt > @now")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@now", DateTime.UtcNow.ToString("O"));

        var tokens = new List<PasswordResetToken>();
        using var iterator = _container.GetItemQueryIterator<PasswordResetToken>(query,
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
            token.UsedAt = invalidatedAt;
            await _container.ReplaceItemAsync(token,
                token.Id.ToString(),
                CosmosPartitionKeys.For(token.UsuarioId),
                cancellationToken: cancellationToken);
        }

        return tokens.Count;
    }
}
