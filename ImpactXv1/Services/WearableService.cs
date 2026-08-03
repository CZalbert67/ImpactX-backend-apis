using System.Security.Cryptography;
using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using ImpactX.Core.Security;
using ImpactX.Core.Telemetry;
using ImpactX.Core.Wearables;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

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

    public async Task<PagedResult<WearableDto>> GetWearablesPagedAsync(
        Guid usuarioId,
        int? pageSize,
        string? continuationToken)
    {
        var size = PaginationValidator.Resolve(pageSize, continuationToken);
        var page = await _wearableRepository.GetAllByUsuarioIdPagedAsync(usuarioId, size, continuationToken);
        return new PagedResult<WearableDto>
        {
            Items = page.Items.Select(MapToDto).ToList(),
            ContinuationToken = page.ContinuationToken,
            HasMoreResults = page.HasMoreResults,
            PageSize = page.PageSize,
        };
    }

    public async Task<PairResponse> PairAsync(Guid usuarioId, PairWearableRequest request)
    {
        _ = await _usuarioRepository.GetByIdAsync(usuarioId)
            ?? throw new NotFoundException("Usuario no encontrado.");

        ValidatePairRequest(request);

        var subscription = await _planService.GetCurrentSubscriptionAsync(usuarioId);
        var planName = subscription?.PlanNombre ?? "Free";

        var existing = await _wearableRepository.GetAllByUsuarioIdAsync(usuarioId);
        var linkedCount = existing.Count(wearable => wearable.Estado == "Vinculado");

        if (planName == "Free" && linkedCount >= 1)
        {
            throw new ConflictException(
                "El plan Free permite solo 1 wearable. Actualiza tu plan para vincular más.");
        }

        var deviceId = request.DispositivoId.Trim();
        var existingDevice = await _wearableRepository.GetByDispositivoIdAsync(deviceId);
        if (existingDevice is not null)
            throw new ConflictException("Este dispositivo ya está vinculado a otra cuenta.");

        var now = DateTime.UtcNow;
        var token = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var expiresAtUtc = now.Add(WearableProductPolicy.PairingLifetime);
        var codigoEmparejamiento = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        var trustToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

        var wearable = new Wearable
        {
            UsuarioId = usuarioId,
            DispositivoId = deviceId,
            Nombre = request.Nombre.Trim(),
            Modelo = WearableProductPolicy.NormalizeModel(request.Modelo),
            Fabricante = WearableProductPolicy.NormalizeManufacturer(request.Fabricante),
            Plataforma = WearableProductPolicy.NormalizePlatform(request.Plataforma),
            VersionSistemaOperativo = NormalizeVersion(request.VersionSistemaOperativo),
            VersionFirmware = NormalizeVersion(request.VersionFirmware),
            AppVersion = NormalizeVersion(request.AppVersion),
            CapacidadesSensores = WearableProductPolicy.NormalizeCapabilities(request.CapacidadesSensores).ToList(),
            // Se persiste solo el hash; el código en claro se devuelve una vez.
            PairingToken = InvitationCodeHasher.Hash(token),
            PairingExpiresAtUtc = expiresAtUtc,
            CodigoEmparejamiento = codigoEmparejamiento,
            TrustToken = trustToken,
            Estado = "Pendiente",
            VinculadoEn = now,
        };

        await _wearableRepository.AddAsync(wearable);

        return new PairResponse
        {
            Token = token,
            CodigoEmparejamiento = codigoEmparejamiento,
            TrustToken = trustToken,
            ExpiresAtUtc = expiresAtUtc,
            Mensaje = "Código de vinculación generado. Confirma la vinculación antes de que expire.",
        };
    }

    public async Task<WearableDto> PairConfirmAsync(Guid usuarioId, PairConfirmRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
            throw new BadRequestException("El token de vinculación es obligatorio.");

        var token = request.Token.Trim().ToUpperInvariant();
        var wearable = await _wearableRepository.GetByPairingTokenAsync(token)
            ?? throw new ConflictException("Token de vinculación inválido o expirado.");

        if (wearable.UsuarioId != usuarioId)
            throw new ConflictException("Este token no pertenece al usuario actual.");

        if (wearable.PairingExpiresAtUtc is DateTime expiresAt && expiresAt <= DateTime.UtcNow)
        {
            wearable.Estado = "Expirado";
            wearable.PairingToken = null;
            await _wearableRepository.UpdateAsync(wearable);
            throw new ConflictException("Token de vinculación inválido o expirado.");
        }

        if (wearable.Estado != "Pendiente")
            throw new ConflictException("Este wearable ya fue vinculado.");

        wearable.Estado = "Vinculado";
        wearable.Connected = true;
        wearable.PairingToken = null;
        wearable.PairingExpiresAtUtc = null;
        wearable.UltimaSincronizacion = DateTime.UtcNow;
        await _wearableRepository.UpdateAsync(wearable);

        return MapToDto(wearable);
    }

    public async Task<List<TelemetryPointDto>> SyncAsync(Guid usuarioId, SyncTelemetryRequest request)
    {
        var wearable = await GetLinkedWearableAsync(usuarioId);

        wearable.UltimaSincronizacion = DateTime.UtcNow;
        wearable.UltimoHeartbeatUtc = DateTime.UtcNow;
        wearable.Connected = true;
        await _wearableRepository.UpdateAsync(wearable);

        return request.Puntos ?? [];
    }

    public async Task<WearableDto> CalibrateAsync(Guid usuarioId, CalibrationRequest request)
    {
        var wearable = await GetLinkedWearableAsync(usuarioId);

        var available = new List<string>();
        if (request.Acelerometro) available.Add("accelerometer");
        if (request.Giroscopio) available.Add("gyroscope");
        if (request.Magnetometro) available.Add("magnetometer");
        if (request.Gps) available.Add("gps");

        wearable.Calibrado = request.Acelerometro && request.Giroscopio && request.Gps;
        wearable.CalibracionPorcentaje = wearable.Calibrado ? 100 : 0;
        wearable.UltimaCalibracion = DateTime.UtcNow;
        wearable.SensoresDisponibles = available;
        await _wearableRepository.UpdateAsync(wearable);

        return MapToDto(wearable);
    }

    public async Task UnlinkAsync(Guid usuarioId)
    {
        var wearables = await _wearableRepository.GetAllByUsuarioIdAsync(usuarioId);
        var linked = wearables.Where(wearable => wearable.Estado == "Vinculado").ToList();

        if (linked.Count == 0)
            throw new ConflictException("No hay un wearable vinculado para desvincular.");

        foreach (var wearable in linked)
        {
            wearable.Estado = "Desvinculado";
            wearable.Connected = false;
            wearable.PairingToken = null;
            wearable.PairingExpiresAtUtc = null;
            await _wearableRepository.UpdateAsync(wearable);
        }
    }

    public async Task<WearableDto> UpdatePermissionsAsync(
        Guid usuarioId,
        UpdateWearablePermissionsRequest request)
    {
        var wearable = await GetLinkedWearableAsync(usuarioId);
        wearable.PermisosOtorgados = WearableProductPolicy
            .NormalizeCapabilities(request.Permisos)
            .ToList();
        await _wearableRepository.UpdateAsync(wearable);
        return MapToDto(wearable);
    }

    public async Task<SensorDiagnosticsDto> GetSensorDiagnosticsAsync(Guid usuarioId)
    {
        var wearable = await GetLinkedWearableAsync(usuarioId);
        var reportedAvailable = wearable.SensoresDisponibles ?? [];
        var capabilities = wearable.CapacidadesSensores ?? [];
        var available = reportedAvailable.Count > 0 ? reportedAvailable : capabilities;

        return new SensorDiagnosticsDto
        {
            Modelo = wearable.Modelo,
            Fabricante = wearable.Fabricante,
            Plataforma = wearable.Plataforma,
            Acelerometro = HasSensor(available, "accelerometer", "acelerometro"),
            Giroscopio = HasSensor(available, "gyroscope", "giroscopio"),
            Magnetometro = HasSensor(available, "magnetometer", "magnetometro"),
            Gps = HasSensor(available, "gps"),
            FrecuenciaCardiaca = HasSensor(available, "heart_rate", "frecuencia_cardiaca"),
            Hrv = HasSensor(available, "hrv"),
            Spo2 = HasSensor(available, "spo2"),
            CalidadGeneral = TelemetrySchema.NormalizeQuality(wearable.CalidadSensores)
                ?? TelemetrySchema.QualityUnknown,
            SensoresDisponibles = wearable.SensoresDisponibles?.ToList() ?? [],
            SensoresNoDisponibles = wearable.SensoresNoDisponibles?.ToList() ?? [],
            NivelBateria = wearable.NivelBateria,
            UltimoDiagnosticoUtc = wearable.UltimoDiagnosticoUtc,
        };
    }

    public async Task<WearableDto> UpdateBatteryAsync(Guid usuarioId, BatteryUpdateRequest request)
    {
        if (request.Nivel is < 0 or > 100)
            throw new BadRequestException("El nivel de batería debe estar entre 0 y 100.");

        var wearable = await GetLinkedWearableAsync(usuarioId);
        wearable.NivelBateria = request.Nivel;
        wearable.Cargando = request.Cargando;
        wearable.UltimoHeartbeatUtc = DateTime.UtcNow;
        wearable.Connected = true;
        await _wearableRepository.UpdateAsync(wearable);
        return MapToDto(wearable);
    }

    public async Task<WearableDto> RegisterHeartbeatAsync(
        Guid usuarioId,
        WearableHeartbeatRequest request)
    {
        ValidateHeartbeat(request);
        var wearable = await GetLinkedWearableAsync(usuarioId);

        if (!string.Equals(wearable.DispositivoId, request.DispositivoId.Trim(), StringComparison.Ordinal))
            throw new NotFoundException("Wearable vinculado no encontrado.");

        wearable.Modelo = WearableProductPolicy.NormalizeModel(request.Modelo);
        wearable.Fabricante = WearableProductPolicy.NormalizeManufacturer(request.Fabricante);
        wearable.Plataforma = WearableProductPolicy.NormalizePlatform(request.Plataforma);
        wearable.AppVersion = NormalizeVersion(request.AppVersion);
        wearable.VersionSistemaOperativo = NormalizeVersion(request.VersionSistemaOperativo);
        wearable.VersionFirmware = NormalizeVersion(request.VersionFirmware);
        wearable.NivelBateria = request.NivelBateria;
        wearable.Cargando = request.Cargando;
        wearable.DesfaseRelojMilisegundos = request.DesfaseRelojMilisegundos;
        wearable.CapacidadesSensores = WearableProductPolicy.NormalizeCapabilities(request.CapacidadesSensores).ToList();
        wearable.UltimoHeartbeatUtc = DateTime.UtcNow;
        wearable.UltimaSincronizacion = DateTime.UtcNow;
        wearable.Connected = true;
        await _wearableRepository.UpdateAsync(wearable);

        return MapToDto(wearable);
    }

    public async Task<WearableDto> ReportDiagnosticsAsync(
        Guid usuarioId,
        WearableDiagnosticsReportRequest request)
    {
        ValidateTimestamp(request.TimestampUtc);

        var quality = TelemetrySchema.NormalizeQuality(request.CalidadGeneral)
            ?? throw new BadRequestException("calidadGeneral debe ser unknown, low, medium o high.");

        if (string.IsNullOrWhiteSpace(request.DispositivoId))
            throw new BadRequestException("dispositivoId es obligatorio.");

        var wearable = await GetLinkedWearableAsync(usuarioId);
        if (!string.Equals(wearable.DispositivoId, request.DispositivoId.Trim(), StringComparison.Ordinal))
            throw new NotFoundException("Wearable vinculado no encontrado.");

        ValidateCapabilities(request.SensoresDisponibles);
        ValidateCapabilities(request.SensoresNoDisponibles);
        var available = WearableProductPolicy.NormalizeCapabilities(request.SensoresDisponibles).ToList();
        var unavailable = WearableProductPolicy.NormalizeCapabilities(request.SensoresNoDisponibles).ToList();

        if (available.Intersect(unavailable, StringComparer.Ordinal).Any())
            throw new BadRequestException("Un sensor no puede aparecer como disponible y no disponible al mismo tiempo.");

        wearable.SensoresDisponibles = available;
        wearable.SensoresNoDisponibles = unavailable;
        wearable.CalidadSensores = quality;
        wearable.UltimoDiagnosticoUtc = DateTime.UtcNow;
        wearable.UltimoHeartbeatUtc = DateTime.UtcNow;
        wearable.Connected = true;
        await _wearableRepository.UpdateAsync(wearable);

        return MapToDto(wearable);
    }

    private async Task<Wearable> GetLinkedWearableAsync(Guid userId)
        => await _wearableRepository.GetByUsuarioIdAsync(userId)
           ?? throw new ConflictException("No hay un wearable vinculado.");

    private static void ValidatePairRequest(PairWearableRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DispositivoId)
            || request.DispositivoId.Trim().Length > WearableProductPolicy.MaxDeviceIdLength)
        {
            throw new BadRequestException("dispositivoId es obligatorio y no puede superar 200 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(request.Nombre)
            || request.Nombre.Trim().Length > WearableProductPolicy.MaxNameLength)
        {
            throw new BadRequestException("El nombre del wearable es obligatorio y no puede superar 200 caracteres.");
        }

        if (!WearableProductPolicy.IsTargetDevice(request.Fabricante, request.Modelo, request.Plataforma))
            throw new BadRequestException("El prototipo admite únicamente Samsung Galaxy Watch 8 con WearOS.");

        ValidateVersion(request.AppVersion, "appVersion");
        ValidateVersion(request.VersionSistemaOperativo, "versionSistemaOperativo");
        ValidateVersion(request.VersionFirmware, "versionFirmware");
        ValidateCapabilities(request.CapacidadesSensores);
    }

    private static void ValidateHeartbeat(WearableHeartbeatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DispositivoId)
            || request.DispositivoId.Trim().Length > WearableProductPolicy.MaxDeviceIdLength)
        {
            throw new BadRequestException("dispositivoId es obligatorio y no puede superar 200 caracteres.");
        }

        if (!WearableProductPolicy.IsTargetDevice(request.Fabricante, request.Modelo, request.Plataforma))
            throw new BadRequestException("El heartbeat no corresponde al wearable objetivo.");

        if (request.NivelBateria is < 0 or > 100)
            throw new BadRequestException("nivelBateria debe estar entre 0 y 100.");

        if (request.DesfaseRelojMilisegundos is < TelemetryIngestionLimits.MinClockOffsetMilliseconds
            or > TelemetryIngestionLimits.MaxClockOffsetMilliseconds)
        {
            throw new BadRequestException("desfaseRelojMilisegundos está fuera del rango permitido.");
        }

        ValidateTimestamp(request.TimestampUtc);
        ValidateVersion(request.AppVersion, "appVersion");
        ValidateVersion(request.VersionSistemaOperativo, "versionSistemaOperativo");
        ValidateVersion(request.VersionFirmware, "versionFirmware");
        ValidateCapabilities(request.CapacidadesSensores);
    }

    private static void ValidateTimestamp(DateTime timestampUtc)
    {
        if (timestampUtc == default || timestampUtc.Kind != DateTimeKind.Utc)
            throw new BadRequestException("timestampUtc debe estar en UTC.");

        if (timestampUtc > DateTime.UtcNow.Add(TelemetryIngestionLimits.MaxFutureTolerance))
            throw new BadRequestException("timestampUtc no puede estar más de 5 minutos en el futuro.");
    }

    private static void ValidateVersion(string? value, string field)
    {
        if (value is null)
            return;

        if (value.Trim().Length > WearableProductPolicy.MaxVersionLength || value.Any(char.IsControl))
            throw new BadRequestException($"{field} contiene un valor no permitido.");
    }

    private static void ValidateCapabilities(IEnumerable<string>? capabilities)
    {
        if (capabilities is null)
            return;

        var list = capabilities.ToList();
        if (list.Count > WearableProductPolicy.MaxSensorCapabilities)
            throw new BadRequestException("La lista de capacidades de sensores es demasiado grande.");

        if (list.Any(value => string.IsNullOrWhiteSpace(value)
                              || value.Trim().Length > WearableProductPolicy.MaxSensorCapabilityLength
                              || value.Any(char.IsControl)))
        {
            throw new BadRequestException("Las capacidades de sensores contienen valores no permitidos.");
        }
    }

    private static bool HasSensor(IEnumerable<string> values, params string[] aliases)
    {
        var set = values.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return aliases.Any(set.Contains);
    }

    private static string? NormalizeVersion(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static WearableDto MapToDto(Wearable wearable) => new()
    {
        Id = wearable.Id,
        DispositivoId = wearable.DispositivoId,
        Nombre = wearable.Nombre,
        Modelo = wearable.Modelo,
        Fabricante = wearable.Fabricante,
        Plataforma = wearable.Plataforma,
        VinculadoEn = wearable.VinculadoEn,
        UltimaSincronizacion = wearable.UltimaSincronizacion,
        UltimoHeartbeatUtc = wearable.UltimoHeartbeatUtc,
        UltimoDiagnosticoUtc = wearable.UltimoDiagnosticoUtc,
        AppVersion = wearable.AppVersion,
        VersionSistemaOperativo = wearable.VersionSistemaOperativo,
        VersionFirmware = wearable.VersionFirmware,
        Connected = wearable.Connected,
        Cargando = wearable.Cargando,
        NivelBateria = wearable.NivelBateria,
        DesfaseRelojMilisegundos = wearable.DesfaseRelojMilisegundos,
        Calibrado = wearable.Calibrado,
        CalibracionPorcentaje = wearable.CalibracionPorcentaje,
        UltimaCalibracion = wearable.UltimaCalibracion,
        PermisosOtorgados = wearable.PermisosOtorgados?.ToList() ?? [],
        CapacidadesSensores = wearable.CapacidadesSensores?.ToList() ?? [],
        SensoresDisponibles = wearable.SensoresDisponibles?.ToList() ?? [],
        SensoresNoDisponibles = wearable.SensoresNoDisponibles?.ToList() ?? [],
        CalidadSensores = wearable.CalidadSensores,
        CodigoEmparejamiento = wearable.CodigoEmparejamiento,
        TrustToken = wearable.TrustToken,
        SensoresActivos = wearable.SensoresActivos is null ? null : new WearableSensoresDto
        {
            Acelerometro = wearable.SensoresActivos.Acelerometro,
            Microfono = wearable.SensoresActivos.Microfono,
            FrecuenciaCardiaca = wearable.SensoresActivos.FrecuenciaCardiaca,
            Gps = wearable.SensoresActivos.Gps,
            SegundoPlano = wearable.SensoresActivos.SegundoPlano,
        },
        Estado = wearable.Estado,
    };
}
