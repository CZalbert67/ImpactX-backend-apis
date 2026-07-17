using Microsoft.Azure.Cosmos;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosContactoRepository : IContactoRepository
{
    private readonly Container _container;

    public CosmosContactoRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.ContactosEmergencia;
    }

    public async Task<List<ContactoEmergencia>> GetByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.usuarioId = @usuarioId ORDER BY c.esPrincipal DESC, c.creadoEn ASC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<ContactoEmergencia>();
        using var iterator = _container.GetItemQueryIterator<ContactoEmergencia>(query);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }
        return results;
    }

    public async Task<ContactoEmergencia?> GetByIdAsync(Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<ContactoEmergencia>(
                id.ToString(), new PartitionKey(id.ToString()));
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ContactoEmergencia?> GetPrincipalAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId AND c.esPrincipal = true")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<ContactoEmergencia>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return null;
    }

    public async Task<int> CountByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @usuarioId")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<int>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault();
        }
        return 0;
    }

    public async Task<bool> ExistsByTelefonoAsync(Guid usuarioId, string telefono)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @usuarioId AND c.telefono = @telefono")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@telefono", telefono);

        using var iterator = _container.GetItemQueryIterator<int>(query);
        if (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            return response.FirstOrDefault() > 0;
        }
        return false;
    }

    public async Task AddAsync(ContactoEmergencia contacto)
    {
        contacto.Id = Guid.NewGuid();
        await _container.CreateItemAsync(contacto, new PartitionKey(contacto.UsuarioId.ToString()));
    }

    public async Task UpdateAsync(ContactoEmergencia contacto)
    {
        await _container.UpsertItemAsync(contacto, new PartitionKey(contacto.UsuarioId.ToString()));
    }

    public async Task DeleteAsync(ContactoEmergencia contacto)
    {
        await _container.DeleteItemAsync<ContactoEmergencia>(
            contacto.Id.ToString(),
            new PartitionKey(contacto.UsuarioId.ToString()));
    }
}
