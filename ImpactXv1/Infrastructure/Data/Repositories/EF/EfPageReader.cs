using System.Globalization;
using System.Text;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Pagination;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

/// <summary>
/// Paginación EF (solo modo InMemory/dev y tests): token de continuación
/// opaco basado en offset codificado en base64. El cliente no debe
/// inspeccionarlo ni modificarlo. Un token malformado se rechaza con
/// BadRequestException genérica (400), sin detalles internos.
/// </summary>
internal static class OffsetContinuationToken
{
    public static string Encode(int offset)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes($"offset:{offset}"));

    public static int Decode(string token)
    {
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            if (!raw.StartsWith("offset:", StringComparison.Ordinal))
                throw new FormatException();

            var offset = int.Parse(raw.AsSpan(7), NumberStyles.None, CultureInfo.InvariantCulture);
            if (offset < 0)
                throw new FormatException();

            return offset;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
        {
            throw new BadRequestException("El token de continuación es inválido o ha expirado.");
        }
    }
}

/// <summary>
/// Ejecuta una página EF con Skip/Take. Consulta pageSize + 1 elementos y
/// devuelve como máximo pageSize: HasMoreResults es true solo cuando se
/// obtuvo el elemento adicional, de modo que una página final exacta (o
/// parcial) nunca genera un token hacia una página vacía.
/// </summary>
internal static class EfPageReader
{
    public static async Task<PagedResult<T>> ReadSinglePageAsync<T>(
        IQueryable<T> query,
        int pageSize,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        var offset = string.IsNullOrEmpty(continuationToken)
            ? 0
            : OffsetContinuationToken.Decode(continuationToken);

        var items = await query
            .Skip(offset)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > pageSize;
        var pageItems = hasMore ? items.Take(pageSize).ToList() : items;

        return new PagedResult<T>
        {
            Items = pageItems,
            ContinuationToken = hasMore ? OffsetContinuationToken.Encode(offset + pageSize) : null,
            HasMoreResults = hasMore,
            PageSize = pageSize,
        };
    }
}
