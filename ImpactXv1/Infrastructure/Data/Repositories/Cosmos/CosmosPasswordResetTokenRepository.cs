using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

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
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.tokenHash = @tokenHash")
            .WithParameter("@tokenHash", tokenHash);

        using var iterator = _container.GetItemQueryIterator<PasswordResetToken>(query);
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
            new PartitionKey(resetToken.UsuarioId.ToString()));
    }

    public async Task UpdateAsync(PasswordResetToken resetToken)
    {
        await _container.UpsertItemAsync(resetToken,
            new PartitionKey(resetToken.UsuarioId.ToString()));
    }

    public async Task<int> InvalidateAllByUsuarioIdAsync(Guid usuarioId, DateTime invalidatedAt, CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.usedAt = null AND c.expiresAt > @now")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@now", DateTime.UtcNow.ToString("O"));

        var tokens = new List<PasswordResetToken>();
        using var iterator = _container.GetItemQueryIterator<PasswordResetToken>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            tokens.AddRange(response);
        }

        foreach (var token in tokens)
        {
            token.UsedAt = invalidatedAt;
            await _container.UpsertItemAsync(token,
                new PartitionKey(token.UsuarioId.ToString()),
                cancellationToken: cancellationToken);
        }

        return tokens.Count;
    }
}
