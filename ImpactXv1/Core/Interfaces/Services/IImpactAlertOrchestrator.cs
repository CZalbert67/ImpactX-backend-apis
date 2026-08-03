using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Services;

public interface IImpactAlertOrchestrator
{
    Task ProcessDetectedEventsAsync(
        Guid userId,
        Viaje trip,
        IReadOnlyList<ViajeTelemetry> detectedEvents,
        CancellationToken cancellationToken = default);

    Task<int> DispatchDuePendingAlertsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
