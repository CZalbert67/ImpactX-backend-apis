using System.Net;
using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using Microsoft.Azure.Cosmos;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosContactoRepository : IContactoRepository
{
    private const string LegacyPredicate =
        "(NOT IS_DEFINED(c.publicContactId) OR IS_NULL(c.publicContactId) OR c.publicContactId = '')";

    private readonly Container _container;

    public CosmosContactoRepository(CosmosDbContext dbContext)
    {
        _container = dbContext.ContactosEmergencia;
    }

    public async Task<List<ContactoEmergencia>> GetByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            $"SELECT * FROM c WHERE c.usuarioId = @usuarioId AND {LegacyPredicate} " +
            "ORDER BY c.esPrincipal DESC, c.creadoEn ASC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        var results = new List<ContactoEmergencia>();
        using var iterator = _container.GetItemQueryIterator<ContactoEmergencia>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 100
            });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    public Task<PagedResult<ContactoEmergencia>> GetByUserPagedAsync(
        Guid usuarioId,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            $"SELECT * FROM c WHERE c.usuarioId = @usuarioId AND {LegacyPredicate} " +
            "ORDER BY c.esPrincipal DESC, c.creadoEn ASC")
            .WithParameter("@usuarioId", usuarioId.ToString());

        return CosmosPageReader.ReadSinglePageAsync<ContactoEmergencia>(
            _container,
            query,
            CosmosPartitionKeys.For(usuarioId),
            pageSize,
            continuationToken,
            cancellationToken);
    }

    public Task<PagedResult<ContactoEmergencia>> GetV1ForUserPagedAsync(
        Guid userId,
        string emailNormalized,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE IS_DEFINED(c.publicContactId) " +
            "AND NOT IS_NULL(c.publicContactId) AND c.publicContactId != '' " +
            "AND (c.usuarioId = @userId OR c.contactUserId = @userId " +
            "OR (c.status = @pending AND c.targetEmailNormalized = @email)) " +
            "ORDER BY c.updatedAtUtc DESC")
            .WithParameter("@userId", userId.ToString())
            .WithParameter("@pending", EmergencyContactStatus.Pending.ToString())
            .WithParameter("@email", emailNormalized);

        return CosmosPageReader.ReadSinglePageAsync<ContactoEmergencia>(
            _container,
            query,
            partitionKey: null,
            pageSize,
            continuationToken,
            cancellationToken);
    }

    public async Task<IReadOnlyList<ContactoEmergencia>> GetV1ForUserAsync(
        Guid userId,
        string emailNormalized,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE IS_DEFINED(c.publicContactId) " +
            "AND NOT IS_NULL(c.publicContactId) AND c.publicContactId != '' " +
            "AND (c.usuarioId = @userId OR c.contactUserId = @userId " +
            "OR (c.status = @pending AND c.targetEmailNormalized = @email)) " +
            "ORDER BY c.updatedAtUtc DESC")
            .WithParameter("@userId", userId.ToString())
            .WithParameter("@pending", EmergencyContactStatus.Pending.ToString())
            .WithParameter("@email", emailNormalized);

        var results = new List<ContactoEmergencia>();
        using var iterator = _container.GetItemQueryIterator<ContactoEmergencia>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 100 });
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }

    public async Task<ContactoEmergencia?> GetByPublicIdAsync(
        string publicContactId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.publicContactId = @publicContactId")
            .WithParameter("@publicContactId", publicContactId);

        using var iterator = _container.GetItemQueryIterator<ContactoEmergencia>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }

    public async Task<ContactoEmergencia?> GetByInvitationCodeHashAsync(
        string codeHash,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.invitationCodeHash = @codeHash")
            .WithParameter("@codeHash", codeHash);

        using var iterator = _container.GetItemQueryIterator<ContactoEmergencia>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }

    public async Task<int> CountAcceptedByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @ownerUserId " +
            "AND c.status = @accepted AND IS_DEFINED(c.publicContactId)")
            .WithParameter("@ownerUserId", ownerUserId.ToString())
            .WithParameter("@accepted", EmergencyContactStatus.Accepted.ToString());

        using var iterator = _container.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(ownerUserId),
                MaxItemCount = 1
            });
        if (!iterator.HasMoreResults)
        {
            return 0;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }

    public async Task<ContactoEmergencia?> GetAcceptedPrimaryByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.usuarioId = @ownerUserId " +
            "AND c.status = @accepted AND c.esPrincipal = true " +
            "AND IS_DEFINED(c.publicContactId)")
            .WithParameter("@ownerUserId", ownerUserId.ToString())
            .WithParameter("@accepted", EmergencyContactStatus.Accepted.ToString());

        using var iterator = _container.GetItemQueryIterator<ContactoEmergencia>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(ownerUserId),
                MaxItemCount = 1
            });
        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }

    public async Task<bool> ExistsV1ActiveOrPendingAsync(
        Guid ownerUserId,
        Guid? contactUserId,
        string? targetEmailNormalized,
        Guid? excludeContactId = null,
        CancellationToken cancellationToken = default)
    {
        var queryText =
            "SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @ownerUserId " +
            "AND IS_DEFINED(c.publicContactId) " +
            "AND (c.status = @pending OR c.status = @accepted) ";

        if (excludeContactId.HasValue)
        {
            queryText += "AND c.id != @excludeId ";
        }

        queryText += "AND (";

        if (contactUserId.HasValue && !string.IsNullOrEmpty(targetEmailNormalized))
        {
            queryText += "c.contactUserId = @contactUserId OR c.targetEmailNormalized = @email)";
        }
        else if (contactUserId.HasValue)
        {
            queryText += "c.contactUserId = @contactUserId)";
        }
        else
        {
            queryText += "c.targetEmailNormalized = @email)";
        }

        var query = new QueryDefinition(queryText)
            .WithParameter("@ownerUserId", ownerUserId.ToString())
            .WithParameter("@pending", EmergencyContactStatus.Pending.ToString())
            .WithParameter("@accepted", EmergencyContactStatus.Accepted.ToString());

        if (excludeContactId.HasValue)
        {
            query.WithParameter("@excludeId", excludeContactId.Value.ToString());
        }

        if (contactUserId.HasValue)
        {
            query.WithParameter("@contactUserId", contactUserId.Value.ToString());
        }

        if (!string.IsNullOrEmpty(targetEmailNormalized))
        {
            query.WithParameter("@email", targetEmailNormalized);
        }

        using var iterator = _container.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(ownerUserId),
                MaxItemCount = 1
            });
        if (!iterator.HasMoreResults)
        {
            return false;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault() > 0;
    }

    public async Task<bool> ExistsV1BlockedAsync(
        Guid ownerUserId,
        string ownerEmailNormalized,
        Guid? contactUserId,
        string? targetEmailNormalized,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE IS_DEFINED(c.publicContactId) " +
            "AND c.status = @blocked AND (" +
            "(c.usuarioId = @owner AND (" +
            "(@hasContact = true AND IS_DEFINED(c.contactUserId) AND c.contactUserId = @contact) " +
            "OR (@hasTargetEmail = true AND IS_DEFINED(c.targetEmailNormalized) " +
            "AND c.targetEmailNormalized = @targetEmail))) " +
            "OR (@hasContact = true AND c.usuarioId = @contact AND (" +
            "(IS_DEFINED(c.contactUserId) AND c.contactUserId = @owner) " +
            "OR (@hasOwnerEmail = true AND IS_DEFINED(c.targetEmailNormalized) " +
            "AND c.targetEmailNormalized = @ownerEmail))))")
            .WithParameter("@blocked", EmergencyContactStatus.Blocked.ToString())
            .WithParameter("@owner", ownerUserId.ToString())
            .WithParameter("@hasContact", contactUserId.HasValue)
            .WithParameter("@contact", contactUserId?.ToString() ?? string.Empty)
            .WithParameter("@hasTargetEmail", !string.IsNullOrEmpty(targetEmailNormalized))
            .WithParameter("@targetEmail", targetEmailNormalized ?? string.Empty)
            .WithParameter("@hasOwnerEmail", !string.IsNullOrEmpty(ownerEmailNormalized))
            .WithParameter("@ownerEmail", ownerEmailNormalized);

        using var iterator = _container.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (!iterator.HasMoreResults)
        {
            return false;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault() > 0;
    }

    public async Task<ContactoEmergencia?> GetByIdAsync(Guid id)
    {
        var query = new QueryDefinition(
            $"SELECT TOP 1 * FROM c WHERE c.id = @id AND {LegacyPredicate}")
            .WithParameter("@id", id.ToString());

        using var iterator = _container.GetItemQueryIterator<ContactoEmergencia>(
            query,
            requestOptions: new QueryRequestOptions { MaxItemCount = 1 });
        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync();
        return response.FirstOrDefault();
    }

    public async Task<ContactoEmergencia?> GetByIdAsync(Guid usuarioId, Guid id)
    {
        try
        {
            var response = await _container.ReadItemAsync<ContactoEmergencia>(
                id.ToString(),
                CosmosPartitionKeys.For(usuarioId));
            var contact = response.Resource;
            return string.IsNullOrWhiteSpace(contact.PublicContactId) ? contact : null;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<ContactoEmergencia?> GetPrincipalAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            $"SELECT TOP 1 * FROM c WHERE c.usuarioId = @usuarioId " +
            $"AND c.esPrincipal = true AND {LegacyPredicate}")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<ContactoEmergencia>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 1
            });
        if (!iterator.HasMoreResults)
        {
            return null;
        }

        var response = await iterator.ReadNextAsync();
        return response.FirstOrDefault();
    }

    public async Task<int> CountByUserAsync(Guid usuarioId)
    {
        var query = new QueryDefinition(
            $"SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @usuarioId AND {LegacyPredicate}")
            .WithParameter("@usuarioId", usuarioId.ToString());

        using var iterator = _container.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 1
            });
        if (!iterator.HasMoreResults)
        {
            return 0;
        }

        var response = await iterator.ReadNextAsync();
        return response.FirstOrDefault();
    }

    public async Task<bool> ExistsByTelefonoAsync(Guid usuarioId, string telefono)
    {
        var query = new QueryDefinition(
            $"SELECT VALUE COUNT(1) FROM c WHERE c.usuarioId = @usuarioId " +
            $"AND c.telefono = @telefono AND {LegacyPredicate}")
            .WithParameter("@usuarioId", usuarioId.ToString())
            .WithParameter("@telefono", telefono);

        using var iterator = _container.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(usuarioId),
                MaxItemCount = 1
            });
        if (!iterator.HasMoreResults)
        {
            return false;
        }

        var response = await iterator.ReadNextAsync();
        return response.FirstOrDefault() > 0;
    }

    public async Task AddAsync(ContactoEmergencia contacto)
    {
        contacto.Id = Guid.NewGuid();
        await _container.CreateItemAsync(
            contacto,
            CosmosPartitionKeys.For(contacto.UsuarioId));
    }

    public async Task UpdateAsync(ContactoEmergencia contacto)
    {
        await _container.ReplaceItemAsync(
            contacto,
            contacto.Id.ToString(),
            CosmosPartitionKeys.For(contacto.UsuarioId));
    }

    public async Task DeleteAsync(ContactoEmergencia contacto)
    {
        await _container.DeleteItemAsync<ContactoEmergencia>(
            contacto.Id.ToString(),
            CosmosPartitionKeys.For(contacto.UsuarioId));
    }
}
