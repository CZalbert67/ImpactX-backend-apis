using System;
using System.Collections.Generic;

namespace ImpactX.Core.Notifications;

public sealed record AppNotificationCommand(
    Guid RecipientUserId,
    string Title,
    string Message,
    string Type,
    string Event,
    string? PublicRelationshipId,
    string? EntityId,
    string? ReferenceType,
    string? DeepLink,
    string IdempotencyKey,
    IReadOnlyDictionary<string, string>? Data = null);
