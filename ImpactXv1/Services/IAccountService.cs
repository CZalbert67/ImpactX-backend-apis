using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IAccountService
{
    Task<AccountExportV2Dto> ExportAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AccountRetentionDto> GetRetentionAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserProfileDto> RevokeConsentsAsync(Guid userId, RevokeConsentsRequest request, CancellationToken cancellationToken = default);
    Task<DeleteAccountV2Response> DeleteAsync(Guid userId, DeleteAccountV2Request request, CancellationToken cancellationToken = default);
}
