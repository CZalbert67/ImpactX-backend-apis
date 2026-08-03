namespace ImpactX.Core.Pagination;

/// <summary>
/// Resultado de una sola página de consulta paginada.
/// Los tokens de continuación son opacos: los consumidores no deben
/// inspeccionarlos ni modificarlos. Nunca se registran en logs.
/// </summary>
public sealed class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = [];

    public string? ContinuationToken { get; init; }

    public bool HasMoreResults { get; init; }

    public int PageSize { get; init; }
}
