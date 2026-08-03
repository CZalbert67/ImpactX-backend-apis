using ImpactX.Core.Notifications;
using System.Collections.Concurrent;
using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ImpactX.Tests.Unit;

public class ListLogger<T> : ILogger<T>
{
    public ConcurrentBag<string> LogEntries { get; } = [];
    public ConcurrentBag<(LogLevel Level, string Message)> FormattedEntries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        LogEntries.Add(message);
        FormattedEntries.Add((logLevel, message));
    }
}

public class NotificationServiceTests
{
    private readonly Mock<INotificacionRepository> _notificacionRepo;
    private readonly Mock<IUsuarioRepository> _usuarioRepo;
    private readonly Mock<IDispositivoRepository> _dispositivoRepo;
    private readonly Mock<IMonitoringRelationshipRepository> _monitoringRepo;
    private readonly Mock<IPushNotificationGateway> _pushGateway;
    private readonly ListLogger<NotificationService> _logger;
    private readonly NotificationService _notificationService;

    public NotificationServiceTests()
    {
        _notificacionRepo = new Mock<INotificacionRepository>();
        _usuarioRepo = new Mock<IUsuarioRepository>();
        _dispositivoRepo = new Mock<IDispositivoRepository>();
        _monitoringRepo = new Mock<IMonitoringRelationshipRepository>();
        _pushGateway = new Mock<IPushNotificationGateway>();
        _logger = new ListLogger<NotificationService>();
        _notificationService = new NotificationService(
            _notificacionRepo.Object,
            _usuarioRepo.Object,
            _dispositivoRepo.Object,
            _monitoringRepo.Object,
            _pushGateway.Object,
            _logger);
    }

    [Fact]
    public async Task GetNotificationsAsync_ReturnsList()
    {
        var usuarioId = Guid.NewGuid();
        _notificacionRepo.Setup(r => r.GetByUserAsync(usuarioId))
            .ReturnsAsync([
                new Notificacion
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = usuarioId,
                    Titulo = "Alerta",
                    Mensaje = "Impacto detectado",
                    Tipo = "SOS",
                    CreadoEn = DateTime.UtcNow,
                },
                new Notificacion
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = usuarioId,
                    Titulo = "Recordatorio",
                    Mensaje = "Revisa tu plan",
                    Tipo = "Info",
                    Leida = true,
                },
            ]);

        var result = await _notificationService.GetNotificationsAsync(usuarioId);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, n => n.Titulo == "Alerta");
        Assert.Contains(result, n => n.Titulo == "Recordatorio");
        _notificacionRepo.Verify(r => r.GetByUserAsync(usuarioId), Times.Once);
    }

    [Fact]
    public async Task GetNotificationsAsync_EmptyList_ReturnsEmpty()
    {
        _notificacionRepo.Setup(r => r.GetByUserAsync(It.IsAny<Guid>())).ReturnsAsync([]);

        var result = await _notificationService.GetNotificationsAsync(Guid.NewGuid());

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ReturnsCount()
    {
        var usuarioId = Guid.NewGuid();
        _notificacionRepo.Setup(r => r.CountUnreadByUserAsync(usuarioId)).ReturnsAsync(3);

        var result = await _notificationService.GetUnreadCountAsync(usuarioId);

        Assert.Equal(3, result);
    }

    [Fact]
    public async Task ToggleReadAsync_MarksAsRead()
    {
        var usuarioId = Guid.NewGuid();
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Leida = false,
        };
        _notificacionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), notificacion.Id)).ReturnsAsync(notificacion);

        await _notificationService.ToggleReadAsync(usuarioId, notificacion.Id, new ToggleReadRequest
        {
            Leida = true,
        });

        Assert.True(notificacion.Leida);
        Assert.NotNull(notificacion.LeidaEn);
        _notificacionRepo.Verify(r => r.UpdateAsync(notificacion), Times.Once);
    }

    [Fact]
    public async Task ToggleReadAsync_MarksAsUnread()
    {
        var usuarioId = Guid.NewGuid();
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Leida = true,
            LeidaEn = DateTime.UtcNow,
        };
        _notificacionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), notificacion.Id)).ReturnsAsync(notificacion);

        await _notificationService.ToggleReadAsync(usuarioId, notificacion.Id, new ToggleReadRequest
        {
            Leida = false,
        });

        Assert.False(notificacion.Leida);
        Assert.Null(notificacion.LeidaEn);
    }

    [Fact]
    public async Task ToggleReadAsync_WithWrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
        };
        _notificacionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), notificacion.Id)).ReturnsAsync(notificacion);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _notificationService.ToggleReadAsync(usuarioId, notificacion.Id, new ToggleReadRequest { Leida = true }));
    }

    [Fact]
    public async Task ToggleReadAsync_NotFound_Throws()
    {
        _notificacionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync((Notificacion?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _notificationService.ToggleReadAsync(Guid.NewGuid(), Guid.NewGuid(), new ToggleReadRequest { Leida = true }));
    }

    [Fact]
    public async Task MarkAllAsReadAsync_CallsRepository()
    {
        var usuarioId = Guid.NewGuid();
        _notificacionRepo.Setup(r => r.CountUnreadByUserAsync(usuarioId)).ReturnsAsync(5);

        await _notificationService.MarkAllAsReadAsync(usuarioId);

        _notificacionRepo.Verify(r => r.MarkAllAsReadAsync(usuarioId), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_DeletesNotification()
    {
        var usuarioId = Guid.NewGuid();
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
        };
        _notificacionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), notificacion.Id)).ReturnsAsync(notificacion);

        await _notificationService.DeleteAsync(usuarioId, notificacion.Id);

        _notificacionRepo.Verify(r => r.DeleteAsync(notificacion), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WithWrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
        };
        _notificacionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), notificacion.Id)).ReturnsAsync(notificacion);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _notificationService.DeleteAsync(usuarioId, notificacion.Id));
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        _notificacionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync((Notificacion?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _notificationService.DeleteAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task DeleteAllAsync_DeletesAll()
    {
        var usuarioId = Guid.NewGuid();

        await _notificationService.DeleteAllAsync(usuarioId);

        _notificacionRepo.Verify(r => r.DeleteAllByUserAsync(usuarioId), Times.Once);
    }

    [Fact]
    public async Task GetNotificationsAsync_MapsAllFields()
    {
        var usuarioId = Guid.NewGuid();
        var notificacion = new Notificacion
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Titulo = "Test Title",
            Mensaje = "Test Message",
            Tipo = "Warning",
            ReferenciaId = "alert-123",
            ReferenciaTipo = "Alerta",
            Leida = true,
            LeidaEn = DateTime.UtcNow,
            CreadoEn = DateTime.UtcNow.AddHours(-1),
        };
        _notificacionRepo.Setup(r => r.GetByUserAsync(usuarioId)).ReturnsAsync([notificacion]);

        var result = await _notificationService.GetNotificationsAsync(usuarioId);
        var dto = result.Single();

        Assert.Equal(notificacion.Id, dto.Id);
        Assert.Equal("Test Title", dto.Titulo);
        Assert.Equal("Test Message", dto.Mensaje);
        Assert.Equal("Warning", dto.Tipo);
        Assert.Equal("alert-123", dto.ReferenciaId);
        Assert.Equal("Alerta", dto.ReferenciaTipo);
        Assert.True(dto.Leida);
        Assert.NotNull(dto.LeidaEn);
    }

    [Fact]
    public async Task ToggleReadAsync_VerifyLoggingSideEffects()
    {
        var usuarioId = Guid.NewGuid();
        var noti = new Notificacion { Id = Guid.NewGuid(), UsuarioId = usuarioId, Leida = false };
        _notificacionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), noti.Id)).ReturnsAsync(noti);

        await _notificationService.ToggleReadAsync(usuarioId, noti.Id, new ToggleReadRequest { Leida = true });

        Assert.True(noti.Leida);
        Assert.NotNull(noti.LeidaEn);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_GetsOnlyActiveMonitors()
    {
        var userId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Empty(result);
        _monitoringRepo.Verify(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>()), Times.Once);
        _pushGateway.Verify(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_ResolvesMonitorUser()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true, FcmToken = "valid-token" };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("Enviado", result[0].Status);
        Assert.True(result[0].Sent);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_MonitorWithoutUserLink_ReturnsDestinatarioNoVinculado()
    {
        var userId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, Guid.NewGuid());

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("DestinatarioNoVinculado", result[0].Status);
        Assert.False(result[0].Sent);
        _pushGateway.Verify(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_InactiveUser_DoesNotCallGateway()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = false };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("DestinatarioNoVinculado", result[0].Status);
        _pushGateway.Verify(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_UserWithoutFcmToken_ReturnsSinToken()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true, FcmToken = null };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("SinToken", result[0].Status);
        _pushGateway.Verify(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NotifyAlertMonitors_FirebaseNotConfigured_ReturnsFirebaseNoConfigurado()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true, FcmToken = "token" };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(false, "FirebaseNoConfigurado"));

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("FirebaseNoConfigurado", result[0].Status);
    }

    [Fact]
    public async Task NotifyAlertMonitors_GatewaySuccess_ReturnsEnviado()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "SOS", Severidad = "severe", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true, FcmToken = "valid-token" };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado", "msg-1"));

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("Enviado", result[0].Status);
        Assert.True(result[0].Sent);
        _notificacionRepo.Verify(r => r.AddAsync(It.IsAny<Notificacion>()), Times.Once);
        _notificacionRepo.Verify(r => r.UpdateAsync(It.IsAny<Notificacion>()), Times.Once);
    }

    [Fact]
    public async Task NotifyAlertMonitors_GatewayFailure_ReturnsFallido()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true, FcmToken = "token" };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(false, "Fallido"));

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("Fallido", result[0].Status);
        Assert.False(result[0].Sent);
    }

    [Fact]
    public async Task NotifyAlertMonitors_SosAlert_UsesSosTitle()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "SOS", Severidad = "severe", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true, FcmToken = "token" };
        string? capturedTitle = null;

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IReadOnlyDictionary<string, string>?, CancellationToken>((_, t, _, _, _) => capturedTitle = t)
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Equal("Alerta SOS ImpactX", capturedTitle);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_PersistsHistoryBeforeGateway()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true, FcmToken = "token" };
        var capturedEstado = string.Empty;

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));
        _notificacionRepo.Setup(r => r.AddAsync(It.IsAny<Notificacion>()))
            .Callback<Notificacion>(n => capturedEstado = n.EstadoEnvio);

        await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Equal("Pendiente", capturedEstado);
    }

    [Fact]
    public async Task NotifyAlertMonitors_UpdatesHistoryAfterDispatch()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "SOS", Severidad = "severe", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true, FcmToken = "token" };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        await _notificationService.NotifyAlertMonitorsAsync(alerta);

        _notificacionRepo.Verify(r => r.UpdateAsync(It.Is<Notificacion>(n => n.EstadoEnvio == "Enviado" && n.EnviadoEn != null)), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_DoesNotDuplicateSentNotification()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var existing = new Notificacion { Id = Guid.NewGuid(), UsuarioId = monitorUserId, EstadoEnvio = "Enviado", ClaveIdempotencia = $"alert:{alerta.Id}:recipient:{monitorUserId}:channel:push" };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(existing.ClaveIdempotencia, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("DuplicadoOmitido", result[0].Status);
        _pushGateway.Verify(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
        _notificacionRepo.Verify(r => r.AddAsync(It.IsAny<Notificacion>()), Times.Never);
    }

    [Fact]
    public async Task RetryAlertNotifications_IncrementsIntentos()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true, FcmToken = "token" };
        var existing = new Notificacion { Id = Guid.NewGuid(), UsuarioId = monitorUserId, EstadoEnvio = "Fallido", Intentos = 1, ClaveIdempotencia = $"alert:{alerta.Id}:recipient:{monitorUserId}:channel:push" };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(existing.ClaveIdempotencia, It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        var result = await _notificationService.RetryAlertNotificationsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("Enviado", result[0].Status);
        Assert.Equal(2, existing.Intentos);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_OneMonitorFailure_DoesNotAffectOthers()
    {
        var userId = Guid.NewGuid();
        var monitorUser1 = Guid.NewGuid();
        var monitorUser2 = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor1 = AcceptedRelationship(userId, monitorUser1);
        var monitor2 = AcceptedRelationship(userId, monitorUser2);
        var usuario1 = new Usuario { Id = monitorUser1, IsActive = true, FcmToken = "token1" };
        var usuario2 = new Usuario { Id = monitorUser2, IsActive = true, FcmToken = null };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor1, monitor2]);
        _usuarioRepo.Setup(u => u.GetByIdAsync(monitorUser1)).ReturnsAsync(usuario1);
        _usuarioRepo.Setup(u => u.GetByIdAsync(monitorUser2)).ReturnsAsync(usuario2);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Equal(2, result.Count);
        var r1 = result.Single(r => r.RecipientUserId == monitorUser1);
        var r2 = result.Single(r => r.RecipientUserId == monitorUser2);
        Assert.Equal("Enviado", r1.Status);
        Assert.Equal("SinToken", r2.Status);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_PayloadContainsOnlyAllowedFields()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "SOS", Severidad = "severe", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true, FcmToken = "token" };
        IReadOnlyDictionary<string, string>? capturedData = null;

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IReadOnlyDictionary<string, string>?, CancellationToken>((_, _, _, d, _) => capturedData = d)
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.NotNull(capturedData);
        Assert.Contains("alertId", capturedData!.Keys);
        Assert.Contains("alertType", capturedData.Keys);
        Assert.Contains("severity", capturedData.Keys);
        Assert.Contains("createdAt", capturedData.Keys);
        Assert.DoesNotContain("fcmToken", capturedData.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("heartRate", capturedData.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("lat", capturedData.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("lng", capturedData.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("email", capturedData.Keys, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("phone", capturedData.Keys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task DispatchToMonitor_DoesNotLogFcmTokenValue()
    {
        var fakeToken = "fcm-" + Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true, FcmToken = fakeToken };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        await _notificationService.NotifyAlertMonitorsAsync(alerta);

        foreach (var entry in _logger.LogEntries)
        {
            Assert.DoesNotContain(fakeToken, entry, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_DoesNotPersistHistoryForUnlinkedMonitor()
    {
        var userId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, Guid.NewGuid());

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("DestinatarioNoVinculado", result[0].Status);
        _notificacionRepo.Verify(r => r.AddAsync(It.IsAny<Notificacion>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_SendsToAllActiveDevices()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "SOS", Severidad = "severe", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true };
        var dispositivos = new List<Dispositivo>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = monitorUserId, DeviceId = "phone-1", TokenFcm = "token-device-1", Activo = true },
            new() { Id = Guid.NewGuid(), UsuarioId = monitorUserId, DeviceId = "phone-2", TokenFcm = "token-device-2", Activo = true },
        };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _dispositivoRepo.Setup(r => r.GetActiveByUsuarioIdAsync(monitorUserId)).ReturnsAsync(dispositivos);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("Enviado", result[0].Status);
        Assert.True(result[0].Sent);
        _pushGateway.Verify(g => g.SendAsync("token-device-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _pushGateway.Verify(g => g.SendAsync("token-device-2", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_OneDeviceFailure_DoesNotBlockOthers()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true };
        var dispositivos = new List<Dispositivo>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = monitorUserId, DeviceId = "phone-1", TokenFcm = "bad-token-1", Activo = true },
            new() { Id = Guid.NewGuid(), UsuarioId = monitorUserId, DeviceId = "phone-2", TokenFcm = "good-token-2", Activo = true },
        };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _dispositivoRepo.Setup(r => r.GetActiveByUsuarioIdAsync(monitorUserId)).ReturnsAsync(dispositivos);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync("bad-token-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(false, "Fallido"));
        _pushGateway.Setup(g => g.SendAsync("good-token-2", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("Enviado", result[0].Status);
        Assert.True(result[0].Sent);
        _pushGateway.Verify(g => g.SendAsync("bad-token-1", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _pushGateway.Verify(g => g.SendAsync("good-token-2", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
        _notificacionRepo.Verify(r => r.UpdateAsync(It.Is<Notificacion>(n => n.EstadoEnvio == "Enviado")), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task SendPushNotificationAsync_NoActiveDevices_FallsBackToLegacyToken()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, IsActive = true, FcmToken = "legacy-token" };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        await _notificationService.SendPushNotificationAsync(usuarioId, "Titulo", "Mensaje");

        _pushGateway.Verify(g => g.SendAsync("legacy-token", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task DispatchToMonitor_DoesNotLogDeviceTokenValues()
    {
        var fakeToken = "fcm-dev-" + Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true };
        var dispositivos = new List<Dispositivo>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = monitorUserId, DeviceId = "watch", TokenFcm = fakeToken, Activo = true },
        };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _dispositivoRepo.Setup(r => r.GetActiveByUsuarioIdAsync(monitorUserId)).ReturnsAsync(dispositivos);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        await _notificationService.NotifyAlertMonitorsAsync(alerta);

        foreach (var entry in _logger.LogEntries)
        {
            Assert.DoesNotContain(fakeToken, entry, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_AllDevicesFail_ReturnsFallido()
    {
        var userId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alerta = new Alerta { Id = Guid.NewGuid(), UsuarioId = userId, Tipo = "Impacto", Severidad = "crash", Estado = "Enviada", CreadoEn = DateTime.UtcNow };
        var monitor = AcceptedRelationship(userId, monitorUserId);
        var usuario = new Usuario { Id = monitorUserId, IsActive = true };
        var dispositivos = new List<Dispositivo>
        {
            new() { Id = Guid.NewGuid(), UsuarioId = monitorUserId, DeviceId = "phone-1", TokenFcm = "bad-token-1", Activo = true },
            new() { Id = Guid.NewGuid(), UsuarioId = monitorUserId, DeviceId = "phone-2", TokenFcm = "bad-token-2", Activo = true },
        };

        _monitoringRepo.Setup(r => r.GetAcceptedForMonitoredUserAsync(userId, It.IsAny<CancellationToken>())).ReturnsAsync([monitor]);
        _usuarioRepo.Setup(r => r.GetByIdAsync(monitorUserId)).ReturnsAsync(usuario);
        _dispositivoRepo.Setup(r => r.GetActiveByUsuarioIdAsync(monitorUserId)).ReturnsAsync(dispositivos);
        _notificacionRepo.Setup(r => r.GetByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).ReturnsAsync((Notificacion?)null);
        _pushGateway.Setup(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(false, "Fallido"));

        var result = await _notificationService.NotifyAlertMonitorsAsync(alerta);

        Assert.Single(result);
        Assert.Equal("Fallido", result[0].Status);
        Assert.False(result[0].Sent);
        _pushGateway.Verify(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _notificacionRepo.Verify(r => r.UpdateAsync(It.Is<Notificacion>(n => n.EstadoEnvio == "Fallido")), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task SendPushNotificationAsync_NoDevicesAndNoLegacyToken_DoesNotSend()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, IsActive = true, FcmToken = null };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);
        _dispositivoRepo.Setup(r => r.GetActiveByUsuarioIdAsync(usuarioId)).ReturnsAsync([]);

        await _notificationService.SendPushNotificationAsync(usuarioId, "Titulo", "Mensaje");

        _pushGateway.Verify(g => g.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_RelationshipWithoutAlertPermission_IsSkipped()
    {
        var monitoredUserId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alert = new Alerta
        {
            Id = Guid.NewGuid(),
            UsuarioId = monitoredUserId,
            Tipo = "Impacto",
            Severidad = "crash",
            Estado = "Enviada",
            CreadoEn = DateTime.UtcNow
        };
        var relationship = AcceptedRelationship(
            monitoredUserId,
            monitorUserId,
            receiveCriticalAlerts: false);

        _monitoringRepo
            .Setup(repository => repository.GetAcceptedForMonitoredUserAsync(
                monitoredUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([relationship]);

        var result = await _notificationService.NotifyAlertMonitorsAsync(alert);

        Assert.Empty(result);
        _usuarioRepo.Verify(
            repository => repository.GetByIdAsync(It.IsAny<Guid>()),
            Times.Never);
        _pushGateway.Verify(
            gateway => gateway.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_LatestRelationshipRevokesNotificationPermission_IsSkipped()
    {
        var monitoredUserId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alert = new Alerta
        {
            Id = Guid.NewGuid(),
            UsuarioId = monitoredUserId,
            Tipo = "Impacto",
            Severidad = "crash",
            Estado = "Enviada",
            CreadoEn = DateTime.UtcNow
        };
        var olderAllowed = AcceptedRelationship(monitoredUserId, monitorUserId);
        olderAllowed.UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        var latestDenied = AcceptedRelationship(
            monitoredUserId,
            monitorUserId,
            receiveNotifications: false);
        latestDenied.UpdatedAtUtc = DateTime.UtcNow;

        _monitoringRepo
            .Setup(repository => repository.GetAcceptedForMonitoredUserAsync(
                monitoredUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([olderAllowed, latestDenied]);

        var result = await _notificationService.NotifyAlertMonitorsAsync(alert);

        Assert.Empty(result);
        _pushGateway.Verify(
            gateway => gateway.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task NotifyAlertMonitors_DuplicateRelationshipsForSameMonitor_SendOnce()
    {
        var monitoredUserId = Guid.NewGuid();
        var monitorUserId = Guid.NewGuid();
        var alert = new Alerta
        {
            Id = Guid.NewGuid(),
            UsuarioId = monitoredUserId,
            Tipo = "SOS",
            Severidad = "critical",
            Estado = "Enviada",
            CreadoEn = DateTime.UtcNow
        };
        var older = AcceptedRelationship(monitoredUserId, monitorUserId);
        older.UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
        var latest = AcceptedRelationship(monitoredUserId, monitorUserId);
        latest.UpdatedAtUtc = DateTime.UtcNow;
        var user = new Usuario
        {
            Id = monitorUserId,
            IsActive = true,
            FcmToken = "monitor-token"
        };

        _monitoringRepo
            .Setup(repository => repository.GetAcceptedForMonitoredUserAsync(
                monitoredUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([older, latest]);
        _usuarioRepo.Setup(repository => repository.GetByIdAsync(monitorUserId))
            .ReturnsAsync(user);
        _notificacionRepo
            .Setup(repository => repository.GetByIdempotencyKeyAsync(
                It.IsAny<string>(),
                monitorUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notificacion?)null);
        _pushGateway
            .Setup(gateway => gateway.SendAsync(
                "monitor-token",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        var result = await _notificationService.NotifyAlertMonitorsAsync(alert);

        Assert.Single(result);
        _pushGateway.Verify(
            gateway => gateway.SendAsync(
                "monitor-token",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _notificacionRepo.Verify(
            repository => repository.AddAsync(
                It.Is<Notificacion>(notification =>
                    notification.PublicRelationshipId == latest.PublicRelationshipId)),
            Times.Once);
    }
    [Fact]
    public async Task NotifyAlertMonitors_LegacyGroupWithoutMaterializedPolicies_UsesSafeGroupDefaults()
    {
        var monitoredUserId = Guid.NewGuid();
        var memberUserId = Guid.NewGuid();
        var familyRepository = new Mock<IFamilySubscriptionRepository>();
        familyRepository
            .Setup(repository => repository.GetActiveByUserAsync(
                monitoredUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FamilySubscription
            {
                Id = Guid.NewGuid(),
                PublicSubscriptionId = "SUB-MIGRATION-TEST",
                OwnerUserId = monitoredUserId,
                PlanName = "Free",
                Status = FamilySubscriptionStatus.Active,
                CreatedAtUtc = DateTime.UtcNow.AddDays(-1),
                UpdatedAtUtc = DateTime.UtcNow,
                Memberships =
                [
                    new FamilyMembership
                    {
                        Id = Guid.NewGuid(),
                        PublicMembershipId = "MEM-MIGRATION-TEST",
                        UserId = memberUserId,
                        Role = FamilyMembershipRole.Member,
                        Status = FamilyMembershipStatus.Active,
                        AcceptedAtUtc = DateTime.UtcNow.AddHours(-1)
                    }
                ]
            });
        _monitoringRepo
            .Setup(repository => repository.GetAcceptedForMonitoredUserAsync(
                monitoredUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _usuarioRepo
            .Setup(repository => repository.GetByIdAsync(memberUserId))
            .ReturnsAsync(new Usuario
            {
                Id = memberUserId,
                IsActive = true,
                FcmToken = "group-member-token"
            });
        _dispositivoRepo
            .Setup(repository => repository.GetActiveByUsuarioIdAsync(memberUserId))
            .ReturnsAsync([]);
        _notificacionRepo
            .Setup(repository => repository.GetByIdempotencyKeyAsync(
                It.IsAny<string>(),
                memberUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notificacion?)null);
        _notificacionRepo
            .Setup(repository => repository.CountUnreadByUserAsync(memberUserId))
            .ReturnsAsync(1);
        _pushGateway
            .Setup(gateway => gateway.SendAsync(
                "group-member-token",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        var service = new NotificationService(
            _notificacionRepo.Object,
            _usuarioRepo.Object,
            _dispositivoRepo.Object,
            _monitoringRepo.Object,
            _pushGateway.Object,
            _logger,
            familyRepository.Object);

        var result = await service.NotifyAlertMonitorsAsync(new Alerta
        {
            Id = Guid.NewGuid(),
            UsuarioId = monitoredUserId,
            Tipo = "SOS",
            Severidad = "severe",
            Estado = "Enviada",
            CreadoEn = DateTime.UtcNow
        });

        var dispatch = Assert.Single(result);
        Assert.Equal(memberUserId, dispatch.RecipientUserId);
        Assert.Equal("Enviado", dispatch.Status);
        _notificacionRepo.Verify(repository => repository.AddAsync(
            It.Is<Notificacion>(notification =>
                notification.UsuarioId == memberUserId
                && notification.Evento == "WearableSosTriggered"
                && notification.PublicRelationshipId != null
                && notification.PublicRelationshipId.StartsWith("GRP-", StringComparison.Ordinal))),
            Times.Once);
    }

    [Fact]
    public async Task CreateAndDispatchAsync_PersistsAndPushesRealtimeNavigationData()
    {
        var recipientUserId = Guid.NewGuid();
        var relationshipId = $"MON-TEST-{Guid.NewGuid():N}";
        var entityId = $"MSG-{Guid.NewGuid():N}";
        IReadOnlyDictionary<string, string>? capturedData = null;
        Notificacion? capturedNotification = null;

        _notificacionRepo
            .Setup(repository => repository.GetByIdempotencyKeyAsync(
                "message-event",
                recipientUserId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notificacion?)null);
        _notificacionRepo
            .Setup(repository => repository.AddAsync(It.IsAny<Notificacion>()))
            .Callback<Notificacion>(value => capturedNotification = value)
            .Returns(Task.CompletedTask);
        _notificacionRepo
            .Setup(repository => repository.CountUnreadByUserAsync(recipientUserId))
            .ReturnsAsync(4);
        _notificacionRepo
            .Setup(repository => repository.UpdateAsync(It.IsAny<Notificacion>()))
            .Returns(Task.CompletedTask);
        _usuarioRepo
            .Setup(repository => repository.GetByIdAsync(recipientUserId))
            .ReturnsAsync(new Usuario
            {
                Id = recipientUserId,
                IsActive = true,
                FcmToken = "recipient-token"
            });
        _dispositivoRepo
            .Setup(repository => repository.GetActiveByUsuarioIdAsync(recipientUserId))
            .ReturnsAsync([]);
        _pushGateway
            .Setup(gateway => gateway.SendAsync(
                "recipient-token",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>?>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IReadOnlyDictionary<string, string>?, CancellationToken>(
                (_, _, _, data, _) => capturedData = data)
            .ReturnsAsync(new PushGatewayResult(true, "Enviado"));

        var result = await _notificationService.CreateAndDispatchAsync(
            new AppNotificationCommand(
                recipientUserId,
                "Mensaje nuevo",
                "Tienes un mensaje rápido.",
                "Message",
                "QuickMessageReceived",
                relationshipId,
                entityId,
                "QuickMessage",
                "/app/messages?recipient=PUB-TEST",
                "message-event"));

        Assert.NotNull(capturedNotification);
        Assert.Equal("QuickMessageReceived", capturedNotification!.Evento);
        Assert.Equal(relationshipId, capturedNotification.PublicRelationshipId);
        Assert.Equal("Enviado", capturedNotification.EstadoEnvio);
        Assert.Equal(capturedNotification.Id, result.Id);
        Assert.NotNull(capturedData);
        Assert.Equal("4", capturedData!["unreadCount"]);
        Assert.Equal("Message", capturedData["type"]);
        Assert.Equal("QuickMessageReceived", capturedData["event"]);
        Assert.Equal(relationshipId, capturedData["publicRelationshipId"]);
        Assert.Equal(entityId, capturedData["entityId"]);
        Assert.Equal("/app/messages?recipient=PUB-TEST", capturedData["deepLink"]);
        Assert.Equal(
            ImpactX.Core.ApiContract.ApiContractDefinition.ContractVersion,
            capturedData["contractVersion"]);
    }

    private static MonitoringRelationship AcceptedRelationship(
        Guid monitoredUserId,
        Guid monitorUserId,
        bool receiveCriticalAlerts = true,
        bool receiveNotifications = true)
    {
        return new MonitoringRelationship
        {
            Id = Guid.NewGuid(),
            PublicRelationshipId = $"MON-TEST-{Guid.NewGuid():N}",
            MonitorUserId = monitorUserId,
            MonitoredUserId = monitoredUserId,
            Status = MonitoringRelationshipStatus.Accepted,
            AcceptedAtUtc = DateTime.UtcNow,
            Permissions = new MonitoringPermissions
            {
                ReceiveCriticalAlerts = receiveCriticalAlerts,
                ReceiveNotifications = receiveNotifications
            }
        };
    }

}
