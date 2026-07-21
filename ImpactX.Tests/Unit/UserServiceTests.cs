using ImpactX.Core.Exceptions;
using Moq;
using ImpactX.Core.Domain;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Tests.Unit;

public class UserServiceTests
{
    private readonly Mock<IUsuarioRepository> _usuarioRepo;
    private readonly UserService _userService;

    public UserServiceTests()
    {
        _usuarioRepo = new Mock<IUsuarioRepository>();
        _userService = new UserService(_usuarioRepo.Object);
    }

    [Fact]
    public async Task GetProfileAsync_WithExistingUser_ReturnsProfile()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Nombre = "Juan",
            Correo = "juan@test.com",
            Username = "juan123",
            PlanActivo = "Premium",
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _userService.GetProfileAsync(usuarioId);

        Assert.Equal("Juan", result.Nombre);
        Assert.Equal("juan@test.com", result.Correo);
        Assert.Equal("Premium", result.PlanActivo);
    }

    [Fact]
    public async Task GetProfileAsync_WithNonExistentUser_Throws()
    {
        _usuarioRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Usuario?)null);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _userService.GetProfileAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task UpdateProfileAsync_UpdatesFields()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, Nombre = "Old", Telefono = "555-0000" };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _userService.UpdateProfileAsync(usuarioId, new UpdateUserProfileRequest
        {
            Nombre = "New Name",
            Telefono = "555-9999",
        });

        Assert.Equal("New Name", result.Nombre);
        Assert.Equal("555-9999", result.Telefono);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_PartialUpdate_KeepsExistingValues()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId, Nombre = "Juan", Telefono = "555-0000" };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _userService.UpdateProfileAsync(usuarioId, new UpdateUserProfileRequest
        {
            Nombre = "Pedro",
        });

        Assert.Equal("Pedro", result.Nombre);
        Assert.Equal("555-0000", result.Telefono);
    }

    [Fact]
    public async Task GetPreferencesAsync_WithPreferences_ReturnsDto()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Preferencias = new PreferenciasUsuario
            {
                NotificacionesPush = true,
                NotificacionesEmail = false,
                Idioma = "es",
                UnidadVelocidad = "kmh",
            },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _userService.GetPreferencesAsync(usuarioId);

        Assert.True(result.NotificacionesPush);
        Assert.False(result.NotificacionesEmail);
        Assert.Equal("es", result.Idioma);
    }

    [Fact]
    public async Task GetPreferencesAsync_WithoutPreferences_ReturnsEmpty()
    {
        var usuarioId = Guid.NewGuid();
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(new Usuario { Id = usuarioId });

        var result = await _userService.GetPreferencesAsync(usuarioId);

        Assert.NotNull(result);
        Assert.False(result.NotificacionesPush);
    }

    [Fact]
    public async Task UpdatePreferencesAsync_UpdatesFields()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _userService.UpdatePreferencesAsync(usuarioId, new UpdateUserPreferencesRequest
        {
            NotificacionesPush = true,
            NotificacionesEmail = false,
            CompartirUbicacion = true,
            Idioma = "en",
            UnidadVelocidad = "mph",
        });

        Assert.True(result.NotificacionesPush);
        Assert.False(result.NotificacionesEmail);
        Assert.True(result.CompartirUbicacion);
        Assert.Equal("en", result.Idioma);
        Assert.Equal("mph", result.UnidadVelocidad);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task GetDriverProfileAsync_WithProfile_ReturnsDto()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            PerfilConduccion = new PerfilConduccion
            {
                TipoVehiculo = "Sedan",
                Marca = "Toyota",
                Modelo = "Corolla",
                Anio = 2020,
                Color = "Rojo",
                Placa = "ABC-123",
            },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _userService.GetDriverProfileAsync(usuarioId);

        Assert.Equal("Sedan", result.TipoVehiculo);
        Assert.Equal("Toyota", result.Marca);
        Assert.Equal("Corolla", result.Modelo);
        Assert.Equal(2020, result.Anio);
    }

    [Fact]
    public async Task GetDriverProfileAsync_WithoutProfile_ReturnsEmpty()
    {
        var usuarioId = Guid.NewGuid();
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(new Usuario { Id = usuarioId });

        var result = await _userService.GetDriverProfileAsync(usuarioId);

        Assert.NotNull(result);
        Assert.Null(result.TipoVehiculo);
    }

    [Fact]
    public async Task UpdateDriverProfileAsync_UpdatesFields()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _userService.UpdateDriverProfileAsync(usuarioId, new UpdateDriverProfileRequest
        {
            TipoVehiculo = "SUV",
            Marca = "Honda",
            Modelo = "CR-V",
            Anio = 2021,
            Color = "Negro",
            Placa = "XYZ-789",
            Uso = "Personal",
        });

        Assert.Equal("SUV", result.TipoVehiculo);
        Assert.Equal("Honda", result.Marca);
        Assert.Equal("Negro", result.Color);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task GetMedicalProfileAsync_WithProfile_ReturnsDto()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            FichaMedica = new FichaMedica
            {
                TipoSangre = "O+",
                Alergias = "Penicilina",
                Condiciones = "Asma",
                Medicamentos = "Salbutamol",
            },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _userService.GetMedicalProfileAsync(usuarioId);

        Assert.Equal("O+", result.TipoSangre);
        Assert.Equal("Penicilina", result.Alergias);
    }

    [Fact]
    public async Task GetMedicalProfileAsync_WithoutProfile_ReturnsEmpty()
    {
        var usuarioId = Guid.NewGuid();
        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(new Usuario { Id = usuarioId });

        var result = await _userService.GetMedicalProfileAsync(usuarioId);

        Assert.NotNull(result);
        Assert.Null(result.TipoSangre);
    }

    [Fact]
    public async Task UpdateMedicalProfileAsync_UpdatesFields()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario { Id = usuarioId };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _userService.UpdateMedicalProfileAsync(usuarioId, new UpdateMedicalProfileRequest
        {
            TipoSangre = "A-",
            Alergias = "Ninguna",
            Condiciones = "Ninguna",
            Nota = "Donante de órganos",
        });

        Assert.Equal("A-", result.TipoSangre);
        Assert.Equal("Ninguna", result.Alergias);
        Assert.Equal("Donante de órganos", result.Nota);
        _usuarioRepo.Verify(r => r.UpdateAsync(usuario), Times.Once);
    }

    [Fact]
    public async Task SearchUsersAsync_ReturnsMatchingUsers()
    {
        var usuarioId = Guid.NewGuid();
        _usuarioRepo.Setup(r => r.SearchAsync("juan"))
            .ReturnsAsync([
                new Usuario { Id = Guid.NewGuid(), Username = "juan1", Nombre = "Juan Perez", Correo = "juan@test.com", AppId = "APP001" },
                new Usuario { Id = Guid.NewGuid(), Username = "juan2", Nombre = "Juan Lopez", Correo = "juan2@test.com", AppId = "APP002" },
            ]);

        var result = await _userService.SearchUsersAsync("juan");

        Assert.Equal(2, result.Count);
        Assert.Equal("juan1", result[0].Username);
    }

    [Fact]
    public async Task SearchUsersAsync_ExcludesCurrentUser()
    {
        var usuarioId = Guid.NewGuid();
        _usuarioRepo.Setup(r => r.SearchAsync("test"))
            .ReturnsAsync([
                new Usuario { Id = usuarioId, Username = "test1" },
                new Usuario { Id = Guid.NewGuid(), Username = "test2" },
            ]);

        var result = await _userService.SearchUsersAsync("test", usuarioId);

        Assert.Single(result);
        Assert.Equal("test2", result[0].Username);
    }

    [Fact]
    public async Task SearchUsersAsync_WithNoResults_ReturnsEmpty()
    {
        _usuarioRepo.Setup(r => r.SearchAsync("zzzz")).ReturnsAsync([]);

        var result = await _userService.SearchUsersAsync("zzzz");

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetProfileAsync_WithAllSubEntities_ReturnsCompleteProfile()
    {
        var usuarioId = Guid.NewGuid();
        var usuario = new Usuario
        {
            Id = usuarioId,
            Nombre = "Completo",
            Correo = "completo@test.com",
            Telefono = "555-1234",
            PlanActivo = "Enterprise",
            EmailConfirmed = true,
            CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            LastLoginAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            PerfilConduccion = new PerfilConduccion { TipoVehiculo = "Camioneta" },
            FichaMedica = new FichaMedica { TipoSangre = "AB+" },
            Preferencias = new PreferenciasUsuario { Idioma = "es" },
            Permisos = new PermisosApp
            {
                Mobile = new PermisosPlataforma { Ubicacion = true },
                Web = new PermisosPlataforma { Notificaciones = true },
            },
            Settings = new SettingsUsuario { TwoFactorEnabled = true },
        };

        _usuarioRepo.Setup(r => r.GetByIdAsync(usuarioId)).ReturnsAsync(usuario);

        var result = await _userService.GetProfileAsync(usuarioId);

        Assert.Equal("Camioneta", result.PerfilConduccion?.TipoVehiculo);
        Assert.Equal("AB+", result.FichaMedica?.TipoSangre);
        Assert.Equal("es", result.Preferencias?.Idioma);
        Assert.True(result.Permisos?.Mobile?.Ubicacion);
        Assert.True(result.Permisos?.Web?.Notificaciones);
        Assert.True(result.Settings?.TwoFactorEnabled);
    }
}
