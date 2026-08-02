using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Models.DTOs.Vehicles;
using ImpactX.Services;
using Moq;

namespace ImpactX.Tests.Unit;

public class VehicleServiceTests
{
    private readonly Mock<IVehicleRepository> _repository = new();
    private readonly Mock<IVehicleQuotaResolver> _quotaResolver = new();
    private readonly VehicleService _service;

    public VehicleServiceTests()
    {
        _service = new VehicleService(_repository.Object, _quotaResolver.Object);
    }

    [Fact]
    public async Task CreateVehicleAsync_FirstVehicle_BecomesPrimaryAndUsesOwnerFromArgument()
    {
        var ownerId = Guid.NewGuid();
        _repository.Setup(repository => repository.CountActiveByOwnerAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _quotaResolver.Setup(resolver => resolver.GetMaxVehiclesAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _repository.Setup(repository => repository.ExistsByPublicIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _service.CreateVehicleAsync(ownerId, ValidCreateRequest());

        Assert.True(result.EsPrincipal);
        Assert.Matches("^VEH-[A-Za-z0-9_-]{22}$", result.PublicVehicleId);
        _repository.Verify(repository => repository.AddAsync(
            It.Is<Vehicle>(vehicle => vehicle.OwnerUserId == ownerId
                && vehicle.EsPrincipal
                && vehicle.Activo),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateVehicleAsync_AtQuota_ThrowsConflict()
    {
        var ownerId = Guid.NewGuid();
        _repository.Setup(repository => repository.CountActiveByOwnerAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _quotaResolver.Setup(resolver => resolver.GetMaxVehiclesAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateVehicleAsync(ownerId, ValidCreateRequest()));

        _repository.Verify(repository => repository.AddAsync(
            It.IsAny<Vehicle>(),
            It.IsAny<bool>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateVehicleAsync_RequestPrimary_SwitchesPrimaryThroughRepository()
    {
        var ownerId = Guid.NewGuid();
        _repository.Setup(repository => repository.CountActiveByOwnerAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        _quotaResolver.Setup(resolver => resolver.GetMaxVehiclesAsync(ownerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);
        _repository.Setup(repository => repository.ExistsByPublicIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var request = ValidCreateRequest();
        request.EsPrincipal = true;

        await _service.CreateVehicleAsync(ownerId, request);

        _repository.Verify(repository => repository.AddAsync(
            It.Is<Vehicle>(vehicle => vehicle.EsPrincipal),
            true,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetVehicleAsync_MissingOrForeign_ThrowsSameNotFound()
    {
        var ownerId = Guid.NewGuid();
        _repository.Setup(repository => repository.GetByPublicIdAsync(
                ownerId,
                "VEH-missing",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Vehicle?)null);

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.GetVehicleAsync(ownerId, "VEH-missing"));

        Assert.Equal("Vehículo no encontrado.", exception.Message);
    }

    [Fact]
    public async Task UpdateVehicleAsync_PreservesOwnershipAndPublicId()
    {
        var ownerId = Guid.NewGuid();
        var vehicle = CreateVehicle(ownerId);
        _repository.Setup(repository => repository.GetByPublicIdAsync(
                ownerId,
                vehicle.PublicVehicleId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);

        var request = new UpdateVehicleRequest
        {
            TipoVehiculo = TipoVehiculo.Suv,
            Marca = "  Honda  ",
            Modelo = "  CR-V  ",
            Ano = 2025,
            VelocidadPromedio = 76,
            UsoPrincipalVehiculo = UsoPrincipalVehiculo.Mixto
        };

        var result = await _service.UpdateVehicleAsync(ownerId, vehicle.PublicVehicleId, request);

        Assert.Equal(vehicle.PublicVehicleId, result.PublicVehicleId);
        Assert.Equal(ownerId, vehicle.OwnerUserId);
        Assert.Equal("Honda", result.Marca);
        Assert.Equal("CR-V", result.Modelo);
        _repository.Verify(repository => repository.UpdateAsync(vehicle, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteVehicleAsync_DelegatesAtomicSoftDelete()
    {
        var ownerId = Guid.NewGuid();
        var vehicle = CreateVehicle(ownerId);
        _repository.Setup(repository => repository.GetByPublicIdAsync(
                ownerId,
                vehicle.PublicVehicleId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);

        await _service.DeleteVehicleAsync(ownerId, vehicle.PublicVehicleId);

        _repository.Verify(repository => repository.SoftDeleteAsync(
            ownerId,
            vehicle.PublicVehicleId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SetPrimaryVehicleAsync_DelegatesAtomicSwitch()
    {
        var ownerId = Guid.NewGuid();
        var vehicle = CreateVehicle(ownerId);
        _repository.Setup(repository => repository.GetByPublicIdAsync(
                ownerId,
                vehicle.PublicVehicleId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(vehicle);

        await _service.SetPrimaryVehicleAsync(ownerId, vehicle.PublicVehicleId);

        _repository.Verify(repository => repository.SetPrimaryAsync(
            ownerId,
            vehicle.PublicVehicleId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(1885, 10)]
    [InlineData(2101, 10)]
    [InlineData(2024, -1)]
    [InlineData(2024, 301)]
    public async Task CreateVehicleAsync_InvalidNumericValues_ThrowsBadRequest(int ano, double velocidad)
    {
        var request = ValidCreateRequest();
        request.Ano = ano;
        request.VelocidadPromedio = velocidad;

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _service.CreateVehicleAsync(Guid.NewGuid(), request));
    }

    [Fact]
    [Trait("Category", "Security")]
    public void VehicleDto_DoesNotExposeInternalIdsOrOwner()
    {
        var propertyNames = typeof(VehicleDto).GetProperties().Select(property => property.Name).ToList();

        Assert.DoesNotContain("Id", propertyNames);
        Assert.DoesNotContain("OwnerUserId", propertyNames);
        Assert.DoesNotContain("Activo", propertyNames);
        Assert.DoesNotContain("DeletedAtUtc", propertyNames);
    }

    private static CreateVehicleRequest ValidCreateRequest()
    {
        return new CreateVehicleRequest
        {
            TipoVehiculo = TipoVehiculo.Automovil,
            Marca = "Toyota",
            Modelo = "Corolla",
            Ano = 2024,
            VelocidadPromedio = 65,
            UsoPrincipalVehiculo = UsoPrincipalVehiculo.Ciudad
        };
    }

    private static Vehicle CreateVehicle(Guid ownerId)
    {
        return new Vehicle
        {
            Id = Guid.NewGuid(),
            PublicVehicleId = "VEH-abcdefghijklmnopqrstuv",
            OwnerUserId = ownerId,
            TipoVehiculo = TipoVehiculo.Automovil,
            Marca = "Toyota",
            Modelo = "Corolla",
            Ano = 2024,
            VelocidadPromedio = 65,
            UsoPrincipalVehiculo = UsoPrincipalVehiculo.Ciudad,
            Activo = true,
            EsPrincipal = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}
