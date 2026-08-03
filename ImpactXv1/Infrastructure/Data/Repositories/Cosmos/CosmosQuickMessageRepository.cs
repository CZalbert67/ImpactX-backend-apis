using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using Microsoft.Azure.Cosmos;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

public class CosmosQuickMessageRepository : IQuickMessageRepository
{
    private readonly Container _templates;
    private readonly Container _messages;

    public CosmosQuickMessageRepository(CosmosDbContext dbContext)
    {
        _templates = dbContext.QuickMessageTemplates;
        _messages = dbContext.QuickMessages;
    }

    public async Task<IReadOnlyList<QuickMessageTemplate>> GetTemplatesByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.ownerKey = @ownerKey AND c.active = true")
            .WithParameter("@ownerKey", ownerUserId.ToString());
        var results = await ReadAllAsync<QuickMessageTemplate>(
            _templates,
            query,
            CosmosPartitionKeys.For(ownerUserId),
            cancellationToken);
        return results.OrderBy(value => value.SortOrder).ThenBy(value => value.CreatedAtUtc).ToList();
    }

    public async Task<QuickMessageTemplate?> GetTemplateByPublicIdAsync(
        Guid ownerUserId,
        string publicTemplateId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.ownerKey = @ownerKey " +
            "AND c.publicTemplateId = @publicTemplateId AND c.active = true")
            .WithParameter("@ownerKey", ownerUserId.ToString())
            .WithParameter("@publicTemplateId", publicTemplateId);
        return await ReadFirstAsync<QuickMessageTemplate>(
            _templates,
            query,
            CosmosPartitionKeys.For(ownerUserId),
            cancellationToken);
    }

    public async Task<int> CountActiveTemplatesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.ownerKey = @ownerKey AND c.active = true")
            .WithParameter("@ownerKey", ownerUserId.ToString());
        using var iterator = _templates.GetItemQueryIterator<int>(
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

    public async Task AddTemplateAsync(
        QuickMessageTemplate template,
        CancellationToken cancellationToken = default)
    {
        await _templates.CreateItemAsync(
            template,
            CosmosPartitionKeys.For(template.OwnerKey),
            cancellationToken: cancellationToken);
    }

    public async Task UpdateTemplateAsync(
        QuickMessageTemplate template,
        CancellationToken cancellationToken = default)
    {
        await _templates.ReplaceItemAsync(
            template,
            template.Id.ToString(),
            CosmosPartitionKeys.For(template.OwnerKey),
            cancellationToken: cancellationToken);
    }

    public async Task AddMessageAsync(
        QuickMessage message,
        CancellationToken cancellationToken = default)
    {
        await _messages.CreateItemAsync(
            message,
            CosmosPartitionKeys.For(message.RecipientUserId),
            cancellationToken: cancellationToken);
    }

    public async Task<QuickMessage?> GetMessageForRecipientAsync(
        Guid recipientUserId,
        string publicMessageId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT TOP 1 * FROM c WHERE c.recipientUserId = @recipientUserId " +
            "AND c.publicMessageId = @publicMessageId")
            .WithParameter("@recipientUserId", recipientUserId.ToString())
            .WithParameter("@publicMessageId", publicMessageId);
        return await ReadFirstAsync<QuickMessage>(
            _messages,
            query,
            CosmosPartitionKeys.For(recipientUserId),
            cancellationToken);
    }

    public async Task<IReadOnlyList<QuickMessage>> GetHistoryAsync(
        Guid userId,
        Guid? otherUserId,
        CancellationToken cancellationToken = default)
    {
        var sql = "SELECT TOP 200 * FROM c WHERE " +
            "(c.senderUserId = @userId OR c.recipientUserId = @userId)";
        if (otherUserId.HasValue)
        {
            sql += " AND ((c.senderUserId = @userId AND c.recipientUserId = @otherUserId) " +
                "OR (c.senderUserId = @otherUserId AND c.recipientUserId = @userId))";
        }

        var query = new QueryDefinition(sql).WithParameter("@userId", userId.ToString());
        if (otherUserId.HasValue)
        {
            query.WithParameter("@otherUserId", otherUserId.Value.ToString());
        }

        var results = await ReadAllAsync<QuickMessage>(
            _messages,
            query,
            null,
            cancellationToken);
        return results.OrderByDescending(value => value.SentAtUtc).Take(200).ToList();
    }

    public async Task<int> CountUnreadAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT VALUE COUNT(1) FROM c WHERE c.recipientUserId = @recipientUserId " +
            "AND c.isRead = false")
            .WithParameter("@recipientUserId", recipientUserId.ToString());
        using var iterator = _messages.GetItemQueryIterator<int>(
            query,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = CosmosPartitionKeys.For(recipientUserId),
                MaxItemCount = 1
            });
        if (!iterator.HasMoreResults)
        {
            return 0;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }

    public async Task<int> MarkConversationReadAsync(
        Guid recipientUserId,
        Guid senderUserId,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default)
    {
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.recipientUserId = @recipientUserId " +
            "AND c.senderUserId = @senderUserId AND c.isRead = false")
            .WithParameter("@recipientUserId", recipientUserId.ToString())
            .WithParameter("@senderUserId", senderUserId.ToString());
        var unread = await ReadAllAsync<QuickMessage>(
            _messages,
            query,
            CosmosPartitionKeys.For(recipientUserId),
            cancellationToken);

        foreach (var message in unread)
        {
            message.IsRead = true;
            message.ReadAtUtc = readAtUtc;
            await UpdateMessageAsync(message, cancellationToken);
        }

        return unread.Count;
    }

    public async Task UpdateMessageAsync(
        QuickMessage message,
        CancellationToken cancellationToken = default)
    {
        await _messages.ReplaceItemAsync(
            message,
            message.Id.ToString(),
            CosmosPartitionKeys.For(message.RecipientUserId),
            cancellationToken: cancellationToken);
    }

    private static async Task<T?> ReadFirstAsync<T>(
        Container container,
        QueryDefinition query,
        PartitionKey? partitionKey,
        CancellationToken cancellationToken)
    {
        var options = new QueryRequestOptions { MaxItemCount = 1 };
        if (partitionKey.HasValue)
        {
            options.PartitionKey = partitionKey.Value;
        }

        using var iterator = container.GetItemQueryIterator<T>(query, requestOptions: options);
        if (!iterator.HasMoreResults)
        {
            return default;
        }

        var response = await iterator.ReadNextAsync(cancellationToken);
        return response.FirstOrDefault();
    }

    private static async Task<List<T>> ReadAllAsync<T>(
        Container container,
        QueryDefinition query,
        PartitionKey? partitionKey,
        CancellationToken cancellationToken)
    {
        var options = new QueryRequestOptions { MaxItemCount = 100 };
        if (partitionKey.HasValue)
        {
            options.PartitionKey = partitionKey.Value;
        }

        var results = new List<T>();
        using var iterator = container.GetItemQueryIterator<T>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            results.AddRange(response);
        }

        return results;
    }
}
