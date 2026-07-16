using Moq;
using Prueba1.Core.Domain;
using Prueba1.Core.Interfaces.Repositories;
using Prueba1.Models.DTOs;
using Prueba1.Services;

namespace Prueba1.Tests.Unit;

public class PermissionServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepo;
    private readonly PermissionService _permissionService;

    public PermissionServiceTests()
    {
        _usuarioRepo = new Mock<IUsuarioRepository>();
        _permissionService = new PermissionService(_usuarioRepo.Object);
    }

    [Fact]
    public async Task GetPermissionsAsync_WithExistingUser_ReturnsPermissions()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Permisos = new PermisosApp
            {
                Mobile = new PermisosPlataforma
                {
                    Ubicacion = true,
                    Notificaciones = true,
                    Bluetooth = true,
                },
                Web = new PermisosPlataforma
                {
                    Ubicacion = false,
                    Notificaciones = true,
                },
            },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _permissionService.GetPermissionsAsync(usuarioId);

        Assert.NotNull(result.Mobile);
        Assert.True(result.Mobile!.Ubicacion);
        Assert.True(result.Mobile.Notificaciones);
        Assert.True(result.Mobile.Bluetooth);

        Assert.NotNull(result.Web);
        Assert.False(result.Web!.Ubicacion);
        Assert.True(result.Web.Notificaciones);
    }

    [Fact]
    public async Task GetPermissionsAsync_WithoutPermissions_ReturnsEmpty()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _permissionService.GetPermissionsAsync(usuarioId);

        Assert.NotNull(result);
        Assert.Null(result.Mobile);
        Assert.Null(result.Web);
    }

    [Fact]
    public async Task GetPermissionsAsync_WithNonExistentUser_Throws()
    {
        _usuarioRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Usuario?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _permissionService.GetPermissionsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateMobilePermissionsAsync_UpdatesAndReturns()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _permissionService.UpdateMobilePermissionsAsync(usuarioId, new UpdatePermissionsRequest
        {
            Ubicacion = true,
            Notificaciones = true,
            Camara = false,
            Bluetooth = true,
        });

        Assert.True(result.Ubicacion);
        Assert.True(result.Notificaciones);
        Assert.True(result.Bluetooth);
        Assert.False(result.Camara);

        Assert.NotNull(usuario.Permisos);
        Assert.NotNull(usuario.Permisos.Mobile);
        Assert.True(usuario.Permisos.Mobile.Ubicacion);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task UpdateWebPermissionsAsync_UpdatesAndReturns()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _permissionService.UpdateWebPermissionsAsync(usuarioId, new UpdatePermissionsRequest
        {
            Ubicacion = false,
            Notificaciones = true,
            Microfono = true,
            Sensores = true,
        });

        Assert.False(result.Ubicacion);
        Assert.True(result.Notificaciones);
        Assert.True(result.Microfono);
        Assert.True(result.Sensores);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task UpdateMobilePermissionsAsync_WithExistingPermissions_Overwrites()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Permisos = new PermisosApp
            {
                Mobile = new PermisosPlataforma
                {
                    Ubicacion = true,
                    Notificaciones = true,
                },
            },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _permissionService.UpdateMobilePermissionsAsync(usuarioId, new UpdatePermissionsRequest
        {
            Ubicacion = false,
        });

        Assert.False(result.Ubicacion);
        Assert.False(usuario.Permisos.Mobile!.Ubicacion);
        Assert.False(usuario.Permisos.Mobile.Notificaciones);
    }
}
