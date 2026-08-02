using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
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
        // Proceso completo incremental: pagina, invalida por página y continúa
        // con el token; no acumula todos los tokens en memoria.
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId AND c.usedAt = null AND c.expiresAt > @now")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@now", DateTime.UtcNow.ToString("O"));

        var invalidated = 0;
        string? continuationToken = null;
        var pk = CosmosPartitionKeys.For(usuarioId);

        do
        {
            var page = await CosmosPageReader.ReadSinglePageAsync<PasswordResetToken>(
                _container, query, pk, PaginationDefaults.MaxPageSize, continuationToken, cancellationToken);

            foreach (var token in page.Items)
            {
                token.UsedAt = invalidatedAt;
                await _container.ReplaceItemAsync(token,
                    token.Id.ToString(),
                    CosmosPartitionKeys.For(token.UsuarioId),
                    cancellationToken: cancellationToken);
                invalidated++;
            }

            continuationToken = page.ContinuationToken;
        } while (continuationToken is not null);

        return invalidated;
    }
}
