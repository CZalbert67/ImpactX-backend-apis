using ImpactX.Configuration;
using Microsoft.Extensions.Options;

namespace ImpactX.Services;

public sealed class SubscriptionLifecycleWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SubscriptionLifecycleOptions _options;
    private readonly ILogger<SubscriptionLifecycleWorker> _logger;

    public SubscriptionLifecycleWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<SubscriptionLifecycleOptions> options,
        ILogger<SubscriptionLifecycleWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
            return;

        var delay = TimeSpan.FromMinutes(Math.Clamp(_options.PollIntervalMinutes, 1, 1440));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var now = DateTime.UtcNow;
                var plans = scope.ServiceProvider.GetRequiredService<IPlanService>();
                var families = scope.ServiceProvider.GetRequiredService<ImpactX.Core.Interfaces.Services.IFamilySubscriptionService>();
                var individualCount = await plans.ProcessLifecycleAsync(now, stoppingToken);
                var familyCount = await families.ProcessLifecycleAsync(now, stoppingToken);
                var processed = individualCount + familyCount;
                if (processed > 0)
                {
                    _logger.LogInformation(
                        "Se procesaron {IndividualCount} suscripciones individuales y {FamilyCount} familiares.",
                        individualCount,
                        familyCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falló el ciclo de vigencia de suscripciones.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }
}
