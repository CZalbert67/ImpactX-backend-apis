using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class QuickMessageRepository : IQuickMessageRepository
{
    private readonly ApplicationDbContext _context;

    public QuickMessageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<QuickMessageTemplate>> GetTemplatesByOwnerAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return await _context.QuickMessageTemplates
            .Where(template => template.OwnerUserId == ownerUserId && template.Active)
            .OrderBy(template => template.SortOrder)
            .ThenBy(template => template.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<QuickMessageTemplate?> GetTemplateByPublicIdAsync(
        Guid ownerUserId,
        string publicTemplateId,
        CancellationToken cancellationToken = default)
    {
        return _context.QuickMessageTemplates.FirstOrDefaultAsync(
            template => template.OwnerUserId == ownerUserId
                && template.PublicTemplateId == publicTemplateId
                && template.Active,
            cancellationToken);
    }

    public Task<int> CountActiveTemplatesAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.QuickMessageTemplates.CountAsync(
            template => template.OwnerUserId == ownerUserId && template.Active,
            cancellationToken);
    }

    public async Task AddTemplateAsync(
        QuickMessageTemplate template,
        CancellationToken cancellationToken = default)
    {
        await _context.QuickMessageTemplates.AddAsync(template, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateTemplateAsync(
        QuickMessageTemplate template,
        CancellationToken cancellationToken = default)
    {
        _context.QuickMessageTemplates.Update(template);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddMessageAsync(
        QuickMessage message,
        CancellationToken cancellationToken = default)
    {
        await _context.QuickMessages.AddAsync(message, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<QuickMessage?> GetMessageForRecipientAsync(
        Guid recipientUserId,
        string publicMessageId,
        CancellationToken cancellationToken = default)
    {
        return _context.QuickMessages.FirstOrDefaultAsync(
            message => message.RecipientUserId == recipientUserId
                && message.PublicMessageId == publicMessageId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<QuickMessage>> GetHistoryAsync(
        Guid userId,
        Guid? otherUserId,
        CancellationToken cancellationToken = default)
    {
        var query = _context.QuickMessages.Where(message =>
            message.SenderUserId == userId || message.RecipientUserId == userId);
        if (otherUserId.HasValue)
        {
            var other = otherUserId.Value;
            query = query.Where(message =>
                (message.SenderUserId == userId && message.RecipientUserId == other)
                || (message.SenderUserId == other && message.RecipientUserId == userId));
        }

        return await query
            .OrderByDescending(message => message.SentAtUtc)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountUnreadAsync(
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.QuickMessages.CountAsync(
            message => message.RecipientUserId == recipientUserId && !message.IsRead,
            cancellationToken);
    }

    public async Task UpdateMessageAsync(
        QuickMessage message,
        CancellationToken cancellationToken = default)
    {
        _context.QuickMessages.Update(message);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
