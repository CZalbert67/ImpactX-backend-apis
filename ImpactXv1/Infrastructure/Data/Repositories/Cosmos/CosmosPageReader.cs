using ImpactX.Core.Exceptions;
using ImpactX.Core.Pagination;
using Microsoft.Azure.Cosmos;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

/// <summary>
/// Lectura de una sola página Cosmos para endpoints públicos paginados:
/// MaxItemCount = PageSize validado, continuationToken del SDK pasado tal
/// cual (opaco), un único ReadNextAsync, sin while. Un token del SDK inválido
/// (400 de Cosmos) se traduce en BadRequestException genérica sin exponer el
/// token ni detalles de Cosmos.
/// </summary>
internal static class CosmosPageReader
{
    public static async Task<PagedResult<T>> ReadSinglePageAsync<T>(
        Container container,
        QueryDefinition query,
        PartitionKey? partitionKey,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        var requestOptions = new QueryRequestOptions
        {
            MaxItemCount = pageSize
        };

        if (partitionKey.HasValue)
        {
            requestOptions.PartitionKey = partitionKey.Value;
        }

        using var iterator = container.GetItemQueryIterator<T>(
            query, continuationToken, requestOptions);

        FeedResponse<T> response;
        try
        {
            response = await iterator.ReadNextAsync(cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            throw new BadRequestException("El token de continuación es inválido o ha expirado.");
        }

        return new PagedResult<T>
        {
            Items = response.Resource.ToList(),
            ContinuationToken = response.ContinuationToken,
            HasMoreResults = iterator.HasMoreResults,
            PageSize = pageSize,
        };
    }
}
