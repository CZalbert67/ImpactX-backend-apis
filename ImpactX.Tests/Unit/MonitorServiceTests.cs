using ImpactX.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using Monitor = ImpactX.Core.Domain.Monitor;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Tests.Unit;

public class MonitorServiceTests
{
    private readonly Mock<IMonitorRepository> _monitorRepo;
    private readonly Mock<IUsuarioRepository> _usuarioRepo;
    private readonly Mock<IPlanService> _planService;
    private readonly MonitorService _monitorService;

    public MonitorServiceTests()
    {
        _monitorRepo = new Mock<IMonitorRepository>();
        _usuarioRepo = new Mock<IUsuarioRepository>();
        _planService = new Mock<IPlanService>();
        var logger = Mock.Of<ILogger<MonitorService>>();
        _monitorService = new MonitorService(_monitorRepo.Object, _usuarioRepo.Object, _planService.Object, logger);
    }

    [Fact]
    public async Task GetMonitorsAsync_ReturnsList()
    {
        var usuarioId = Guid.NewGuid();
        _monitorRepo.Setup(r => r.GetByUserAsync(usuarioId))
            .ReturnsAsync([new Monitor { Id = Guid.NewGuid(), UsuarioId = usuarioId, Username = "monitor1" }]);

        var result = await _monitorService.GetMonitorsAsync(usuarioId);

        Assert.Single(result);
        Assert.Equal("monitor1", result[0].Username);
    }

    [Fact]
    public async Task InviteAsync_WithValidUsername_ReturnsToken()
    {
        var usuarioId = Guid.NewGuid();
        var invitado = new Usuario { Id = Guid.NewGuid(), Username = "juan", Correo = "juan@test.com", AppId = "APP002" };

        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _monitorRepo.Setup(r => r.CountActiveByUserAsync(usuarioId)).ReturnsAsync(0);
        _monitorRepo.Setup(r => r.ExistsByUsernameAsync(usuarioId, "juan")).ReturnsAsync(false);
        _usuarioRepo.Setup(r => r.GetByUsernameAsync("juan")).ReturnsAsync(invitado);

        var result = await _monitorService.InviteAsync(usuarioId, new InviteMonitorRequest
        {
            Username = "juan",
        });

        Assert.NotNull(result.Token);
        Assert.Equal(12, result.Token.Length);
        _monitorRepo.Verify(r => r.AddAsync(It.IsAny<Monitor>()), Times.Once);
    }

    [Fact]
    public async Task InviteAsync_FreePlan_Throws()
    {
        var usuarioId = Guid.NewGuid();

        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Free" });
        _monitorRepo.Setup(r => r.CountActiveByUserAsync(usuarioId)).ReturnsAsync(0);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _monitorService.InviteAsync(usuarioId, new InviteMonitorRequest { Username = "test" }));
    }

    [Fact]
    public async Task InviteAsync_OverLimit_Throws()
    {
        var usuarioId = Guid.NewGuid();

        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Basic" });
        _monitorRepo.Setup(r => r.CountActiveByUserAsync(usuarioId)).ReturnsAsync(2);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _monitorService.InviteAsync(usuarioId, new InviteMonitorRequest { Username = "test" }));
    }

    [Fact]
    public async Task InviteAsync_WithoutUsername_Throws()
    {
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _monitorRepo.Setup(r => r.CountActiveByUserAsync(It.IsAny<Guid>())).ReturnsAsync(0);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _monitorService.InviteAsync(Guid.NewGuid(), new InviteMonitorRequest()));
    }

    [Fact]
    public async Task InviteAsync_SelfInvite_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var invitado = new Usuario { Id = usuarioId, Username = "yo" };

        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _monitorRepo.Setup(r => r.CountActiveByUserAsync(usuarioId)).ReturnsAsync(0);
        _monitorRepo.Setup(r => r.ExistsByUsernameAsync(usuarioId, "yo")).ReturnsAsync(false);
        _usuarioRepo.Setup(r => r.GetByUsernameAsync("yo")).ReturnsAsync(invitado);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _monitorService.InviteAsync(usuarioId, new InviteMonitorRequest { Username = "yo" }));
    }

    [Fact]
    public async Task InviteAsync_ExistingInvite_Throws()
    {
        var usuarioId = Guid.NewGuid();

        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _monitorRepo.Setup(r => r.CountActiveByUserAsync(usuarioId)).ReturnsAsync(0);
        _monitorRepo.Setup(r => r.ExistsByUsernameAsync(usuarioId, "juan")).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _monitorService.InviteAsync(usuarioId, new InviteMonitorRequest { Username = "juan" }));
    }

    [Fact]
    public async Task ResendInviteAsync_RegeneratesToken()
    {
        var usuarioId = Guid.NewGuid();
        var monitor = new Monitor { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Pendiente", TokenInvitacion = "OLD" };
        _monitorRepo.Setup(r => r.GetByIdAsync(monitor.Id)).ReturnsAsync(monitor);

        await _monitorService.ResendInviteAsync(usuarioId, monitor.Id);

        Assert.NotEqual("OLD", monitor.TokenInvitacion);
        _monitorRepo.Verify(r => r.UpdateAsync(monitor), Times.Once);
    }

    [Fact]
    public async Task ResendInviteAsync_NotPending_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var monitor = new Monitor { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Activo" };
        _monitorRepo.Setup(r => r.GetByIdAsync(monitor.Id)).ReturnsAsync(monitor);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _monitorService.ResendInviteAsync(usuarioId, monitor.Id));
    }

    [Fact]
    public async Task RevokeMonitorAsync_SetsRevoked()
    {
        var usuarioId = Guid.NewGuid();
        var monitor = new Monitor { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Activo" };
        _monitorRepo.Setup(r => r.GetByIdAsync(monitor.Id)).ReturnsAsync(monitor);

        await _monitorService.RevokeMonitorAsync(usuarioId, monitor.Id);

        Assert.Equal("Revocado", monitor.Estado);
        Assert.NotNull(monitor.RevocadoEn);
        _monitorRepo.Verify(r => r.UpdateAsync(monitor), Times.Once);
    }

    [Fact]
    public async Task RestoreMonitorAsync_RestoresRevoked()
    {
        var usuarioId = Guid.NewGuid();
        var monitor = new Monitor { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Revocado", RevocadoEn = DateTime.UtcNow };
        _monitorRepo.Setup(r => r.GetByIdAsync(monitor.Id)).ReturnsAsync(monitor);

        await _monitorService.RestoreMonitorAsync(usuarioId, monitor.Id);

        Assert.Equal("Activo", monitor.Estado);
        Assert.Null(monitor.RevocadoEn);
        _monitorRepo.Verify(r => r.UpdateAsync(monitor), Times.Once);
    }

    [Fact]
    public async Task RestoreMonitorAsync_NotRevoked_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var monitor = new Monitor { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Activo" };
        _monitorRepo.Setup(r => r.GetByIdAsync(monitor.Id)).ReturnsAsync(monitor);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _monitorService.RestoreMonitorAsync(usuarioId, monitor.Id));
    }

    [Fact]
    public async Task GetInvitationByTokenAsync_WithValidToken_ReturnsInfo()
    {
        var monitor = new Monitor
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            Username = "juan",
            Estado = "Pendiente",
            Expiracion = DateTime.UtcNow.AddDays(1),
        };
        _monitorRepo.Setup(r => r.GetByTokenAsync("VALID")).ReturnsAsync(monitor);

        var result = await _monitorService.GetInvitationByTokenAsync("VALID");

        Assert.Equal("juan", result.Username);
        Assert.Equal("Pendiente", result.Estado);
    }

    [Fact]
    public async Task GetInvitationByTokenAsync_InvalidToken_Throws()
    {
        _monitorRepo.Setup(r => r.GetByTokenAsync("INVALID")).ReturnsAsync((Monitor?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _monitorService.GetInvitationByTokenAsync("INVALID"));
    }

    [Fact]
    public async Task GetInvitationByTokenAsync_Expired_Throws()
    {
        var monitor = new Monitor
        {
            Estado = "Pendiente",
            Expiracion = DateTime.UtcNow.AddDays(-1),
        };
        _monitorRepo.Setup(r => r.GetByTokenAsync("EXPIRED")).ReturnsAsync(monitor);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _monitorService.GetInvitationByTokenAsync("EXPIRED"));
    }

    [Fact]
    public async Task AcceptInvitationAsync_SetsActive()
    {
        var monitorUsuarioId = Guid.NewGuid();
        var monitor = new Monitor
        {
            Id = Guid.NewGuid(),
            Estado = "Pendiente",
            ProfileId = monitorUsuarioId.ToString(),
            TokenInvitacion = "TOKEN",
            Expiracion = DateTime.UtcNow.AddDays(1),
        };
        _monitorRepo.Setup(r => r.GetByTokenAsync("TOKEN")).ReturnsAsync(monitor);

        await _monitorService.AcceptInvitationAsync("TOKEN", monitorUsuarioId);

        Assert.Equal("Activo", monitor.Estado);
        Assert.NotNull(monitor.ConfirmadoEn);
        Assert.Null(monitor.TokenInvitacion);
        _monitorRepo.Verify(r => r.UpdateAsync(monitor), Times.Once);
    }

    [Fact]
    public async Task AcceptInvitationAsync_WrongUser_Throws()
    {
        var monitor = new Monitor
        {
            Estado = "Pendiente",
            ProfileId = Guid.NewGuid().ToString(),
            TokenInvitacion = "TOKEN",
        };
        _monitorRepo.Setup(r => r.GetByTokenAsync("TOKEN")).ReturnsAsync(monitor);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _monitorService.AcceptInvitationAsync("TOKEN", Guid.NewGuid()));
    }

    [Fact]
    public async Task RejectInvitationAsync_SetsRejected()
    {
        var monitorUsuarioId = Guid.NewGuid();
        var monitor = new Monitor
        {
            Estado = "Pendiente",
            ProfileId = monitorUsuarioId.ToString(),
            TokenInvitacion = "TOKEN",
        };
        _monitorRepo.Setup(r => r.GetByTokenAsync("TOKEN")).ReturnsAsync(monitor);

        await _monitorService.RejectInvitationAsync("TOKEN", monitorUsuarioId);

        Assert.Equal("Rechazado", monitor.Estado);
        Assert.Null(monitor.TokenInvitacion);
        _monitorRepo.Verify(r => r.UpdateAsync(monitor), Times.Once);
    }

    [Fact]
    public async Task RevokeMonitorAsync_WrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        var monitor = new Monitor { Id = Guid.NewGuid(), UsuarioId = otroUsuarioId, Estado = "Activo" };
        _monitorRepo.Setup(r => r.GetByIdAsync(monitor.Id)).ReturnsAsync(monitor);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _monitorService.RevokeMonitorAsync(usuarioId, monitor.Id));
    }
}
