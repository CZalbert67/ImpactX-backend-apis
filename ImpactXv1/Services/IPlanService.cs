using ImpactX.Core.Exceptions;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IPlanService
{
    Task<List<PlanDto>> GetAllPlansAsync();
    Task<SuscripcionDto?> GetCurrentSubscriptionAsync(Guid usuarioId);
    Task<List<SuscripcionDto>> GetSubscriptionHistoryAsync(Guid usuarioId);
    Task<SuscripcionDto> ChangePlanAsync(Guid usuarioId, ChangePlanRequest request);
    Task<SuscripcionDto> CancelSubscriptionAsync(Guid usuarioId, CancelSubscriptionRequest? request);
    Task<List<PagoDto>> GetPaymentsAsync(Guid usuarioId);
    Task<PagoDto?> GetPaymentReceiptAsync(Guid id, Guid usuarioId);
    Task<int> ExpireSubscriptionsAsync();
}
