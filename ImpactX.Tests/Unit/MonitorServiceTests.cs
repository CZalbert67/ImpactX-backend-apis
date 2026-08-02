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
    private readonly ListLogger<MonitorService> _logger;
    private readonly MonitorService _monitorService;

    public MonitorServiceTests()
    {
        _monitorRepo = new Mock<IMonitorRepository>();
        _usuarioRepo = new Mock<IUsuarioRepository>();
        _planService = new Mock<IPlanService>();
        _logger = new ListLogger<MonitorService>();
        _monitorService = new MonitorService(_monitorRepo.Object, _usuarioRepo.Object, _planService.Object, _logger);
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
    [Trait("Category", "Security")]
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
    [Trait("Category", "Security")]
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
    [Trait("Category", "Security")]
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
    [Trait("Category", "Security")]
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
        _monitorRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), monitor.Id)).ReturnsAsync(monitor);

        await _monitorService.ResendInviteAsync(usuarioId, monitor.Id);

        Assert.NotEqual("OLD", monitor.TokenInvitacion);
        _monitorRepo.Verify(r => r.UpdateAsync(monitor), Times.Once);
    }

    [Fact]
    public async Task ResendInviteAsync_NotPending_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var monitor = new Monitor { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Activo" };
        _monitorRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), monitor.Id)).ReturnsAsync(monitor);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _monitorService.ResendInviteAsync(usuarioId, monitor.Id));
    }

    [Fact]
    public async Task RevokeMonitorAsync_SetsRevoked()
    {
        var usuarioId = Guid.NewGuid();
        var monitor = new Monitor { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Activo" };
        _monitorRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), monitor.Id)).ReturnsAsync(monitor);

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
        _monitorRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), monitor.Id)).ReturnsAsync(monitor);

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
        _monitorRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), monitor.Id)).ReturnsAsync(monitor);

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
    [Trait("Category", "Security")]
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
    [Trait("Category", "Security")]
    public async Task RevokeMonitorAsync_WrongUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var otroUsuarioId = Guid.NewGuid();
        var monitor = new Monitor { Id = Guid.NewGuid(), UsuarioId = otroUsuarioId, Estado = "Activo" };
        _monitorRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), monitor.Id)).ReturnsAsync(monitor);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _monitorService.RevokeMonitorAsync(usuarioId, monitor.Id));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task InviteAsync_PremiumAllowsSixMonitors()
    {
        var usuarioId = Guid.NewGuid();
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _monitorRepo.Setup(r => r.CountActiveByUserAsync(usuarioId)).ReturnsAsync(5);
        _monitorRepo.Setup(r => r.ExistsByUsernameAsync(usuarioId, "monitor6")).ReturnsAsync(false);
        _usuarioRepo.Setup(r => r.GetByUsernameAsync("monitor6"))
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), Username = "monitor6", Correo = "m6@test.com" });

        var result = await _monitorService.InviteAsync(usuarioId, new InviteMonitorRequest
        {
            Username = "monitor6",
        });

        Assert.NotNull(result.Token);
        _monitorRepo.Verify(r => r.AddAsync(It.IsAny<Monitor>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task InviteAsync_PremiumRejectsSeventhMonitor()
    {
        var usuarioId = Guid.NewGuid();
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _monitorRepo.Setup(r => r.CountActiveByUserAsync(usuarioId)).ReturnsAsync(6);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _monitorService.InviteAsync(usuarioId, new InviteMonitorRequest { Username = "monitor7" }));
        _monitorRepo.Verify(r => r.AddAsync(It.IsAny<Monitor>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task InviteAsync_BasicKeepsLimitOfTwo()
    {
        var usuarioId = Guid.NewGuid();
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Basic" });
        _monitorRepo.Setup(r => r.CountActiveByUserAsync(usuarioId)).ReturnsAsync(2);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _monitorService.InviteAsync(usuarioId, new InviteMonitorRequest { Username = "extra" }));
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task InviteAsync_InactiveMonitorsDoNotConsumeQuota()
    {
        var usuarioId = Guid.NewGuid();
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "Premium" });
        _monitorRepo.Setup(r => r.CountActiveByUserAsync(usuarioId)).ReturnsAsync(5);
        _monitorRepo.Setup(r => r.ExistsByUsernameAsync(usuarioId, "newmonitor")).ReturnsAsync(false);
        _usuarioRepo.Setup(r => r.GetByUsernameAsync("newmonitor"))
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), Username = "newmonitor", Correo = "nm@test.com" });

        var existingInactive = new Monitor { Id = Guid.NewGuid(), UsuarioId = usuarioId, Estado = "Revocado" };
        var result = await _monitorService.InviteAsync(usuarioId, new InviteMonitorRequest
        {
            Username = "newmonitor",
        });

        Assert.NotNull(result.Token);
        _monitorRepo.Verify(r => r.AddAsync(It.IsAny<Monitor>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task InviteAsync_PlanNameCaseInsensitive_IsNotCaseSensitive()
    {
        var usuarioId = Guid.NewGuid();
        _planService.Setup(s => s.GetCurrentSubscriptionAsync(usuarioId))
            .ReturnsAsync(new SuscripcionDto { PlanNombre = "premium" });
        _monitorRepo.Setup(r => r.CountActiveByUserAsync(usuarioId)).ReturnsAsync(5);
        _monitorRepo.Setup(r => r.ExistsByUsernameAsync(usuarioId, "test")).ReturnsAsync(false);
        _usuarioRepo.Setup(r => r.GetByUsernameAsync("test"))
            .ReturnsAsync(new Usuario { Id = Guid.NewGuid(), Username = "test", Correo = "t@test.com" });

        var result = await _monitorService.InviteAsync(usuarioId, new InviteMonitorRequest
        {
            Username = "test",
        });

        Assert.NotNull(result.Token);
        _monitorRepo.Verify(r => r.AddAsync(It.IsAny<Monitor>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "Security")]
    public async Task TEST_ONLY_INVITATION_TOKEN_DO_NOT_LOG()
    {
        var fakeToken = "TEST-" + Guid.NewGuid().ToString("N")[..8];
        var usuarioId = Guid.NewGuid();
        var monitorUsuarioId = Guid.NewGuid();
        var monitorId = Guid.NewGuid();

        var monitor = new Monitor
        {
            Id = monitorId,
            UsuarioId = usuarioId,
            ProfileId = monitorUsuarioId.ToString(),
            Estado = "Pendiente",
            TokenInvitacion = fakeToken,
            Expiracion = DateTime.UtcNow.AddDays(1),
        };

        _monitorRepo.Setup(r => r.GetByTokenAsync(fakeToken)).ReturnsAsync(monitor);

        await _monitorService.AcceptInvitationAsync(fakeToken, monitorUsuarioId);

        foreach (var entry in _logger.LogEntries)
        {
            Assert.DoesNotContain(fakeToken, entry, StringComparison.OrdinalIgnoreCase);
        }

        _logger.LogEntries.Clear();

        var monitor2 = new Monitor
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            ProfileId = monitorUsuarioId.ToString(),
            Estado = "Pendiente",
            TokenInvitacion = fakeToken,
            Expiracion = DateTime.UtcNow.AddDays(1),
        };

        _monitorRepo.Setup(r => r.GetByTokenAsync(fakeToken)).ReturnsAsync(monitor2);

        await _monitorService.RejectInvitationAsync(fakeToken, monitorUsuarioId);

        foreach (var entry in _logger.LogEntries)
        {
            Assert.DoesNotContain(fakeToken, entry, StringComparison.OrdinalIgnoreCase);
        }
    }
}
