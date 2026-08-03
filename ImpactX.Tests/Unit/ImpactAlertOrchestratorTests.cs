using ImpactX.Core.Domain;
using ImpactX.Core.ImpactDetection;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Notifications;
using ImpactX.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ImpactX.Tests.Unit;

public sealed class ImpactAlertOrchestratorTests
{
    [Fact]
    public async Task ModerateCandidate_CreatesPendingAlertWithCancellationWindow()
    {
        var repository = NewRepository();
        Alerta? added = null;
        repository.Setup(value => value.AddAsync(It.IsAny<Alerta>()))
            .Callback<Alerta>(value => added = value)
            .Returns(Task.CompletedTask);
        var notifications = NewNotifications();
        var service = Create(repository, notifications);
        var telemetry = Candidate("moderate", 6);

        await service.ProcessDetectedEventsAsync(
            telemetry.UsuarioId,
            Trip(telemetry),
            [telemetry]);

        Assert.NotNull(added);
        Assert.Equal("Pendiente", added!.Estado);
        Assert.NotNull(added.AutoSendAtUtc);
        Assert.False(added.EsBypassCritico);
        Assert.Equal(telemetry.Id, added.SourceTelemetryEventId);
        notifications.Verify(value => value.NotifyAlertMonitorsAsync(
            It.IsAny<Alerta>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SevereCandidate_CreatesImmediateAlertAndNotifies()
    {
        var repository = NewRepository();
        Alerta? added = null;
        repository.Setup(value => value.AddAsync(It.IsAny<Alerta>()))
            .Callback<Alerta>(value => added = value)
            .Returns(Task.CompletedTask);
        var notifications = NewNotifications();
        var service = Create(repository, notifications);
        var telemetry = Candidate("severe", 8);

        await service.ProcessDetectedEventsAsync(
            telemetry.UsuarioId,
            Trip(telemetry),
            [telemetry]);

        Assert.NotNull(added);
        Assert.Equal("Enviada", added!.Estado);
        Assert.NotNull(added.EnviadaEn);
        Assert.Null(added.AutoSendAtUtc);
        Assert.True(added.EsBypassCritico);
        notifications.Verify(value => value.NotifyAlertMonitorsAsync(
            added,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExistingSourceEvent_DoesNotCreateDuplicateAlert()
    {
        var telemetry = Candidate("severe", 8);
        var repository = NewRepository();
        repository.Setup(value => value.GetBySourceTelemetryEventIdAsync(
                telemetry.UsuarioId,
                telemetry.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Alerta());
        var service = Create(repository, NewNotifications());

        await service.ProcessDetectedEventsAsync(
            telemetry.UsuarioId,
            Trip(telemetry),
            [telemetry]);

        repository.Verify(value => value.AddAsync(It.IsAny<Alerta>()), Times.Never);
    }

    [Fact]
    public async Task SevereSignal_PromotesRecentPendingAlert()
    {
        var telemetry = Candidate("severe", 8);
        var trip = Trip(telemetry);
        var pending = new Alerta
        {
            UsuarioId = telemetry.UsuarioId,
            ViajeId = trip.Id.ToString(),
            Estado = "Pendiente",
            Severidad = "moderate",
            CreadoEn = DateTime.UtcNow,
            AutoSendAtUtc = DateTime.UtcNow.AddSeconds(10)
        };
        var repository = NewRepository();
        repository.Setup(value => value.GetActiveByUserAsync(telemetry.UsuarioId))
            .ReturnsAsync(pending);
        var notifications = NewNotifications();
        var service = Create(repository, notifications);

        await service.ProcessDetectedEventsAsync(
            telemetry.UsuarioId,
            trip,
            [telemetry]);

        Assert.Equal("Enviada", pending.Estado);
        Assert.Equal("severe", pending.Severidad);
        Assert.Null(pending.AutoSendAtUtc);
        repository.Verify(value => value.UpdateAsync(pending), Times.Once);
        notifications.Verify(value => value.NotifyAlertMonitorsAsync(
            pending,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatchDuePendingAlerts_SendsOnlyDueItems()
    {
        var now = DateTime.UtcNow;
        var due = new Alerta
        {
            Estado = "Pendiente",
            AutoSendAtUtc = now.AddSeconds(-1)
        };
        var repository = NewRepository();
        repository.Setup(value => value.GetPendingDueAsync(
                now,
                100,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Alerta> { due });
        var notifications = NewNotifications();
        var service = Create(repository, notifications);

        var count = await service.DispatchDuePendingAlertsAsync(now);

        Assert.Equal(1, count);
        Assert.Equal("Enviada", due.Estado);
        Assert.Equal(now, due.EnviadaEn);
        repository.Verify(value => value.UpdateAsync(due), Times.Once);
        notifications.Verify(value => value.NotifyAlertMonitorsAsync(
            due,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<IAlertaRepository> NewRepository()
    {
        var repository = new Mock<IAlertaRepository>();
        repository.Setup(value => value.GetBySourceTelemetryEventIdAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Alerta?)null);
        repository.Setup(value => value.GetActiveByUserAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Alerta?)null);
        repository.Setup(value => value.UpdateAsync(It.IsAny<Alerta>()))
            .Returns(Task.CompletedTask);
        return repository;
    }

    private static Mock<INotificationService> NewNotifications()
    {
        var notifications = new Mock<INotificationService>();
        notifications.Setup(value => value.NotifyAlertMonitorsAsync(
                It.IsAny<Alerta>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<NotificationDispatchResult>());
        return notifications;
    }

    private static ImpactAlertOrchestrator Create(
        Mock<IAlertaRepository> repository,
        Mock<INotificationService> notifications)
    {
        return new ImpactAlertOrchestrator(
            repository.Object,
            notifications.Object,
            Options.Create(new ImpactDetectionOptions
            {
                Enabled = true,
                PendingDispatchWorkerEnabled = false,
                ActiveAlertCooldownSeconds = 60
            }),
            NullLogger<ImpactAlertOrchestrator>.Instance);
    }

    private static ViajeTelemetry Candidate(string severity, int score)
    {
        return new ViajeTelemetry
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            ViajeId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            Lat = 19.43,
            Lng = -99.13,
            MagnitudAceleracion = 32,
            FrecuenciaCardiaca = 150,
            ImpactCandidate = true,
            DetectionLabel = "impact_candidate",
            SeverityLabel = severity,
            RuleVersion = ImpactDetectionEngine.CurrentRuleVersion,
            DetectionScore = score
        };
    }

    private static Viaje Trip(ViajeTelemetry telemetry)
    {
        return new Viaje
        {
            Id = telemetry.ViajeId,
            UsuarioId = telemetry.UsuarioId,
            Estado = "Activo"
        };
    }
}
