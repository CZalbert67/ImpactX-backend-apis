using ImpactX.Models.DTOs.QuickMessages;

namespace ImpactX.Core.Interfaces.Services;

public interface IQuickMessageService
{
    Task<IReadOnlyList<QuickMessageTemplateDto>> GetTemplatesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<QuickMessageTemplateDto> CreateTemplateAsync(
        Guid userId,
        UpsertQuickMessageTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task<QuickMessageTemplateDto> UpdateTemplateAsync(
        Guid userId,
        string publicTemplateId,
        UpsertQuickMessageTemplateRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteTemplateAsync(
        Guid userId,
        string publicTemplateId,
        CancellationToken cancellationToken = default);

    Task<QuickMessageDto> SendAsync(
        Guid senderUserId,
        SendQuickMessageRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuickMessageDto>> GetHistoryAsync(
        Guid userId,
        string? otherPublicProfileId,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task MarkReadAsync(
        Guid userId,
        string publicMessageId,
        CancellationToken cancellationToken = default);
}
