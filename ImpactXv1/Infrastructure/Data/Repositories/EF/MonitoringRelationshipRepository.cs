using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ImpactX.Infrastructure.Data.Repositories.EF;

public class MonitoringRelationshipRepository : IMonitoringRelationshipRepository
{
    private readonly ApplicationDbContext _context;

    public MonitoringRelationshipRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<MonitoringRelationship?> GetByPublicIdAsync(
        string publicRelationshipId,
        CancellationToken cancellationToken = default)
    {
        return _context.MonitoringRelationships.FirstOrDefaultAsync(
            relationship => relationship.PublicRelationshipId == publicRelationshipId,
            cancellationToken);
    }

    public Task<MonitoringRelationship?> GetByInvitationCodeHashAsync(
        string codeHash,
        CancellationToken cancellationToken = default)
    {
        return _context.MonitoringRelationships.FirstOrDefaultAsync(
            relationship => relationship.InvitationCodeHash == codeHash
                && relationship.Status == MonitoringRelationshipStatus.Pending,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MonitoringRelationship>> GetForUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.MonitoringRelationships
            .Where(relationship => relationship.MonitorUserId == userId
                || relationship.MonitoredUserId == userId)
            .OrderByDescending(relationship => relationship.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountAcceptedByMonitorAsync(
        Guid monitorUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.MonitoringRelationships.CountAsync(
            relationship => relationship.MonitorUserId == monitorUserId
                && relationship.Status == MonitoringRelationshipStatus.Accepted,
            cancellationToken);
    }

    public Task<int> CountAcceptedForMonitoredAsync(
        Guid monitoredUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.MonitoringRelationships.CountAsync(
            relationship => relationship.MonitoredUserId == monitoredUserId
                && relationship.Status == MonitoringRelationshipStatus.Accepted,
            cancellationToken);
    }

    public async Task<IReadOnlyList<MonitoringRelationship>> GetAcceptedForMonitoredUserAsync(
        Guid monitoredUserId,
        CancellationToken cancellationToken = default)
    {
        return await _context.MonitoringRelationships
            .Where(relationship => relationship.MonitoredUserId == monitoredUserId
                && relationship.Status == MonitoringRelationshipStatus.Accepted)
            .OrderBy(relationship => relationship.AcceptedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ExistsBlockedAsync(
        Guid monitorUserId,
        Guid monitoredUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.MonitoringRelationships.AnyAsync(
            relationship => relationship.MonitorUserId == monitorUserId
                && relationship.MonitoredUserId == monitoredUserId
                && relationship.Status == MonitoringRelationshipStatus.Blocked,
            cancellationToken);
    }

    public Task<bool> ExistsActiveOrPendingAsync(
        Guid monitorUserId,
        Guid monitoredUserId,
        CancellationToken cancellationToken = default)
    {
        return _context.MonitoringRelationships.AnyAsync(
            relationship => relationship.MonitorUserId == monitorUserId
                && relationship.MonitoredUserId == monitoredUserId
                && (relationship.Status == MonitoringRelationshipStatus.Pending
                    || relationship.Status == MonitoringRelationshipStatus.Accepted),
            cancellationToken);
    }

    public async Task AddAsync(
        MonitoringRelationship relationship,
        CancellationToken cancellationToken = default)
    {
        await _context.MonitoringRelationships.AddAsync(relationship, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        MonitoringRelationship relationship,
        CancellationToken cancellationToken = default)
    {
        _context.MonitoringRelationships.Update(relationship);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
