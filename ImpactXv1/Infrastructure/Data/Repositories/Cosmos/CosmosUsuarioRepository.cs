using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosUsuarioRepository : IUsuarioRepository
{
    private readonly Container _container;

    public CosmosUsuarioRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.Usuarios;
    }

    public async Task<Usuario?> GetByIdAsync(Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<Usuario>(
                id.ToString(), new PartitionKey(id.ToString()));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Usuario?> GetByCorreoAsync(string correo)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.correo = @correo")
            .WithParameter("@correo", correo);

        using var iterator = _container.GetItemQueryIterator<Usuario>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Usuario?> GetByUsernameAsync(string username)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.username = @username")
            .WithParameter("@username", username);

        using var iterator = _container.GetItemQueryIterator<Usuario>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<List<Usuario>> SearchAsync(string query)
    {
        var lower = query.ToLowerInvariant();
        var sqlQuery = new QueryDefinition(
            "SELECT * FROM c WHERE CONTAINS(LOWER(c.username), @query) " +
            "OR CONTAINS(LOWER(c.nombre), @query) " +
            "OR CONTAINS(LOWER(c.appId), @query) " +
            "OR CONTAINS(LOWER(c.correo), @query)")
            .WithParameter("@query", lower);

        var users = new List<Usuario>();
        using var iterator = _container.GetItemQueryIterator<Usuario>(sqlQuery);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            users.AddRange(response);
            if (users.Count >= 20) break;
        }
        return users;
    }

    public async Task<bool> ExistsByCorreoAsync(string correo)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.correo = @correo")
            .WithParameter("@correo", correo);

        using var iterator = _container.GetItemQueryIterator<int>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault() > 0;
        }
        return false;
    }

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.username = @username")
            .WithParameter("@username", username);

        using var iterator = _container.GetItemQueryIterator<int>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault() > 0;
        }
        return false;
    }

    public async Task AddAsync(Usuario usuario)
    {
        usuario.Id = Guid.NewGuid();
        await _container.CreateItemAsync(usuario,
            new PartitionKey(usuario.Id.ToString()));
    }

    public async Task UpdateAsync(Usuario usuario)
    {
        await _container.UpsertItemAsync(usuario,
            new PartitionKey(usuario.Id.ToString()));
    }

    public async Task DeleteAsync(Usuario usuario)
    {
        await _container.DeleteItemAsync<Usuario>(
            usuario.Id.ToString(),
            new PartitionKey(usuario.Id.ToString()));
    }
}
