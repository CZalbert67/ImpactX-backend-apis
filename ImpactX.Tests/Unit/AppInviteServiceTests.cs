using ImpactX.Core.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Tests.Unit;

public class AppInviteServiceTests
{
    private readonly Mock<IAppInviteRepository> _appInviteRepo;
    private readonly AppInviteService _appInviteService;

    public AppInviteServiceTests()
    {
        _appInviteRepo = new Mock<IAppInviteRepository>();
        var logger = Mock.Of<ILogger<AppInviteService>>();
        _appInviteService = new AppInviteService(_appInviteRepo.Object, logger);
    }

    [Fact]
    public async Task CreateAsync_GeneratesTokenAndUrl()
    {
        var usuarioId = Guid.NewGuid();

        var result = await _appInviteService.CreateAsync(usuarioId, new CreateAppInviteRequest
        {
            SuggestedUsername = "amigo",
            Relation = "Hermano",
            PersonalMessage = "Únete a ImpactX",
        });

        Assert.StartsWith("INV-", result.Token);
        Assert.True(result.Token.Length >= 8);
        Assert.Contains(result.Token, result.InviteUrl);
        Assert.Equal("Pendiente de registro", result.Status);
        Assert.Equal(usuarioId, result.UsuarioId);
        _appInviteRepo.Verify(r => r.AddAsync(It.IsAny<AppInvite>()), Times.Once);
    }

    [Fact]
    public async Task GetInvitesAsync_MarksExpired()
    {
        var usuarioId = Guid.NewGuid();
        var expired = new AppInvite
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Token = "INV-ABC-1234",
            Status = "Pendiente de registro",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        };
        var pending = new AppInvite
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Token = "INV-DEF-5678",
            Status = "Pendiente de registro",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        };

        _appInviteRepo.Setup(r => r.GetByUserAsync(usuarioId)).ReturnsAsync([expired, pending]);

        var result = await _appInviteService.GetInvitesAsync(usuarioId);

        Assert.Equal(2, result.Count);
        Assert.Equal("Expirada", result[0].Status);
        Assert.Equal("Pendiente de registro", result[1].Status);
        _appInviteRepo.Verify(r => r.UpdateAsync(expired), Times.Once);
    }

    [Fact]
    public async Task GetByTokenAsync_WithValidToken_ReturnsDto()
    {
        var invite = new AppInvite
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            Token = "INV-ABC-1234",
            Status = "Pendiente de registro",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        };

        _appInviteRepo.Setup(r => r.GetByTokenAsync(invite.Token)).ReturnsAsync(invite);

        var result = await _appInviteService.GetByTokenAsync(invite.Token);

        Assert.Equal(invite.Token, result.Token);
        Assert.False(result.Expirada);
    }

    [Fact]
    public async Task GetByTokenAsync_WithUnknownToken_Throws()
    {
        _appInviteRepo.Setup(r => r.GetByTokenAsync(It.IsAny<string>())).ReturnsAsync((AppInvite?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _appInviteService.GetByTokenAsync("INV-XXX-0000"));
    }

    [Fact]
    public async Task GetByTokenAsync_WithExpiredToken_Throws()
    {
        var invite = new AppInvite
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            Token = "INV-ABC-1234",
            Status = "Pendiente de registro",
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
        };

        _appInviteRepo.Setup(r => r.GetByTokenAsync(invite.Token)).ReturnsAsync(invite);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _appInviteService.GetByTokenAsync(invite.Token));
    }

    [Fact]
    public async Task AcceptAsync_MarksAccepted()
    {
        var usuarioId = Guid.NewGuid();
        var invite = new AppInvite
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            Token = "INV-ABC-1234",
            Status = "Pendiente de registro",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        };

        _appInviteRepo.Setup(r => r.GetByTokenAsync(invite.Token)).ReturnsAsync(invite);

        var result = await _appInviteService.AcceptAsync(usuarioId, new AcceptAppInviteRequest { Token = invite.Token });

        Assert.Equal("Aceptado", result.Status);
        Assert.Equal("Aceptado", invite.Status);
        _appInviteRepo.Verify(r => r.UpdateAsync(invite), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_WithAlreadyProcessedToken_Throws()
    {
        var invite = new AppInvite
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            Token = "INV-ABC-1234",
            Status = "Aceptado",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        };

        _appInviteRepo.Setup(r => r.GetByTokenAsync(invite.Token)).ReturnsAsync(invite);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _appInviteService.AcceptAsync(Guid.NewGuid(), new AcceptAppInviteRequest { Token = invite.Token }));
    }

    [Fact]
    public async Task CancelAsync_ByOwner_Cancels()
    {
        var usuarioId = Guid.NewGuid();
        var invite = new AppInvite
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Token = "INV-ABC-1234",
            Status = "Pendiente de registro",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        };

        _appInviteRepo.Setup(r => r.GetByIdAsync(invite.Id)).ReturnsAsync(invite);

        var result = await _appInviteService.CancelAsync(usuarioId, invite.Id);

        Assert.Equal("Cancelado", result.Status);
        _appInviteRepo.Verify(r => r.UpdateAsync(invite), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_ByOtherUser_Throws()
    {
        var usuarioId = Guid.NewGuid();
        var invite = new AppInvite
        {
            Id = Guid.NewGuid(),
            UsuarioId = Guid.NewGuid(),
            Token = "INV-ABC-1234",
            Status = "Pendiente de registro",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        };

        _appInviteRepo.Setup(r => r.GetByIdAsync(invite.Id)).ReturnsAsync(invite);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _appInviteService.CancelAsync(usuarioId, invite.Id));
    }

    [Fact]
    public async Task DeleteAsync_ByOwner_Deletes()
    {
        var usuarioId = Guid.NewGuid();
        var invite = new AppInvite
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            Token = "INV-ABC-1234",
            Status = "Pendiente de registro",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
        };

        _appInviteRepo.Setup(r => r.GetByIdAsync(invite.Id)).ReturnsAsync(invite);

        await _appInviteService.DeleteAsync(usuarioId, invite.Id);

        _appInviteRepo.Verify(r => r.DeleteAsync(invite), Times.Once);
    }
}
