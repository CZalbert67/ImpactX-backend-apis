using Microsoft.Azure.Cosmos;

namespace ImpactX.Infrastructure.Data.Repositories.Cosmos;

/// <summary>
/// Construcción de consultas SQL de incidentes con parámetros QueryDefinition.
/// El texto SQL solo contiene cláusulas fijas y los valores del usuario se
/// enlazan siempre como parámetros; OFFSET/LIMIT son enteros (nunca strings
/// del usuario) porque Cosmos SQL no admite parámetros en esa posición.
/// </summary>
public static class IncidenteQueryBuilder
{
    public sealed record IncidenteFilterSpec(
        Guid UsuarioId, string? Severidad, DateTime? Desde, DateTime? Hasta);

    public static string BuildWhereClause(IncidenteFilterSpec spec)
    {
        var clauses = new List<string> { "c.usuarioId = @usuarioId" };

        if (!string.IsNullOrWhiteSpace(spec.Severidad))
        {
            clauses.Add("c.severidad = @severidad");
        }

        if (spec.Desde.HasValue)
        {
            clauses.Add("c.creadoEn >= @desde");
        }

        if (spec.Hasta.HasValue)
        {
            clauses.Add("c.creadoEn <= @hasta");
        }

        return string.Join(" AND ", clauses);
    }

    public static void AddFilterParameters(QueryDefinition query, IncidenteFilterSpec spec)
    {
        query.WithParameter("@usuarioId", spec.UsuarioId.ToString());

        if (!string.IsNullOrWhiteSpace(spec.Severidad))
        {
            query.WithParameter("@severidad", spec.Severidad);
        }

        if (spec.Desde.HasValue)
        {
            query.WithParameter("@desde", spec.Desde.Value.ToString("O"));
        }

        if (spec.Hasta.HasValue)
        {
            query.WithParameter("@hasta", spec.Hasta.Value.ToString("O"));
        }
    }
}
