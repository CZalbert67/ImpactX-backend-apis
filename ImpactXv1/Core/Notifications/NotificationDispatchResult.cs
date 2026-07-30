namespace ImpactX.Core.Notifications;

public sealed record NotificationDispatchResult(
    Guid NotificationId,
    Guid RecipientUserId,
    string Status,
    bool Sent);
