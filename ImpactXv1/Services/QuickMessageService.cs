using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Identity;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.QuickMessages;
using ImpactX.Models.DTOs.QuickMessages;

namespace ImpactX.Services;

public class QuickMessageService : IQuickMessageService
{
    private const int MaxCustomTemplates = 10;
    private const int MaxTextLength = 160;

    private readonly IQuickMessageRepository _repository;
    private readonly IMonitoringRelationshipService _monitoringService;
    private readonly IUsuarioRepository _usuarioRepository;

    public QuickMessageService(
        IQuickMessageRepository repository,
        IMonitoringRelationshipService monitoringService,
        IUsuarioRepository usuarioRepository)
    {
        _repository = repository;
        _monitoringService = monitoringService;
        _usuarioRepository = usuarioRepository;
    }

    public async Task<IReadOnlyList<QuickMessageTemplateDto>> GetTemplatesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var system = SystemQuickMessageTemplates.All.Select(value => new QuickMessageTemplateDto
        {
            PublicTemplateId = value.PublicTemplateId,
            Text = value.Text,
            SortOrder = value.SortOrder,
            IsSystem = true
        });
        var custom = (await _repository.GetTemplatesByOwnerAsync(userId, cancellationToken))
            .Select(MapTemplate);
        return system.Concat(custom)
            .OrderBy(value => value.IsSystem ? 0 : 1)
            .ThenBy(value => value.SortOrder)
            .ThenBy(value => value.Text)
            .ToList();
    }

    public async Task<QuickMessageTemplateDto> CreateTemplateAsync(
        Guid userId,
        UpsertQuickMessageTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var text = ValidateText(request.Text);
        var count = await _repository.CountActiveTemplatesAsync(userId, cancellationToken);
        if (count >= MaxCustomTemplates)
        {
            throw new ConflictException("Solo puedes tener 10 plantillas personalizadas activas.");
        }

        var now = DateTime.UtcNow;
        var template = new QuickMessageTemplate
        {
            Id = Guid.NewGuid(),
            PublicTemplateId = QuickMessagePublicIdGenerator.GenerateTemplateId(),
            OwnerUserId = userId,
            OwnerKey = userId.ToString(),
            Text = text,
            SortOrder = request.SortOrder,
            Active = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        await _repository.AddTemplateAsync(template, cancellationToken);
        return MapTemplate(template);
    }

    public async Task<QuickMessageTemplateDto> UpdateTemplateAsync(
        Guid userId,
        string publicTemplateId,
        UpsertQuickMessageTemplateRequest request,
        CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetTemplateByPublicIdAsync(
            userId,
            publicTemplateId,
            cancellationToken)
            ?? throw new NotFoundException("Plantilla no encontrada.");
        template.Text = ValidateText(request.Text);
        template.SortOrder = request.SortOrder;
        template.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateTemplateAsync(template, cancellationToken);
        return MapTemplate(template);
    }

    public async Task DeleteTemplateAsync(
        Guid userId,
        string publicTemplateId,
        CancellationToken cancellationToken = default)
    {
        var template = await _repository.GetTemplateByPublicIdAsync(
            userId,
            publicTemplateId,
            cancellationToken)
            ?? throw new NotFoundException("Plantilla no encontrada.");
        template.Active = false;
        template.DeletedAtUtc = DateTime.UtcNow;
        template.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateTemplateAsync(template, cancellationToken);
    }

    public async Task<QuickMessageDto> SendAsync(
        Guid senderUserId,
        SendQuickMessageRequest request,
        CancellationToken cancellationToken = default)
    {
        var recipientPublicProfileId = RequirePublicId(
            request.RecipientPublicProfileId,
            "Destinatario no encontrado.");
        var publicTemplateId = RequirePublicId(
            request.PublicTemplateId,
            "Plantilla no encontrada.");

        var sender = await GetUserAsync(senderUserId);
        var recipient = await _usuarioRepository.GetByPublicProfileIdAsync(
            recipientPublicProfileId)
            ?? throw new NotFoundException("Destinatario no encontrado.");
        if (recipient.Id == senderUserId)
        {
            throw new BadRequestException("No puedes enviarte un mensaje a ti mismo.");
        }

        var relationship = await _monitoringService.GetAcceptedBetweenAsync(
            senderUserId,
            recipient.Id,
            cancellationToken);
        if (!relationship.Permissions.SendMessages)
        {
            throw new ForbiddenException("La relación no permite enviar mensajes.");
        }

        var templateText = await ResolveTemplateTextAsync(
            senderUserId,
            publicTemplateId,
            cancellationToken);
        var message = new QuickMessage
        {
            Id = Guid.NewGuid(),
            PublicMessageId = QuickMessagePublicIdGenerator.GenerateMessageId(),
            SenderUserId = senderUserId,
            RecipientUserId = recipient.Id,
            PublicRelationshipId = relationship.PublicRelationshipId,
            PublicTemplateId = publicTemplateId,
            TextSnapshot = templateText,
            RoutePublicId = NormalizeOptionalId(request.RoutePublicId),
            IncidentPublicId = NormalizeOptionalId(request.IncidentPublicId),
            SentAtUtc = DateTime.UtcNow,
            IsRead = false
        };
        await _repository.AddMessageAsync(message, cancellationToken);
        return MapMessage(message, sender, recipient);
    }

    public async Task<IReadOnlyList<QuickMessageDto>> GetHistoryAsync(
        Guid userId,
        string? otherPublicProfileId,
        CancellationToken cancellationToken = default)
    {
        Guid? otherUserId = null;
        if (!string.IsNullOrWhiteSpace(otherPublicProfileId))
        {
            var other = await _usuarioRepository.GetByPublicProfileIdAsync(
                otherPublicProfileId.Trim())
                ?? throw new NotFoundException("Usuario no encontrado.");
            otherUserId = other.Id;
        }

        var messages = await _repository.GetHistoryAsync(userId, otherUserId, cancellationToken);
        var users = new Dictionary<Guid, Usuario>();
        var result = new List<QuickMessageDto>(messages.Count);
        foreach (var message in messages)
        {
            var sender = await GetCachedUserAsync(message.SenderUserId, users);
            var recipient = await GetCachedUserAsync(message.RecipientUserId, users);
            result.Add(MapMessage(message, sender, recipient));
        }

        return result;
    }

    public Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _repository.CountUnreadAsync(userId, cancellationToken);
    }

    public async Task MarkReadAsync(
        Guid userId,
        string publicMessageId,
        CancellationToken cancellationToken = default)
    {
        var message = await _repository.GetMessageForRecipientAsync(
            userId,
            publicMessageId,
            cancellationToken)
            ?? throw new NotFoundException("Mensaje no encontrado.");
        if (message.IsRead)
        {
            return;
        }

        message.IsRead = true;
        message.ReadAtUtc = DateTime.UtcNow;
        await _repository.UpdateMessageAsync(message, cancellationToken);
    }

    private async Task<string> ResolveTemplateTextAsync(
        Guid userId,
        string publicTemplateId,
        CancellationToken cancellationToken)
    {
        var system = SystemQuickMessageTemplates.Find(publicTemplateId);
        if (system is not null)
        {
            return system.Text;
        }

        var custom = await _repository.GetTemplateByPublicIdAsync(
            userId,
            publicTemplateId,
            cancellationToken)
            ?? throw new NotFoundException("Plantilla no encontrada.");
        return custom.Text;
    }

    private async Task<Usuario> GetUserAsync(Guid userId)
    {
        return await _usuarioRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("Usuario no encontrado.");
    }

    private async Task<Usuario> GetCachedUserAsync(
        Guid userId,
        IDictionary<Guid, Usuario> cache)
    {
        if (cache.TryGetValue(userId, out var cached))
        {
            return cached;
        }

        var user = await GetUserAsync(userId);
        cache[userId] = user;
        return user;
    }

    private static string ValidateText(string? text)
    {
        var normalized = text?.Trim() ?? string.Empty;
        if (normalized.Length is 0 or > MaxTextLength)
        {
            throw new BadRequestException("La plantilla debe contener entre 1 y 160 caracteres.");
        }

        return normalized;
    }

    private static string RequirePublicId(string? value, string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new NotFoundException(errorMessage);
        }

        return value.Trim();
    }

    private static string? NormalizeOptionalId(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static QuickMessageTemplateDto MapTemplate(QuickMessageTemplate template)
    {
        return new QuickMessageTemplateDto
        {
            PublicTemplateId = template.PublicTemplateId,
            Text = template.Text,
            SortOrder = template.SortOrder,
            IsSystem = false
        };
    }

    private static QuickMessageDto MapMessage(
        QuickMessage message,
        Usuario sender,
        Usuario recipient)
    {
        return new QuickMessageDto
        {
            PublicMessageId = message.PublicMessageId,
            SenderPublicProfileId = sender.PublicProfileId,
            SenderUsername = sender.Username,
            RecipientPublicProfileId = recipient.PublicProfileId,
            RecipientUsername = recipient.Username,
            PublicRelationshipId = message.PublicRelationshipId,
            PublicTemplateId = message.PublicTemplateId,
            Text = message.TextSnapshot,
            RoutePublicId = message.RoutePublicId,
            IncidentPublicId = message.IncidentPublicId,
            SentAtUtc = message.SentAtUtc,
            IsRead = message.IsRead,
            ReadAtUtc = message.ReadAtUtc
        };
    }
}
