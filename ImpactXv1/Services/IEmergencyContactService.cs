using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IEmergencyContactService
{
    Task<PagedResult<EmergencyContactDto>> GetContactsPagedAsync(
        Guid userId,
        int? pageSize,
        string? continuationToken,
        CancellationToken cancellationToken = default);

    Task<EmergencyContactDto> GetByPublicIdAsync(
        Guid userId,
        string publicContactId,
        CancellationToken cancellationToken = default);

    Task<CreateEmergencyContactInvitationResponse> CreateInvitationAsync(
        Guid ownerUserId,
        CreateEmergencyContactInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task AcceptInvitationAsync(
        Guid userId,
        RespondEmergencyContactInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task RejectInvitationAsync(
        Guid userId,
        RespondEmergencyContactInvitationRequest request,
        CancellationToken cancellationToken = default);

    Task<EmergencyContactDto> UpdateAsync(
        Guid ownerUserId,
        string publicContactId,
        UpdateEmergencyContactRequest request,
        CancellationToken cancellationToken = default);

    Task<EmergencyContactDto> MakePrimaryAsync(
        Guid ownerUserId,
        string publicContactId,
        CancellationToken cancellationToken = default);

    Task RevokeAsync(
        Guid userId,
        string publicContactId,
        CancellationToken cancellationToken = default);

    Task BlockAsync(
        Guid userId,
        string publicContactId,
        CancellationToken cancellationToken = default);

    Task<EmergencyContactSyncResponse> GetSyncAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
