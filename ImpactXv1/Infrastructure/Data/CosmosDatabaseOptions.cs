namespace ImpactX.Infrastructure.Data;

/// <summary>
/// Opciones fuertemente tipadas de Cosmos DB (sección "AzureCosmosDb").
/// La Key nunca debe contener un valor real en appsettings; su presencia
/// y validez se verifican en readiness (ConfigurationReadinessCheck).
/// Ningún contenedor usa throughput dedicado: toda la base comparte
/// SharedThroughput.
/// </summary>
public sealed class CosmosDatabaseOptions
{
    public const string SectionName = "AzureCosmosDb";
    public const string DefaultDatabaseName = "ImpactX-Data";
    public const int DefaultSharedThroughput = 400;
    public const int MaxSharedThroughput = 1000;

    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = DefaultDatabaseName;
    public int SharedThroughput { get; set; } = DefaultSharedThroughput;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxRetryAttemptsOnRateLimitedRequests { get; set; } = 3;
    public int MaxRetryWaitTimeSeconds { get; set; } = 30;

    /// <summary>
    /// Validación usada por Options pattern (ValidateOnStart).
    /// Devuelve null cuando la configuración es válida.
    /// </summary>
    public static string? Validate(CosmosDatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint) ||
            !Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var endpointUri) ||
            (endpointUri.Scheme != Uri.UriSchemeHttps && endpointUri.Scheme != Uri.UriSchemeHttp))
        {
            return "Cosmos DB endpoint must be an absolute HTTP(S) URI.";
        }

        if (string.IsNullOrWhiteSpace(options.DatabaseName))
        {
            return "Cosmos DB database name must not be empty.";
        }

        if (options.SharedThroughput <= 0 || options.SharedThroughput > MaxSharedThroughput)
        {
            return $"Cosmos DB shared throughput must be a positive integer between 1 and {MaxSharedThroughput}.";
        }

        if (options.RequestTimeoutSeconds <= 0)
        {
            return "Cosmos DB request timeout must be a positive number of seconds.";
        }

        if (options.MaxRetryAttemptsOnRateLimitedRequests < 0)
        {
            return "Cosmos DB max retry attempts must not be negative.";
        }

        if (options.MaxRetryWaitTimeSeconds < 0)
        {
            return "Cosmos DB max retry wait time must not be negative.";
        }

        return null;
    }
}
