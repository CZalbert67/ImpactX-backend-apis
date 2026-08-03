using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Notifications;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ImpactX.Tests.Unit;

public class AlertServiceTests
{
    private readonly Mock<IAlertaRepository> _alertaRepo;
    private readonly Mock<IIncidenteRepository> _incidenteRepo;
    private readonly Mock<IPlanService> _planService;
    private readonly Mock<INotificationService> _notificationService;
    private readonly AlertService _alertService;

    public AlertServiceTests()
    {
        _alertaRepo = new Mock<IAlertaRepository>();
        _incidenteRepo = new Mock<IIncidenteRepository>();
        _planService = new Mock<IPlanService>();
        _notificationService = new Mock<INotificationService>();
        var logger = Mock.Of<ILogger<AlertService>>();
        _alertService = new AlertService(_alertaRepo.Object, _incidenteRepo.Object, _planService.Object, _notificationService.Object, logger);
    }

    [Fact]
    public async Task DetectAsync_CreatesPendingAlert()
    {
        var usuarioId = Guid.NewGuid();

        var result = await _alertService.DetectAsync(usuarioId, new DetectAlertRequest
        {
            Lat = 19.43,
            Lng = -99.13,
            Severidad = "bump",
            GForce = 2.5,
            Decibeles = 85,
            FrecuenciaCardiaca = 95,
        });

        Assert.Equal("Impacto", result.Tipo);
        Assert.Equal("bump", result.Severidad);
        Assert.Equal("Pendiente", result.Estado);
        Assert.Equal(19.43, result.Lat);
        Assert.Equal(-99.13, result.Lng);
        Assert.Single(result.Timeline);
        _alertaRepo.Verify(r => r.AddAsync(It.IsAny<Alerta>()), Times.Once);
    }

    [Fact]
    public async Task DetectAsync_WithSevereImpact_CreatesAlert()
    {
        var usuarioId = Guid.NewGuid();

        var result = await _alertService.DetectAsync(usuarioId, new DetectAlertRequest
        {
            Lat = 19.43,
            Lng = -99.13,
            Severidad = "crash",
            GForce = 5.0,
            Decibeles = 110,
            FrecuenciaCardiaca = 120,
            ViajeId = "trip-001",
        });

        Assert.Equal("crash", result.Severidad);
        Assert.Equal("trip-001", result.ViajeId);
    }

    [Fact]
    public async Task SendSosAsync_CreatesAlertInEnviadaState()
    {
        var usuarioId = Guid.NewGuid();

        var result = await _alertService.SendSosAsync(usuarioId, new SosRequest
        {
            Lat = 19.43,
            Lng = -99.13,
            Severidad = "severe",
            Canal = "manual",
            Modo = "manual",
        });

        Assert.Equal("SOS", result.Tipo);
        Assert.Equal("Enviada", result.Estado);
        Assert.NotNull(result.EnviadaEn);
        Assert.True(result.EsBypassCritico);
        _alertaRepo.Verify(r => r.AddAsync(It.IsAny<Alerta>()), Times.Once);
    }

    [Fact]
    public async Task SendSosAsync_WithImmediateMode_SetsBypass()
    {
        var usuarioId = Guid.NewGuid();

        var result = await _alertService.SendSosAsync(usuarioId, new SosRequest
        {
            Lat = 19.43,
            Lng = -99.13,
            Severidad = "bump",
            Modo = "immediate",
        });

        Assert.True(result.EsBypassCritico);
    }

    [Fact]
    public async Task ConfirmOkAsync_WithPendingAlert_Cancels()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Pendiente" };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        var result = await _alertService.ConfirmOkAsync(usuarioId, alerta.Id);

        Assert.True(result.EsFalsaAlarma);
        Assert.Equal("FalsaAlarma", alerta.Estado);
        Assert.True(alerta.EsFalsaAlarma);
        Assert.NotNull(alerta.ConfirmadaEn);
        Assert.NotNull(alerta.CerradaEn);
        _alertaRepo.Verify(r => r.UpdateAsync(alerta), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ConfirmOkAsync_WithClosedAlert_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Cerrada" };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _alertService.ConfirmOkAsync(usuarioId, alerta.Id));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task ConfirmOkAsync_WithWrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = Guid.NewGuid(), Estado = "Pendiente" };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _alertService.ConfirmOkAsync(usuarioId, alerta.Id));
    }

    [Fact]
    public async Task BypassCriticalAsync_SetsBypassAndSent()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Enviada" };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        var result = await _alertService.BypassCriticalAsync(usuarioId, alerta.Id);

        Assert.True(alerta.EsBypassCritico);
        Assert.Equal("Enviada", alerta.Estado);
        Assert.Equal("Enviada", result.Estado);
        _alertaRepo.Verify(r => r.UpdateAsync(alerta), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task BypassCriticalAsync_WithClosedAlert_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Cerrada" };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _alertService.BypassCriticalAsync(usuarioId, alerta.Id));
    }

    [Fact]
    public async Task RetryAsync_SendsPendingAlert()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Estado = "Pendiente",
            Timeline = [["2024-01-01T00:00:00Z", "Impacto detectado: crash"]],
        };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        var result = await _alertService.RetryAsync(usuarioId, alerta.Id);

        Assert.Equal("Enviada", alerta.Estado);
        Assert.Equal(1, alerta.Reintentos);
        Assert.NotNull(alerta.EnviadaEn);
        Assert.Equal(2, alerta.Timeline.Count);
        _alertaRepo.Verify(r => r.UpdateAsync(alerta), Times.Once);
    }

    [Fact]
    public async Task RetryAsync_WithNonPendingAlert_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Enviada" };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _alertService.RetryAsync(usuarioId, alerta.Id));
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsAlert()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Tipo = "SOS",
            Severidad = "crash",
            Estado = "Enviada",
        };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        var result = await _alertService.GetStatusAsync(usuarioId, alerta.Id);

        Assert.Equal(alerta.Id, result.Id);
        Assert.Equal("SOS", result.Tipo);
        Assert.Equal("crash", result.Severidad);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task GetStatusAsync_WithWrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = Guid.NewGuid() };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _alertService.GetStatusAsync(usuarioId, alerta.Id));
    }

    [Fact]
    public async Task GetStatusAsync_NotFound_Throws()
    {
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync((Alerta?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _alertService.GetStatusAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task CloseAsync_WithAtendido_CreatesIncident()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Severidad = "crash",
            Estado = "Activa",
            Lat = 19.43,
            Lng = -99.13,
        };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        var result = await _alertService.CloseAsync(usuarioId, alerta.Id, new CloseAlertRequest
        {
            MetodoCierre = "Atendido",
            Nota = "Usuario atendido por paramédicos.",
        });

        Assert.Equal("Cerrada", alerta.Estado);
        Assert.Equal("Atendido", alerta.MetodoCierre);
        Assert.Equal("Usuario atendido por paramédicos.", alerta.Nota);
        _alertaRepo.Verify(r => r.UpdateAsync(alerta), Times.Once);
        _incidenteRepo.Verify(r => r.AddAsync(It.IsAny<Incidente>()), Times.Once);
    }

    [Fact]
    public async Task CloseAsync_WithFalsaAlarma_MarksAsFalse()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Activa" };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        var result = await _alertService.CloseAsync(usuarioId, alerta.Id, new CloseAlertRequest
        {
            MetodoCierre = "FalsaAlarma",
        });

        Assert.Equal("FalsaAlarma", alerta.Estado);
        Assert.True(alerta.EsFalsaAlarma);
        _incidenteRepo.Verify(r => r.AddAsync(It.IsAny<Incidente>()), Times.Once);
    }

    [Fact]
    public async Task CloseAsync_WithInvalidMetodo_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Activa" };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _alertService.CloseAsync(usuarioId, alerta.Id, new CloseAlertRequest
            {
                MetodoCierre = "Invalido",
            }));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task CloseAsync_WithWrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = Guid.NewGuid(), Estado = "Activa" };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _alertService.CloseAsync(usuarioId, alerta.Id, new CloseAlertRequest
            {
                MetodoCierre = "Atendido",
            }));
    }

    [Fact]
    public async Task SyncOfflineAsync_CreatesAlertsAsEnviada()
    {
        var usuarioId = Guid.NewGuid();

        var result = await _alertService.SyncOfflineAsync(usuarioId, new SyncOfflineRequest
        {
            Alertas =
            [
                new OfflineAlertDto
                {
                    Lat = 19.43,
                    Lng = -99.13,
                    Severidad = "crash",
                    Tipo = "SOS",
                    GForce = "5.5",
                    CreadoEn = DateTime.UtcNow.AddHours(-1),
                },
                new OfflineAlertDto
                {
                    Lat = 19.44,
                    Lng = -99.14,
                    Severidad = "bump",
                    Tipo = "Impacto",
                    CreadoEn = DateTime.UtcNow.AddMinutes(-30),
                },
            ],
        });

        Assert.Equal(2, result.Count);
        Assert.All(result, r => Assert.Equal("Enviada", r.Estado));
        Assert.All(result, r => Assert.True(r.EsOffline));
        _alertaRepo.Verify(r => r.AddAsync(It.IsAny<Alerta>()), Times.Exactly(2));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task DetectAsync_EstadoPendiente_DoesNotInvokeNotificationService()
    {
        var usuarioId = Guid.NewGuid();
        await _alertService.DetectAsync(usuarioId, new DetectAlertRequest
        {
            Lat = 19.43,
            Lng = -99.13,
            Severidad = "bump",
        });
        _notificationService.Verify(n => n.NotifyAlertMonitorsAsync(It.IsAny<Alerta>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task SendSosAsync_EstadoEnviada_InvokesNotificationService()
    {
        var usuarioId = Guid.NewGuid();
        await _alertService.SendSosAsync(usuarioId, new SosRequest
        {
            Lat = 19.43,
            Lng = -99.13,
            Severidad = "severe",
        });
        _notificationService.Verify(n => n.NotifyAlertMonitorsAsync(It.IsAny<Alerta>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConfirmOkAsync_DoesNotInvokeNotificationService()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Pendiente" };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        await _alertService.ConfirmOkAsync(usuarioId, alerta.Id);

        _notificationService.Verify(n => n.NotifyAlertMonitorsAsync(It.IsAny<Alerta>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificationService.Verify(n => n.RetryAlertNotificationsAsync(It.IsAny<Alerta>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CloseAsync_DoesNotInvokeNotificationService()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Severidad = "crash", Estado = "Activa" };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        await _alertService.CloseAsync(usuarioId, alerta.Id, new CloseAlertRequest { MetodoCierre = "Atendido" });

        _notificationService.Verify(n => n.NotifyAlertMonitorsAsync(It.IsAny<Alerta>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BypassCriticalAsync_NotifiesImmediately()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Pendiente" };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        await _alertService.BypassCriticalAsync(usuarioId, alerta.Id);

        Assert.Equal("Enviada", alerta.Estado);
        _notificationService.Verify(n => n.NotifyAlertMonitorsAsync(alerta, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RetryAsync_InvokesRetryNotifications()
    {
        var usuarioId = Guid.NewGuid();
        var alerta = new Alerta
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Estado = "Pendiente",
            Timeline = [["2024-01-01T00:00:00Z", "Impacto detectado: crash"]],
        };
        _alertaRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), alerta.Id)).ReturnsAsync(alerta);

        await _alertService.RetryAsync(usuarioId, alerta.Id);

        _notificationService.Verify(n => n.RetryAlertNotificationsAsync(It.IsAny<Alerta>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotificationServiceFailure_DoesNotDeleteAlert()
    {
        var usuarioId = Guid.NewGuid();
        _notificationService.Setup(n => n.NotifyAlertMonitorsAsync(It.IsAny<Alerta>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Fallo inesperado"));

        var result = await _alertService.SendSosAsync(usuarioId, new SosRequest
        {
            Lat = 19.43,
            Lng = -99.13,
            Severidad = "severe",
        });

        Assert.NotNull(result);
        Assert.Equal("SOS", result.Tipo);
        _alertaRepo.Verify(r => r.AddAsync(It.IsAny<Alerta>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task SendSosAsync_GuardaAlertaAntesDeNotificar()
    {
        var usuarioId = Guid.NewGuid();
        Alerta? savedAlert = null;
        _alertaRepo.Setup(r => r.AddAsync(It.IsAny<Alerta>()))
            .Callback<Alerta>(a => savedAlert = a)
            .Returns(Task.CompletedTask);

        await _alertService.SendSosAsync(usuarioId, new SosRequest
        {
            Lat = 19.43,
            Lng = -99.13,
            Severidad = "severe",
        });

        Assert.NotNull(savedAlert);
        _notificationService.Verify(n => n.NotifyAlertMonitorsAsync(It.Is<Alerta>(a => a.Id == savedAlert!.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_OperationCanceled_DoesNotDeleteAlert()
    {
        var usuarioId = Guid.NewGuid();
        _notificationService.Setup(n => n.NotifyAlertMonitorsAsync(It.IsAny<Alerta>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var result = await _alertService.SendSosAsync(usuarioId, new SosRequest
        {
            Lat = 19.43,
            Lng = -99.13,
            Severidad = "severe",
        });

        Assert.NotNull(result);
        Assert.Equal("SOS", result.Tipo);
        _alertaRepo.Verify(r => r.AddAsync(It.IsAny<Alerta>()), Times.Once);
    }
}
