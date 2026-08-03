using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IMobileSyncService
{
    Task<MobileSyncSnapshotDto> GetBootstrapAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<MobileSyncChangesDto> GetChangesAsync(
        Guid userId,
        string? cursor,
        CancellationToken cancellationToken = default);

    Task<MobileSyncPushResponse> PushAsync(
        Guid userId,
        MobileSyncPushRequest request,
        CancellationToken cancellationToken = default);

    Task<MobileSyncAckResponse> AcknowledgeAsync(
        Guid userId,
        MobileSyncAckRequest request,
        CancellationToken cancellationToken = default);
}
