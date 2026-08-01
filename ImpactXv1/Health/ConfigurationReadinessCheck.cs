using ImpactX.Infrastructure.Data;
using ImpactX.Infrastructure.Security;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ImpactX.Health;

public sealed class ConfigurationReadinessCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public ConfigurationReadinessCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            JwtSecurityConfiguration.GetRequiredSecret(_configuration);
        }
        catch (InvalidOperationException)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Required security configuration is missing."));
        }

        if (_configuration.GetValue<bool>("UseCosmosDb"))
        {
            if (string.IsNullOrWhiteSpace(_configuration["AzureCosmosDb:Endpoint"]))
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy("Cosmos DB endpoint is not configured."));
            }

            if (string.IsNullOrWhiteSpace(_configuration["AzureCosmosDb:DatabaseName"]))
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy("Cosmos DB database name is not configured."));
            }

            var key = _configuration["AzureCosmosDb:Key"];
            if (string.IsNullOrWhiteSpace(key) ||
                string.Equals(key, CosmosDatabaseDefaults.PlaceholderKey, StringComparison.Ordinal))
            {
                return Task.FromResult(
                    HealthCheckResult.Unhealthy("Cosmos DB key is not configured."));
            }
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
