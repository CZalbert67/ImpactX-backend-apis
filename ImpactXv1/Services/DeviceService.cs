using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;
using Microsoft.Extensions.Logging;

namespace ImpactX.Services;

public class DeviceService : IDeviceService
{
    private readonly IDispositivoRepository _dispositivoRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly ILogger<DeviceService> _logger;

    public DeviceService(
        IDispositivoRepository dispositivoRepository,
        IUsuarioRepository usuarioRepository,
        ILogger<DeviceService> logger)
    {
        _dispositivoRepository = dispositivoRepository;
        _usuarioRepository = usuarioRepository;
        _logger = logger;
    }

    public async Task<List<DeviceDto>> GetDevicesAsync(Guid usuarioId)
    {
        var dispositivos = await _dispositivoRepository.GetByUsuarioIdAsync(usuarioId);
        return dispositivos.Select(MapToDto).ToList();
    }

    public async Task UpsertFcmTokenAsync(Guid usuarioId, UpsertDeviceRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.DeviceId))
        {
            throw new BadRequestException("El DeviceId es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            throw new BadRequestException("El token FCM es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(request.Platform))
        {
            throw new BadRequestException("La plataforma es obligatoria.");
        }

        var platform = NormalizePlatform(request.Platform);

        var now = DateTime.UtcNow;
        var dispositivo = await _dispositivoRepository.GetByDeviceIdAsync(usuarioId, request.DeviceId);
        var owner = await _dispositivoRepository.GetByTokenFcmAsync(request.Token);

        if (owner is not null && owner.Id != dispositivo?.Id)
        {
            if (owner.UsuarioId != usuarioId)
            {
                throw new ConflictException("El token FCM ya está en uso.");
            }

            owner.TokenFcm = string.Empty;
            owner.ActualizadoEn = now;
            owner.Activo = false;
            await _dispositivoRepository.UpdateAsync(owner);
        }

        if (dispositivo is null)
        {
            dispositivo = new Dispositivo
            {
                UsuarioId = usuarioId,
                DeviceId = request.DeviceId,
                Platform = platform,
                TokenFcm = request.Token,
                Nombre = request.Name,
                Activo = true,
                CreadoEn = now,
                ActualizadoEn = now,
                UltimoUsoEn = now,
            };
            await _dispositivoRepository.AddAsync(dispositivo);
            _logger.LogInformation("Dispositivo {DispositivoId} registrado para usuario {UsuarioId} (plataforma {Plataforma})",
                dispositivo.Id, usuarioId, dispositivo.Platform);
        }
        else
        {
            dispositivo.Platform = platform;
            dispositivo.TokenFcm = request.Token;
            dispositivo.Nombre = request.Name;
            dispositivo.Activo = true;
            dispositivo.ActualizadoEn = now;
            dispositivo.UltimoUsoEn = now;
            await _dispositivoRepository.UpdateAsync(dispositivo);
            _logger.LogInformation("Dispositivo {DispositivoId} actualizado para usuario {UsuarioId}", dispositivo.Id, usuarioId);
        }

        await ClearLegacyTokenAsync(usuarioId);
    }

    public async Task DeleteDeviceAsync(Guid usuarioId, Guid deviceId)
    {
        var dispositivo = await _dispositivoRepository.GetByIdAsync(usuarioId, deviceId);

        if (dispositivo is null)
        {
            throw new NotFoundException("Dispositivo no encontrado.");
        }

        await _dispositivoRepository.DeleteAsync(dispositivo);

        var remaining = await _dispositivoRepository.GetActiveByUsuarioIdAsync(usuarioId);
        if (remaining.Count == 0)
        {
            await ClearLegacyTokenAsync(usuarioId);
        }

        _logger.LogInformation("Dispositivo {DispositivoId} eliminado para usuario {UsuarioId}", dispositivo.Id, usuarioId);
    }

    public async Task DeleteAllDevicesAsync(Guid usuarioId)
    {
        var count = await _dispositivoRepository.DeleteAllByUsuarioIdAsync(usuarioId);
        await ClearLegacyTokenAsync(usuarioId);
        _logger.LogInformation("{Count} dispositivos revocados para usuario {UsuarioId}", count, usuarioId);
    }

    private async Task ClearLegacyTokenAsync(Guid usuarioId)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is not null && !string.IsNullOrEmpty(usuario.FcmToken))
        {
            usuario.FcmToken = null;
            await _usuarioRepository.UpdateAsync(usuario);
        }
    }

    private static string NormalizePlatform(string platform)
    {
        return platform.Trim().ToLowerInvariant() switch
        {
            "android" => "Android",
            "wearos" => "WearOS",
            "web" => "Web",
            _ => throw new BadRequestException("La plataforma debe ser Android, WearOS o Web.")
        };
    }

    private static DeviceDto MapToDto(Dispositivo d) => new()
    {
        Id = d.Id,
        DeviceId = d.DeviceId,
        Platform = d.Platform,
        Nombre = d.Nombre,
        Activo = d.Activo,
        CreadoEn = d.CreadoEn,
        ActualizadoEn = d.ActualizadoEn,
        UltimoUsoEn = d.UltimoUsoEn,
    };
}
