using ImpactX.Infrastructure.Data.Repositories.Cosmos;

namespace ImpactX.Tests.Unit;

public class IncidenteQueryBuilderTests
{
    private static IncidenteQueryBuilder.IncidenteFilterSpec CreateSpec(
        Guid? usuarioId = null, string? severidad = null, DateTime? desde = null, DateTime? hasta = null)
        => new(usuarioId ?? Guid.NewGuid(), severidad, desde, hasta);

    [Trait("Category", "Security")]
    [Fact]
    public void BuildWhereClause_AlwaysContainsUsuarioIdParameter()
    {
        var where = IncidenteQueryBuilder.BuildWhereClause(CreateSpec());

        Assert.Contains("@usuarioId", where);
    }

    [Trait("Category", "Security")]
    [Fact]
    public void BuildWhereClause_NeverContainsRawUserInput()
    {
        var userId = Guid.NewGuid();
        var severidad = "Alta' OR '1'='1";
        var desde = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var where = IncidenteQueryBuilder.BuildWhereClause(CreateSpec(userId, severidad, desde));

        Assert.DoesNotContain(userId.ToString(), where);
        Assert.DoesNotContain(severidad, where);
        Assert.DoesNotContain("'1'='1", where);
        Assert.DoesNotContain(desde.ToString("O"), where);
    }

    [Trait("Category", "Security")]
    [Fact]
    public void AddFilterParameters_BindsUserValuesAsParameters()
    {
        var userId = Guid.NewGuid();
        var severidad = "Alta";
        var desde = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var spec = CreateSpec(userId, severidad, desde);

        var query = new Microsoft.Azure.Cosmos.QueryDefinition(
            $"SELECT * FROM c WHERE {IncidenteQueryBuilder.BuildWhereClause(spec)}");

        IncidenteQueryBuilder.AddFilterParameters(query, spec);

        var text = query.QueryText;
        Assert.Contains("@usuarioId", text);
        Assert.Contains("@severidad", text);
        Assert.Contains("@desde", text);
        Assert.DoesNotContain(severidad, text);
        Assert.DoesNotContain(desde.ToString("O"), text);
    }

    [Trait("Category", "Security")]
    [Fact]
    public void BuildWhereClause_OptionalFilters_AreConditional()
    {
        var onlyUser = IncidenteQueryBuilder.BuildWhereClause(CreateSpec());
        Assert.Equal("c.usuarioId = @usuarioId", onlyUser);

        var withSeverity = IncidenteQueryBuilder.BuildWhereClause(CreateSpec(severidad: "Alta"));
        Assert.Contains("c.severidad = @severidad", withSeverity);
        Assert.DoesNotContain("@desde", withSeverity);
        Assert.DoesNotContain("@hasta", withSeverity);

        var withRange = IncidenteQueryBuilder.BuildWhereClause(
            CreateSpec(desde: DateTime.UtcNow, hasta: DateTime.UtcNow));
        Assert.Contains("c.creadoEn >= @desde", withRange);
        Assert.Contains("c.creadoEn <= @hasta", withRange);
    }

    [Fact]
    public void CountQuery_UsesSameParameterizedWhereClause()
    {
        var spec = CreateSpec(severidad: "Media");
        var where = IncidenteQueryBuilder.BuildWhereClause(spec);

        var query = new Microsoft.Azure.Cosmos.QueryDefinition($"SELECT VALUE COUNT(1) FROM c WHERE {where}");
        IncidenteQueryBuilder.AddFilterParameters(query, spec);

        Assert.StartsWith("SELECT VALUE COUNT(1)", query.QueryText);
        Assert.DoesNotContain(spec.Severidad!, query.QueryText);
    }
}
