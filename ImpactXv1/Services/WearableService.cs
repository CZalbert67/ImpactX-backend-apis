using Prueba1.Core.Domain;
using Prueba1.Core.Interfaces.Repositories;
using Prueba1.Models.DTOs;

namespace Prueba1.Services;

public class WearableService : IWearableService
{
    private readonly IWearableRepository _wearableRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPlanService _planService;

    public WearableService(
        IWearableRepository wearableRepository,
        IUsuarioRepository usuarioRepository,
        IPlanService planService)
    {
        _wearableRepository = wearableRepository;
        _usuarioRepository = usuarioRepository;
        _planService = planService;
    }

    public async Task<WearableDto?> GetWearableAsync(Guid usuarioId)
    {
        var wearable = await _wearableRepository.GetByUsuarioIdAsync(usuarioId);
        return wearable is null ? null : MapToDto(wearable);
    }

    public async Task<PairResponse> PairAsync(Guid usuarioId, PairWearableRequest request)
    {
        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId)
            ?? throw new KeyNotFoundException("Usuario no encontrado.");

        var suscripcion = await _planService.GetCurrentSubscriptionAsync(usuarioId);
        var planName = suscripcion?.PlanNombre ?? "Free";

        var existing = await _wearableRepository.GetAllByUsuarioIdAsync(usuarioId);
        var vinculados = existing.Count(w => w.Estado == "Vinculado");

        if (planName == "Free" && vinculados >= 1)
            throw new InvalidOperationException(
                "El plan Free permite solo 1 wearable. Actualiza tu plan para vincular más.");

        var existingDevice = await _wearableRepository.GetByDispositivoIdAsync(request.DispositivoId);
        if (existingDevice is not null)
            throw new InvalidOperationException("Este dispositivo ya está vinculado a otra cuenta.");

        var token = Guid.NewGuid().ToString("N")[..8].ToUpper();

        var wearable = new Wearable
        {
            UsuarioId = usuarioId,
            DispositivoId = request.DispositivoId,
            Nombre = request.Nombre,
            Modelo = request.Modelo,
            PairingToken = token,
            Estado = "Pendiente",
            VinculadoEn = DateTime.UtcNow,
        };

        await _wearableRepository.AddAsync(wearable);

        return new PairResponse
        {
            Token = token,
            Mensaje = "Código de vinculación generado. Confírmalo desde el wearable."
        };
    }

    public async Task<WearableDto> PairConfirmAsync(Guid usuarioId, PairConfirmRequest request)
    {
        var wearable = await _wearableRepository.GetByPairingTokenAsync(request.Token)
            ?? throw new InvalidOperationException("Token de vinculación inválido o expirado.");

        if (wearable.UsuarioId != usuarioId)
            throw new InvalidOperationException("Este token no pertenece al usuario actual.");

        if (wearable.Estado != "Pendiente")
            throw new InvalidOperationException("Este wearable ya fue vinculado.");

        wearable.Estado = "Vinculado";
        wearable.Connected = true;
        wearable.PairingToken = null;
        await _wearableRepository.UpdateAsync(wearable);

        return MapToDto(wearable);
    }

    public async Task<List<TelemetryPointDto>> SyncAsync(Guid usuarioId, SyncTelemetryRequest request)
    {
        var wearable = await _wearableRepository.GetByUsuarioIdAsync(usuarioId)
            ?? throw new InvalidOperationException("No hay un wearable vinculado.");

        wearable.UltimaSincronizacion = DateTime.UtcNow;
        wearable.Connected = true;
        await _wearableRepository.UpdateAsync(wearable);

        return request.Puntos;
    }

    public async Task<WearableDto> CalibrateAsync(Guid usuarioId, CalibrationRequest request)
    {
        var wearable = await _wearableRepository.GetByUsuarioIdAsync(usuarioId)
            ?? throw new InvalidOperationException("No hay un wearable vinculado.");

        wearable.Calibrado = true;
        wearable.UltimaCalibracion = DateTime.UtcNow;
        await _wearableRepository.UpdateAsync(wearable);

        return MapToDto(wearable);
    }

    public async Task UnlinkAsync(Guid usuarioId)
    {
        var wearables = await _wearableRepository.GetAllByUsuarioIdAsync(usuarioId);
        var vinculados = wearables.Where(w => w.Estado == "Vinculado").ToList();

        if (vinculados.Count == 0)
            throw new InvalidOperationException("No hay un wearable vinculado para desvincular.");

        foreach (var w in vinculados)
        {
            w.Estado = "Desvinculado";
            w.Connected = false;
            await _wearableRepository.UpdateAsync(w);
        }
    }

    public async Task<WearableDto> UpdatePermissionsAsync(Guid usuarioId, UpdateWearablePermissionsRequest request)
    {
        var wearable = await _wearableRepository.GetByUsuarioIdAsync(usuarioId)
            ?? throw new InvalidOperationException("No hay un wearable vinculado.");

        wearable.PermisosOtorgados = request.Permisos;
        await _wearableRepository.UpdateAsync(wearable);

        return MapToDto(wearable);
    }

    public async Task<SensorDiagnosticsDto> GetSensorDiagnosticsAsync(Guid usuarioId)
    {
        var wearable = await _wearableRepository.GetByUsuarioIdAsync(usuarioId)
            ?? throw new InvalidOperationException("No hay un wearable vinculado.");

        return new SensorDiagnosticsDto
        {
            Acelerometro = true,
            Giroscopio = true,
            Magnetometro = true,
            Gps = true,
            FrecuenciaCardiaca = true,
            NivelBateria = wearable.NivelBateria,
            UltimoDiagnostico = DateTime.UtcNow,
        };
    }

    public async Task<WearableDto> UpdateBatteryAsync(Guid usuarioId, BatteryUpdateRequest request)
    {
        var wearable = await _wearableRepository.GetByUsuarioIdAsync(usuarioId)
            ?? throw new InvalidOperationException("No hay un wearable vinculado.");

        wearable.NivelBateria = Math.Clamp(request.Nivel, 0, 100);
        await _wearableRepository.UpdateAsync(wearable);

        return MapToDto(wearable);
    }

    private static WearableDto MapToDto(Wearable w) => new()
    {
        Id = w.Id,
        DispositivoId = w.DispositivoId,
        Nombre = w.Nombre,
        Modelo = w.Modelo,
        VinculadoEn = w.VinculadoEn,
        UltimaSincronizacion = w.UltimaSincronizacion,
        AppVersion = w.AppVersion,
        Connected = w.Connected,
        NivelBateria = w.NivelBateria,
        Calibrado = w.Calibrado,
        UltimaCalibracion = w.UltimaCalibracion,
        PermisosOtorgados = w.PermisosOtorgados,
        Estado = w.Estado,
    };
}
