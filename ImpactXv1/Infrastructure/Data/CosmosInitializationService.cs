using System.Net;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

namespace ImpactX.Infrastructure.Data;

public class CosmosInitializationService : BackgroundService
{
    private static readonly HashSet<HttpStatusCode> TransientStatusCodes = new()
    {
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    };

    private readonly CosmosDbContext _cosmosDb;
    private readonly DatabaseInitializationState _state;
    private readonly DatabaseInitializationOptions _options;
    private readonly ILogger<CosmosInitializationService> _logger;

    public CosmosInitializationService(
        CosmosDbContext cosmosDb,
        DatabaseInitializationState state,
        IOptions<DatabaseInitializationOptions> options,
        ILogger<CosmosInitializationService> logger)
    {
        _cosmosDb = cosmosDb;
        _state = state;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Cosmos database initialization is disabled by configuration.");
            return;
        }

        var maxAttempts = Math.Max(1, _options.MaxAttempts);
        var retryDelay = TimeSpan.FromSeconds(Math.Max(0, _options.RetryDelaySeconds));
        var timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));

        _state.MarkRunning(maxAttempts);

        try
        {
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                _state.MarkAttempt();

                try
                {
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timeoutCts.CancelAfter(timeout);

                    await InitializeAsync(timeoutCts.Token);

                    _state.MarkSucceeded();
                    _logger.LogInformation(
                        "Cosmos database initialization completed successfully.");
                    return;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (OperationCanceledException)
                {
                    if (attempt < maxAttempts)
                    {
                        _logger.LogWarning(
                            "Cosmos database initialization attempt {Attempt}/{MaxAttempts} timed out. Retrying.",
                            attempt, maxAttempts);
                        await DelayRetryAsync(retryDelay, stoppingToken);
                        continue;
                    }

                    _state.MarkFailed("Database initialization timed out.");
                    return;
                }
                catch (CosmosSchemaValidationException ex)
                {
                    // No se reintenta: es un desajuste permanente que requiere
                    // migración controlada. Solo se registra el nombre lógico
                    // del contenedor, nunca valores internos.
                    _logger.LogError(
                        "Cosmos schema mismatch detected for container {ContainerName} ({MismatchKind}); controlled migration required.",
                        ex.ContainerName,
                        ex.MismatchKind);
                    _state.MarkFailed("Database schema mismatch detected; controlled migration required.");
                    return;
                }
                catch (CosmosException ex) when (IsTransient(ex.StatusCode))
                {
                    if (attempt < maxAttempts)
                    {
                        _logger.LogWarning(
                            "Cosmos database initialization attempt {Attempt}/{MaxAttempts} failed with status {StatusCode}. Retrying.",
                            attempt, maxAttempts, (int)ex.StatusCode);
                        await DelayRetryAsync(retryDelay, stoppingToken);
                        continue;
                    }

                    _state.MarkFailed("Database initialization failed after retries.");
                    return;
                }
                catch (CosmosException ex)
                {
                    _logger.LogError(
                        "Cosmos database initialization failed with non-retryable status {StatusCode}.",
                        (int)ex.StatusCode);
                    _state.MarkFailed("Database initialization failed with a non-retryable error.");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        "Cosmos database initialization failed with {ErrorType}.",
                        ex.GetType().Name);
                    _state.MarkFailed("Database initialization failed.");
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Cosmos database initialization cancelled during shutdown.");
        }
    }

    protected virtual async Task InitializeAsync(CancellationToken cancellationToken)
    {
        switch (_options.Mode)
        {
            case DatabaseInitializationMode.Ensure:
                await _cosmosDb.EnsureContainersAsync(cancellationToken);
                await PlanSeeder.SeedPlansAsync(_cosmosDb, cancellationToken);
                break;

            case DatabaseInitializationMode.ValidateOnly:
                await _cosmosDb.ValidateSchemaAsync(cancellationToken);
                break;

            default:
                throw new InvalidOperationException("Unsupported database initialization mode.");
        }
    }

    private static async Task DelayRetryAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        if (delay <= TimeSpan.Zero)
        {
            return;
        }

        try
        {
            await Task.Delay(delay, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) => TransientStatusCodes.Contains(statusCode);
}
