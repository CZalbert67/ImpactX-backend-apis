using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IPlanService
{
    Task<List<PlanDto>> GetAllPlansAsync();
    Task<SuscripcionDto?> GetCurrentSubscriptionAsync(Guid usuarioId);
    Task<EffectiveSubscriptionDto> GetEffectiveSubscriptionAsync(Guid usuarioId, CancellationToken cancellationToken = default);
    Task<List<SuscripcionDto>> GetSubscriptionHistoryAsync(Guid usuarioId);
    Task<PagedResult<SuscripcionDto>> GetSubscriptionHistoryPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken);
    Task<SuscripcionDto> ChangePlanAsync(Guid usuarioId, ChangePlanRequest request);
    Task<SubscriptionPaymentResultDto> ActivateAsync(Guid usuarioId, ChangePlanRequest request);
    Task<SubscriptionPaymentResultDto> RenewAsync(Guid usuarioId, RenewSubscriptionRequest request);
    Task<SuscripcionDto> CancelSubscriptionAsync(Guid usuarioId, CancelSubscriptionRequest? request);
    Task<List<PagoDto>> GetPaymentsAsync(Guid usuarioId);
    Task<PagedResult<PagoDto>> GetPaymentsPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken);
    Task<PagoDto?> GetPaymentReceiptAsync(Guid id, Guid usuarioId);
    Task<int> ExpireSubscriptionsAsync();
    Task<int> ProcessLifecycleAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
