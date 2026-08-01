using ImpactX.Configuration;
using ImpactX.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ImpactX.Health;

public sealed class DatabaseReadinessCheck : IHealthCheck
{
    private readonly CosmosDbContext _cosmosDb;
    private readonly DatabaseInitializationState _initializationState;
    private readonly DatabaseInitializationOptions _initializationOptions;
    private readonly ReadinessOptions _readinessOptions;

    public DatabaseReadinessCheck(
        CosmosDbContext cosmosDb,
        DatabaseInitializationState initializationState,
        IOptions<DatabaseInitializationOptions> initializationOptions,
        IOptions<ReadinessOptions> readinessOptions)
    {
        _cosmosDb = cosmosDb;
        _initializationState = initializationState;
        _initializationOptions = initializationOptions.Value;
        _readinessOptions = readinessOptions.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_initializationOptions.Enabled && _readinessOptions.InitializationRequired)
        {
            switch (_initializationState.Status)
            {
                case DatabaseInitializationStatus.Succeeded:
                    break;
                case DatabaseInitializationStatus.Failed:
                    return HealthCheckResult.Unhealthy(
                        _initializationState.FailureDescription ?? "Database initialization failed.");
                default:
                    return HealthCheckResult.Unhealthy("Database initialization has not completed.");
            }
        }

        var accessTimeout = TimeSpan.FromSeconds(Math.Max(1, _readinessOptions.CosmosAccessTimeoutSeconds));

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(accessTimeout);

            var accessible = await _cosmosDb.IsAccessibleAsync(timeoutCts.Token);
            return accessible
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Database is not accessible.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Unhealthy("Database access check timed out.");
        }
    }
}
