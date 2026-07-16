using Microsoft.Azure.Cosmos;
using Prueba1.Core.Domain;
using Prueba1.Core.Interfaces.Repositories;

namespace Prueba1.Infrastructure.Data.Repositories.Cosmos;

public class CosmosPasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly Container _container;

    public CosmosPasswordResetTokenRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.PasswordResetTokens;
    }

    public async Task<PasswordResetToken?> GetByTokenAsync(string token)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.token = @token")
            .WithParameter("@token", token);

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
}
