using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IAnalyticsService
{
    Task<DashboardDto> GetDashboardAsync(Guid usuarioId);
    Task<List<IncidentTrendPointDto>> GetIncidentTrendAsync(Guid usuarioId, TrendFilterRequest filter);
    Task<TripSummaryDto> GetTripSummaryAsync(Guid usuarioId);
}
