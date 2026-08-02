using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Identity;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Models.DTOs.Vehicles;

namespace ImpactX.Services;

public class VehicleService : IVehicleService
{
    private const int PublicIdGenerationAttempts = 10;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IVehicleQuotaResolver _quotaResolver;

    public VehicleService(
        IVehicleRepository vehicleRepository,
        IVehicleQuotaResolver quotaResolver)
    {
        _vehicleRepository = vehicleRepository;
        _quotaResolver = quotaResolver;
    }

    public async Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var vehicles = await _vehicleRepository.GetAllByOwnerAsync(
            usuarioId,
            cancellationToken);
        return vehicles.Select(MapToDto).ToList();
    }

    public async Task<VehicleDto> GetVehicleAsync(
        Guid usuarioId,
        string publicVehicleId,
        CancellationToken cancellationToken = default)
    {
        var vehicle = await GetOwnedVehicleAsync(
            usuarioId,
            publicVehicleId,
            cancellationToken);
        return MapToDto(vehicle);
    }

    public async Task<VehicleDto> CreateVehicleAsync(
        Guid usuarioId,
        CreateVehicleRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.TipoVehiculo, request.Marca, request.Modelo,
            request.Ano, request.VelocidadPromedio, request.UsoPrincipalVehiculo);

        var activeCount = await _vehicleRepository.CountActiveByOwnerAsync(
            usuarioId,
            cancellationToken);
        var maxVehicles = await _quotaResolver.GetMaxVehiclesAsync(
            usuarioId,
            cancellationToken);

        if (activeCount >= maxVehicles)
        {
            throw new ConflictException("Has alcanzado el límite de vehículos de tu plan.");
        }

        var makePrimary = activeCount == 0 || request.EsPrincipal == true;
        var now = DateTime.UtcNow;
        var vehicle = new Vehicle
        {
            Id = Guid.NewGuid(),
            PublicVehicleId = await GenerateUniquePublicVehicleIdAsync(cancellationToken),
            OwnerUserId = usuarioId,
            TipoVehiculo = request.TipoVehiculo,
            Marca = request.Marca.Trim(),
            Modelo = request.Modelo.Trim(),
            Ano = request.Ano,
            VelocidadPromedio = request.VelocidadPromedio,
            UsoPrincipalVehiculo = request.UsoPrincipalVehiculo,
            EsPrincipal = makePrimary,
            Activo = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await _vehicleRepository.AddAsync(vehicle, makePrimary, cancellationToken);
        return MapToDto(vehicle);
    }

    public async Task<VehicleDto> UpdateVehicleAsync(
        Guid usuarioId,
        string publicVehicleId,
        UpdateVehicleRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request.TipoVehiculo, request.Marca, request.Modelo,
            request.Ano, request.VelocidadPromedio, request.UsoPrincipalVehiculo);

        var vehicle = await GetOwnedVehicleAsync(
            usuarioId,
            publicVehicleId,
            cancellationToken);

        vehicle.TipoVehiculo = request.TipoVehiculo;
        vehicle.Marca = request.Marca.Trim();
        vehicle.Modelo = request.Modelo.Trim();
        vehicle.Ano = request.Ano;
        vehicle.VelocidadPromedio = request.VelocidadPromedio;
        vehicle.UsoPrincipalVehiculo = request.UsoPrincipalVehiculo;
        vehicle.UpdatedAtUtc = DateTime.UtcNow;

        await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);
        return MapToDto(vehicle);
    }

    public async Task DeleteVehicleAsync(
        Guid usuarioId,
        string publicVehicleId,
        CancellationToken cancellationToken = default)
    {
        await GetOwnedVehicleAsync(usuarioId, publicVehicleId, cancellationToken);
        await _vehicleRepository.SoftDeleteAsync(
            usuarioId,
            publicVehicleId,
            cancellationToken);
    }

    public async Task SetPrimaryVehicleAsync(
        Guid usuarioId,
        string publicVehicleId,
        CancellationToken cancellationToken = default)
    {
        await GetOwnedVehicleAsync(usuarioId, publicVehicleId, cancellationToken);
        await _vehicleRepository.SetPrimaryAsync(
            usuarioId,
            publicVehicleId,
            cancellationToken);
    }

    private async Task<Vehicle> GetOwnedVehicleAsync(
        Guid usuarioId,
        string publicVehicleId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicVehicleId))
        {
            throw new NotFoundException("Vehículo no encontrado.");
        }

        return await _vehicleRepository.GetByPublicIdAsync(
            usuarioId,
            publicVehicleId.Trim(),
            cancellationToken)
            ?? throw new NotFoundException("Vehículo no encontrado.");
    }

    private async Task<string> GenerateUniquePublicVehicleIdAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < PublicIdGenerationAttempts; attempt++)
        {
            var candidate = PublicVehicleIdGenerator.Generate();
            if (!await _vehicleRepository.ExistsByPublicIdAsync(
                    candidate,
                    cancellationToken))
            {
                return candidate;
            }
        }

        throw new ConflictException("No fue posible generar un identificador de vehículo único.");
    }

    private static void ValidateRequest(
        TipoVehiculo tipoVehiculo,
        string marca,
        string modelo,
        int ano,
        double velocidadPromedio,
        UsoPrincipalVehiculo usoPrincipalVehiculo)
    {
        if (!Enum.IsDefined(tipoVehiculo))
        {
            throw new BadRequestException("El tipo de vehículo no es válido.");
        }

        if (!Enum.IsDefined(usoPrincipalVehiculo))
        {
            throw new BadRequestException("El uso principal del vehículo no es válido.");
        }

        if (string.IsNullOrWhiteSpace(marca) || marca.Trim().Length > 100)
        {
            throw new BadRequestException("La marca es obligatoria y no puede exceder 100 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(modelo) || modelo.Trim().Length > 100)
        {
            throw new BadRequestException("El modelo es obligatorio y no puede exceder 100 caracteres.");
        }

        if (ano < 1886 || ano > 2100)
        {
            throw new BadRequestException("El año debe estar entre 1886 y 2100.");
        }

        if (double.IsNaN(velocidadPromedio)
            || double.IsInfinity(velocidadPromedio)
            || velocidadPromedio < 0
            || velocidadPromedio > 300)
        {
            throw new BadRequestException("La velocidad promedio debe estar entre 0 y 300.");
        }
    }

    private static VehicleDto MapToDto(Vehicle vehicle)
    {
        return new VehicleDto
        {
            PublicVehicleId = vehicle.PublicVehicleId,
            TipoVehiculo = vehicle.TipoVehiculo,
            Marca = vehicle.Marca,
            Modelo = vehicle.Modelo,
            Ano = vehicle.Ano,
            VelocidadPromedio = vehicle.VelocidadPromedio,
            UsoPrincipalVehiculo = vehicle.UsoPrincipalVehiculo,
            EsPrincipal = vehicle.EsPrincipal,
            CreatedAtUtc = vehicle.CreatedAtUtc,
            UpdatedAtUtc = vehicle.UpdatedAtUtc
        };
    }
}
