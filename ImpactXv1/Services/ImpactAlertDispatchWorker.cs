using ImpactX.Core.ImpactDetection;
using ImpactX.Core.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace ImpactX.Services;

public sealed class ImpactAlertDispatchWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ImpactDetectionOptions _options;
    private readonly ILogger<ImpactAlertDispatchWorker> _logger;

    public ImpactAlertDispatchWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ImpactDetectionOptions> options,
        ILogger<ImpactAlertDispatchWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.PendingDispatchWorkerEnabled)
            return;

        var delay = TimeSpan.FromSeconds(Math.Clamp(_options.PollIntervalSeconds, 1, 60));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IImpactAlertOrchestrator>();
                await orchestrator.DispatchDuePendingAlertsAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló el despacho de alertas pendientes del motor de impacto");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
