using ImpactX.Core.Domain.Enums;
using ImpactX.Models.DTOs;
using ImpactX.Models.DTOs.Monitoring;

namespace ImpactX.Core.Interfaces.Services;

public interface IMonitoringRelationshipService
{
    Task<IReadOnlyList<MonitoringRelationshipDto>> GetRelationshipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CreateMonitoringInvitationResponse> CreateInvitationAsync(
        Guid monitorUserId,
        CreateMonitoringInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task AcceptAsync(
        Guid userId,
        AcceptMonitoringInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task RejectAsync(
        Guid userId,
        RespondMonitoringInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task BlockAsync(
        Guid monitoredUserId,
        string publicRelationshipId,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        Guid userId,
        string publicRelationshipId,
        CancellationToken cancellationToken = default);

    Task<MonitoringRelationshipDto> UpdatePermissionsAsync(
        Guid userId,
        string publicRelationshipId,
        UpdateMonitoringPermissionsRequest request,
        CancellationToken cancellationToken = default);

    Task<Guid> ResolveAuthorizedMonitoredUserIdAsync(
        Guid monitorUserId,
        string publicRelationshipId,
        MonitoringResourcePermission permission,
        CancellationToken cancellationToken = default);

    Task<MedicalProfileDto> GetAuthorizedMedicalProfileAsync(
        Guid monitorUserId,
        string publicRelationshipId,
        CancellationToken cancellationToken = default);

    Task<bool> CanMessageAsync(
        Guid senderUserId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task<MonitoringRelationshipDto> GetAcceptedBetweenAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default);
}
