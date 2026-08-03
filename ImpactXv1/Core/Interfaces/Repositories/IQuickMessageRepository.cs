using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IQuickMessageRepository
{
    Task<IReadOnlyList<QuickMessageTemplate>> GetTemplatesByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task<QuickMessageTemplate?> GetTemplateByPublicIdAsync(
        Guid ownerUserId,
        string publicTemplateId,
        CancellationToken cancellationToken = default);

    Task<int> CountActiveTemplatesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    Task AddTemplateAsync(
        QuickMessageTemplate template,
        CancellationToken cancellationToken = default);

    Task UpdateTemplateAsync(
        QuickMessageTemplate template,
        CancellationToken cancellationToken = default);

    Task AddMessageAsync(
        QuickMessage message,
        CancellationToken cancellationToken = default);

    Task<QuickMessage?> GetMessageForRecipientAsync(
        Guid recipientUserId,
        string publicMessageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<QuickMessage>> GetHistoryAsync(
        Guid userId,
        Guid? otherUserId,
        CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default);

    Task<int> MarkConversationReadAsync(
        Guid recipientUserId,
        Guid senderUserId,
        DateTime readAtUtc,
        CancellationToken cancellationToken = default);

    Task UpdateMessageAsync(
        QuickMessage message,
        CancellationToken cancellationToken = default);
}
