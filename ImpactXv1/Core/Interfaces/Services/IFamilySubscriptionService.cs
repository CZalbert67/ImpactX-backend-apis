using ImpactX.Models.DTOs.FamilySubscriptions;

namespace ImpactX.Core.Interfaces.Services;

public interface IFamilySubscriptionService
{
    Task<FamilySubscriptionSummaryDto?> GetCurrentAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<FamilySubscriptionSummaryDto> ActivateAsync(
        Guid userId,
        ActivateFamilySubscriptionRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilySubscriptionSummaryDto> ChangePlanAsync(
        Guid userId,
        ChangeFamilyPlanRequest request,
        CancellationToken cancellationToken = default);

    Task<FamilySubscriptionSummaryDto> RenewAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task CancelAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FamilyMemberDto>> GetMembersAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FamilyInvitationDto>> GetInvitationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CreateFamilyInvitationResponse> CreateInvitationAsync(
        Guid userId,
        CreateFamilyInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task AcceptInvitationAsync(
        Guid userId,
        string publicInvitationId,
        CancellationToken cancellationToken = default);

    Task RedeemInvitationCodeAsync(
        Guid userId,
        RedeemFamilyInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task RejectInvitationAsync(
        Guid userId,
        string publicInvitationId,
        CancellationToken cancellationToken = default);

    Task RemoveMemberAsync(
        Guid ownerUserId,
        string publicMembershipId,
        CancellationToken cancellationToken = default);

    Task LeaveAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<string> GetEffectivePlanNameAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<int> ProcessLifecycleAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
