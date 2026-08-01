using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Infrastructure.Data;

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
                id.ToString(), CosmosPartitionKeys.For(id));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<Usuario?> GetByCorreoAsync(string correo)
    {
        // Cross-partition justificada: Usuarios particiona por /id; la
        // búsqueda por correo no tiene partition key conocida. Contenedor
        // pequeño y consulta parametrizada con detención temprana.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.correo = @correo")
            .WithParameter("@correo", correo);

        using var iterator = _container.GetItemQueryIterator<Usuario>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Usuario?> GetByUsernameAsync(string username)
    {
        // Cross-partition justificada: Usuarios particiona por /id (ver GetByCorreoAsync).
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.username = @username")
            .WithParameter("@username", username);

        using var iterator = _container.GetItemQueryIterator<Usuario>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<List<Usuario>> SearchAsync(string query)
    {
        // Cross-partition justificada: búsqueda global por username/nombre/
        // appId/correo. Límite interno de 20 resultados con detención temprana.
        var lower = query.ToLowerInvariant();
        var sqlQuery = new QueryDefinition(
            "SELECT * FROM c WHERE CONTAINS(LOWER(c.username), @query) " +
            "OR CONTAINS(LOWER(c.nombre), @query) " +
            "OR CONTAINS(LOWER(c.appId), @query) " +
            "OR CONTAINS(LOWER(c.correo), @query)")
            .WithParameter("@query", lower);

        var users = new List<Usuario>();
        using var iterator = _container.GetItemQueryIterator<Usuario>(sqlQuery,
            requestOptions: new QueryRequestOptions { MaxItemCount = 20 });
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
        // Cross-partition justificada por el mismo motivo que GetByCorreoAsync.
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.correo = @correo")
            .WithParameter("@correo", correo);

        using var iterator = _container.GetItemQueryIterator<int>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault() > 0;
        }
        return false;
    }

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        // Cross-partition justificada por el mismo motivo que GetByCorreoAsync.
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.username = @username")
            .WithParameter("@username", username);

        using var iterator = _container.GetItemQueryIterator<int>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
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
            CosmosPartitionKeys.For(usuario.Id));
    }

    public async Task UpdateAsync(Usuario usuario)
    {
        await _container.ReplaceItemAsync(usuario,
            usuario.Id.ToString(),
            CosmosPartitionKeys.For(usuario.Id));
    }

    public async Task DeleteAsync(Usuario usuario)
    {
        await _container.DeleteItemAsync<Usuario>(
            usuario.Id.ToString(),
            CosmosPartitionKeys.For(usuario.Id));
    }
}
