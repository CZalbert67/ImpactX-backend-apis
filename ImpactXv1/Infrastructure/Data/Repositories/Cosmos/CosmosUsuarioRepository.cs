using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Identity;
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
        // búsqueda por correo no tiene partition key conocida. Incluye
        // correoNormalizado (identidad canónica) con fallback a LOWER(correo)
        // para cuentas legacy sin el campo normalizado.
        var normalized = EmailNormalizer.Normalize(correo);
        if (normalized.Length == 0)
            return null;

        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.correoNormalizado = @correo " +
            "OR LOWER(c.correo) = @correo")
            .WithParameter("@correo", normalized);

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
        // Cross-partition justificada por el mismo motivo que GetByCorreoAsync.
        var normalized = UsernamePolicy.Normalize(username);
        if (normalized is null)
            return null;

        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE LOWER(c.username) = @username")
            .WithParameter("@username", normalized);

        using var iterator = _container.GetItemQueryIterator<Usuario>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<Usuario?> GetByPublicProfileIdAsync(string publicProfileId)
    {
        // Cross-partition justificada por el mismo motivo que GetByCorreoAsync.
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.publicProfileId = @id")
            .WithParameter("@id", publicProfileId);

        using var iterator = _container.GetItemQueryIterator<Usuario>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<List<Usuario>> SearchAsync(string query, string? by = null)
    {
        // Cross-partition justificada: búsqueda global por username/nombre/
        // publicProfileId. Sin correo ni ids internos. Límite interno de 20.
        var lower = query.ToLowerInvariant();
        var mode = by?.Trim().ToLowerInvariant();

        var filter = mode switch
        {
            "username" => "CONTAINS(LOWER(c.username), @query)",
            "publicprofileid" => "CONTAINS(LOWER(c.publicProfileId), @query)",
            _ => "CONTAINS(LOWER(c.username), @query) " +
                 "OR CONTAINS(LOWER(c.nombre), @query) " +
                 "OR CONTAINS(LOWER(c.publicProfileId), @query)"
        };

        var sqlQuery = new QueryDefinition($"SELECT * FROM c WHERE {filter}")
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
        var normalized = EmailNormalizer.Normalize(correo);
        if (normalized.Length == 0)
            return false;

        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.correoNormalizado = @correo " +
            "OR LOWER(c.correo) = @correo")
            .WithParameter("@correo", normalized);

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
        var normalized = UsernamePolicy.Normalize(username);
        if (normalized is null)
            return false;

        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE LOWER(c.username) = @username")
            .WithParameter("@username", normalized);

        using var iterator = _container.GetItemQueryIterator<int>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault() > 0;
        }
        return false;
    }

    public async Task<bool> ExistsByPublicProfileIdAsync(string publicProfileId)
    {
        // Cross-partition justificada por el mismo motivo que GetByCorreoAsync.
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.publicProfileId = @publicProfileId")
            .WithParameter("@publicProfileId", publicProfileId);

        using var iterator = _container.GetItemQueryIterator<int>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault() > 0;
        }
        return false;
    }

    public async Task<bool> ExistsByUsernameIncludingHistoryAsync(string username)
    {
        // Cross-partition justificada por el mismo motivo que GetByCorreoAsync.
        // Valida globalmente (actual e histórico reservado) sin consultas globales a otro
        // contenedor, ya que los usernames anteriores se guardan embebidos en el usuario.
        var normalized = UsernamePolicy.Normalize(username);
        if (normalized is null)
            return false;

        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE LOWER(c.username) = @username " +
            "OR ARRAY_CONTAINS(c.usernamesAnteriores, @username)")
            .WithParameter("@username", normalized);

        using var iterator = _container.GetItemQueryIterator<int>(query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault() > 0;
        }
        return false;
    }

    public async Task<bool> ExistsByUsernameHistoryExcludingUsuarioAsync(string username, Guid usuarioId)
    {
        // Cross-partition justificada por el mismo motivo que GetByCorreoAsync.
        var normalized = UsernamePolicy.Normalize(username);
        if (normalized is null)
            return false;

        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.id != @usuarioId " +
            "AND ARRAY_CONTAINS(c.usernamesAnteriores, @username)")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@username", normalized);

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
        if (usuario.Id == Guid.Empty)
        {
            usuario.Id = Guid.NewGuid();
        }
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
