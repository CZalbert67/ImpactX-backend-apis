using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ImpactX.Tests.Unit;

public class NotificationServiceTests
{
    private readonly Mock<INotificacionRepository> _notificacionRepo;
    private readonly NotificationService _notificationService;

    public NotificationServiceTests()
    {
        _notificacionRepo = new Mock<INotificacionRepository>();
        var logger = Mock.Of<ILogger<NotificationService>>();
        _notificationService = new NotificationService(_notificacionRepo.Object, logger);
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
        _notificacionRepo.Setup(r => r.GetByIdAsync(notificacion.Id)).ReturnsAsync(notificacion);

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
        _notificacionRepo.Setup(r => r.GetByIdAsync(notificacion.Id)).ReturnsAsync(notificacion);

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
        _notificacionRepo.Setup(r => r.GetByIdAsync(notificacion.Id)).ReturnsAsync(notificacion);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _notificationService.ToggleReadAsync(usuarioId, notificacion.Id, new ToggleReadRequest { Leida = true }));
    }

    [Fact]
    public async Task ToggleReadAsync_NotFound_Throws()
    {
        _notificacionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Notificacion?)null);

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
        _notificacionRepo.Setup(r => r.GetByIdAsync(notificacion.Id)).ReturnsAsync(notificacion);

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
        _notificacionRepo.Setup(r => r.GetByIdAsync(notificacion.Id)).ReturnsAsync(notificacion);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _notificationService.DeleteAsync(usuarioId, notificacion.Id));
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Throws()
    {
        _notificacionRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Notificacion?)null);

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
        _notificacionRepo.Setup(r => r.GetByIdAsync(noti.Id)).ReturnsAsync(noti);

        await _notificationService.ToggleReadAsync(usuarioId, noti.Id, new ToggleReadRequest { Leida = true });

        Assert.True(noti.Leida);
        Assert.NotNull(noti.LeidaEn);
    }
}
