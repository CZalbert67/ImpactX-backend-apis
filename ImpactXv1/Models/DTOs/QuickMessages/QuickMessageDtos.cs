using System.ComponentModel.DataAnnotations;

namespace ImpactX.Models.DTOs.QuickMessages;

public class UpsertQuickMessageTemplateRequest
{
    [Required, MaxLength(160)]
    public string Text { get; set; } = string.Empty;

    [Range(0, 1000)]
    public int SortOrder { get; set; }
}

public class SendQuickMessageRequest
{
    [Required, MaxLength(64)]
    public string RecipientPublicProfileId { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string PublicTemplateId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string? RoutePublicId { get; set; }

    [MaxLength(64)]
    public string? IncidentPublicId { get; set; }
}

public class QuickMessageTemplateDto
{
    public string PublicTemplateId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
}

public class QuickMessageDto
{
    public string PublicMessageId { get; set; } = string.Empty;
    public string SenderPublicProfileId { get; set; } = string.Empty;
    public string SenderUsername { get; set; } = string.Empty;
    public string RecipientPublicProfileId { get; set; } = string.Empty;
    public string RecipientUsername { get; set; } = string.Empty;
    public string PublicRelationshipId { get; set; } = string.Empty;
    public string PublicTemplateId { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string? RoutePublicId { get; set; }
    public string? IncidentPublicId { get; set; }
    public DateTime SentAtUtc { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
}

public class QuickMessageRecipientDto
{
    public string PublicRelationshipId { get; set; } = string.Empty;
    public string RecipientPublicProfileId { get; set; } = string.Empty;
    public string RecipientUsername { get; set; } = string.Empty;
    public string RecipientName { get; set; } = string.Empty;
}
