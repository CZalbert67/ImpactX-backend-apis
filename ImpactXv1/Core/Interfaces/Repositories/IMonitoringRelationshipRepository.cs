using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IMonitoringRelationshipRepository
{
    Task<MonitoringRelationship?> GetByPublicIdAsync(
        string publicRelationshipId,
        CancellationToken cancellationToken = default);

    Task<MonitoringRelationship?> GetByInvitationCodeHashAsync(
        string codeHash,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonitoringRelationship>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<int> CountAcceptedByMonitorAsync(
        Guid monitorUserId,
        CancellationToken cancellationToken = default);

    Task<int> CountAcceptedForMonitoredAsync(
        Guid monitoredUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonitoringRelationship>> GetAcceptedForMonitoredUserAsync(
        Guid monitoredUserId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsBlockedAsync(
        Guid monitorUserId,
        Guid monitoredUserId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsActiveOrPendingAsync(
        Guid monitorUserId,
        Guid monitoredUserId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        MonitoringRelationship relationship,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        MonitoringRelationship relationship,
        CancellationToken cancellationToken = default);
}
