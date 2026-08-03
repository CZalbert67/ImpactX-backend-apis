using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Services;
using Moq;

namespace ImpactX.Tests.Unit;

public class MonitoringRelationshipConsolidationTests
{
    private readonly Mock<IMonitoringRelationshipRepository> _repository = new();
    private readonly Mock<IUsuarioRepository> _users = new();
    private readonly Mock<IFamilySubscriptionService> _family = new();

    [Fact]
    public async Task GetRelationships_ExpiredPendingInvitation_IsPersistedAsExpired()
    {
        var monitor = User();
        var monitored = User();
        var relationship = Relationship(
            monitor.Id,
            monitored.Id,
            DateTime.UtcNow.AddMinutes(-1));
        _repository.Setup(value => value.GetForUserAsync(
                monitored.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([relationship]);
        SetupUsers(monitor, monitored);
        var service = CreateService();

        var result = await service.GetRelationshipsAsync(monitored.Id);

        var dto = Assert.Single(result);
        Assert.Equal(MonitoringRelationshipStatus.Expired, dto.Status);
        Assert.Empty(relationship.InvitationCodeHash);
        _repository.Verify(value => value.UpdateAsync(
                relationship,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetRelationships_UnexpiredPendingInvitation_DoesNotWrite()
    {
        var monitor = User();
        var monitored = User();
        var relationship = Relationship(
            monitor.Id,
            monitored.Id,
            DateTime.UtcNow.AddDays(1));
        _repository.Setup(value => value.GetForUserAsync(
                monitored.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([relationship]);
        SetupUsers(monitor, monitored);
        var service = CreateService();

        var result = await service.GetRelationshipsAsync(monitored.Id);

        Assert.Equal(MonitoringRelationshipStatus.Pending, Assert.Single(result).Status);
        _repository.Verify(value => value.UpdateAsync(
                It.IsAny<MonitoringRelationship>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private MonitoringRelationshipService CreateService()
    {
        return new MonitoringRelationshipService(
            _repository.Object,
            _users.Object,
            _family.Object);
    }

    private void SetupUsers(Usuario monitor, Usuario monitored)
    {
        _users.Setup(value => value.GetByIdAsync(monitor.Id)).ReturnsAsync(monitor);
        _users.Setup(value => value.GetByIdAsync(monitored.Id)).ReturnsAsync(monitored);
    }

    private static Usuario User()
    {
        return new Usuario
        {
            Id = Guid.NewGuid(),
            PublicProfileId = $"USR-{Guid.NewGuid():N}",
            Username = $"user_{Guid.NewGuid():N}"[..20],
            Nombre = "Monitoring Test",
            Correo = $"{Guid.NewGuid():N}@test.com"
        };
    }

    private static MonitoringRelationship Relationship(
        Guid monitorUserId,
        Guid monitoredUserId,
        DateTime expiresAtUtc)
    {
        return new MonitoringRelationship
        {
            Id = Guid.NewGuid(),
            PublicRelationshipId = $"MON-{Guid.NewGuid():N}",
            MonitorUserId = monitorUserId,
            MonitoredUserId = monitoredUserId,
            InitiatedByUserId = monitorUserId,
            Direction = MonitoringRequestDirection.MonitorInvitesMonitored,
            Status = MonitoringRelationshipStatus.Pending,
            InvitationCodeHash = "hash",
            RequestedAtUtc = DateTime.UtcNow.AddDays(-7),
            ExpiresAtUtc = expiresAtUtc,
            UpdatedAtUtc = DateTime.UtcNow.AddDays(-7)
        };
    }
}
