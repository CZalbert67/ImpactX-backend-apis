using ImpactX.Core.Domain.Enums;
using ImpactX.Models.DTOs.FamilySubscriptions;
using ImpactX.Models.DTOs.Monitoring;

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

    Task<IReadOnlyList<IncomingFamilyInvitationDto>> GetIncomingInvitationsAsync(
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

    Task RevokeInvitationAsync(
        Guid ownerUserId,
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

    Task<IReadOnlyList<MonitoringRelationshipDto>> GetUnifiedRelationshipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<MonitoringRelationshipDto?> TryGetUnifiedRelationshipAsync(
        Guid participantUserId,
        string publicRelationshipId,
        CancellationToken cancellationToken = default);

    Task<MonitoringRelationshipDto?> TryUpdateUnifiedPermissionsAsync(
        Guid subjectUserId,
        string publicRelationshipId,
        UpdateMonitoringPermissionsRequest request,
        CancellationToken cancellationToken = default);

    Task<Guid?> TryResolveUnifiedAuthorizedUserIdAsync(
        Guid viewerUserId,
        string publicRelationshipId,
        MonitoringResourcePermission permission,
        CancellationToken cancellationToken = default);

    Task<bool> CanUnifiedMembersMessageAsync(
        Guid senderUserId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task<MonitoringRelationshipDto?> TryGetUnifiedRelationshipBetweenAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FamilyMemberAccessDto>> GetMemberAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<FamilyMemberAccessDto> UpdateMemberAccessAsync(
        Guid userId,
        string targetPublicProfileId,
        UpdateFamilyMemberAccessRequest request,
        CancellationToken cancellationToken = default);

    Task<int> ProcessLifecycleAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default);
}
