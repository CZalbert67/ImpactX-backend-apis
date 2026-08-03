using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.ImpactDetection;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Pagination;
using ImpactX.Core.Security;
using ImpactX.Core.Telemetry;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class ViajeService : IViajeService
{
    private readonly IViajeRepository _viajeRepository;
    private readonly ILogger<ViajeService> _logger;
    private readonly IVehicleRepository? _vehicleRepository;
    private readonly IImpactDetectionEngine? _impactDetectionEngine;
    private readonly IImpactAlertOrchestrator? _impactAlertOrchestrator;

    public ViajeService(
        IViajeRepository viajeRepository,
        ILogger<ViajeService> logger,
        IVehicleRepository? vehicleRepository = null,
        IImpactDetectionEngine? impactDetectionEngine = null,
        IImpactAlertOrchestrator? impactAlertOrchestrator = null)
    {
        _viajeRepository = viajeRepository;
        _logger = logger;
        _vehicleRepository = vehicleRepository;
        _impactDetectionEngine = impactDetectionEngine;
        _impactAlertOrchestrator = impactAlertOrchestrator;
    }

    public async Task<ViajeDto> StartAsync(Guid usuarioId, StartTripRequest request)
    {
        var active = await _viajeRepository.GetActiveByUserAsync(usuarioId);
        if (active is not null)
            throw new ConflictException("Ya tienes un viaje activo. Finalízalo antes de iniciar uno nuevo.");

        var vehiclePublicId = await ResolveVehiclePublicIdAsync(usuarioId, request.VehiclePublicId);

        // Invariante de producto: el ciclo de vida del viaje es controlado
        // exclusivamente por el wearable. Los campos legacy se conservan solo
        // para leer documentos históricos, no para crear nuevos viajes.
        var viaje = new Viaje
        {
            UsuarioId = usuarioId,
            DispositivoId = request.DispositivoId,
            VehiclePublicId = vehiclePublicId,
            ControlClient = ClientTypePolicy.Wearable,
            MobileFallbackUsed = false,
            FallbackReason = null,
            Estado = "Activo",
            Inicio = DateTime.UtcNow,
            Proposito = request.Proposito,
            RutaOrigen = request.RutaOrigen,
            RutaDestino = request.RutaDestino,
        };

        await _viajeRepository.AddAsync(viaje);

        _logger.LogInformation(
            "Viaje {ViajeId} iniciado para usuario {UsuarioId}",
            viaje.Id, usuarioId);

        return MapToDto(viaje);
    }

    public async Task<TripActionResponse> PauseAsync(Guid usuarioId, Guid viajeId)
    {
        var viaje = await GetOwnedViajeAsync(usuarioId, viajeId);

        if (viaje.Estado != "Activo")
            throw new ConflictException("Solo se puede pausar un viaje activo.");

        viaje.Estado = "Pausado";
        await _viajeRepository.UpdateAsync(viaje);

        return new TripActionResponse { ViajeId = viajeId, Estado = "Pausado", Mensaje = "Viaje pausado." };
    }

    public async Task<TripActionResponse> ResumeAsync(Guid usuarioId, Guid viajeId)
    {
        var viaje = await GetOwnedViajeAsync(usuarioId, viajeId);

        if (viaje.Estado != "Pausado")
            throw new ConflictException("Solo se puede reanudar un viaje pausado.");

        viaje.Estado = "Activo";
        await _viajeRepository.UpdateAsync(viaje);

        return new TripActionResponse { ViajeId = viajeId, Estado = "Activo", Mensaje = "Viaje reanudado." };
    }

    public async Task<ViajeDto> FinishAsync(Guid usuarioId, Guid viajeId)
    {
        var viaje = await GetOwnedViajeAsync(usuarioId, viajeId);

        if (viaje.Estado == "Finalizado")
            throw new ConflictException("Este viaje ya fue finalizado.");

        viaje.Estado = "Finalizado";
        viaje.Fin = DateTime.UtcNow;

        if (viaje.Inicio.Kind == DateTimeKind.Utc)
            viaje.DuracionMinutos = (int)(viaje.Fin.Value - viaje.Inicio).TotalMinutes;

        var telemetry = await _viajeRepository.GetTelemetryByViajeAsync(viajeId);
        if (telemetry.Count > 0)
        {
            viaje.DistanciaRecorridaKm = CalculateDistance(telemetry);
            viaje.VelocidadPromedio = telemetry.Average(t => t.Velocidad);
            viaje.VelocidadMaxima = telemetry.Max(t => t.Velocidad);
        }

        await _viajeRepository.UpdateAsync(viaje);

        _logger.LogInformation("Viaje {ViajeId} finalizado para usuario {UsuarioId}", viajeId, usuarioId);

        return MapToDto(viaje);
    }

    public async Task<List<TelemetryPointDto>> UpdateTelemetryAsync(Guid usuarioId, Guid viajeId, TelemetryUpdateRequest request)
    {
        var viaje = await GetOwnedViajeAsync(usuarioId, viajeId);

        if (viaje.Estado != "Activo" && viaje.Estado != "Pausado")
            throw new ConflictException("Solo se puede enviar telemetría de un viaje activo o pausado.");

        foreach (var punto in request.Puntos)
        {
            var telemetry = new ViajeTelemetry
            {
                ViajeId = viajeId,
                UsuarioId = usuarioId,
                Timestamp = punto.Timestamp,
                Lat = punto.Lat,
                Lng = punto.Lng,
                Velocidad = punto.Velocidad,
                Altitud = punto.Altitud,
                Heading = punto.Heading,
                SchemaVersion = punto.SchemaVersion,
                SequenceNumber = punto.SequenceNumber,
                CapturedOffline = punto.CapturedOffline,
                WearableDeviceId = TelemetryCanonicalizer.NormalizeOptionalText(punto.WearableDeviceId),
                WearableModel = TelemetryCanonicalizer.NormalizeOptionalText(punto.WearableModel),
                VehiclePublicId = viaje.VehiclePublicId,
                BatteryLevel = punto.BatteryLevel,
                GpsAccuracyMeters = punto.GpsAccuracyMeters,
                AceleracionX = punto.AceleracionX,
                AceleracionY = punto.AceleracionY,
                AceleracionZ = punto.AceleracionZ,
                MagnitudAceleracion = TelemetryCanonicalizer.ResolveMagnitude(
                    punto.AceleracionX, punto.AceleracionY, punto.AceleracionZ, punto.MagnitudAceleracion),
                GiroscopioX = punto.GiroscopioX,
                GiroscopioY = punto.GiroscopioY,
                GiroscopioZ = punto.GiroscopioZ,
                MagnitudGiroscopio = TelemetryCanonicalizer.ResolveMagnitude(
                    punto.GiroscopioX, punto.GiroscopioY, punto.GiroscopioZ, punto.MagnitudGiroscopio),
                Desaceleracion = punto.Desaceleracion,
                FrecuenciaCardiaca = punto.FrecuenciaCardiaca,
                HrvMilisegundos = punto.HrvMilisegundos,
                Spo2Porcentaje = punto.Spo2Porcentaje,
                Pitch = punto.Pitch,
                Roll = punto.Roll,
                Yaw = punto.Yaw,
                CalidadSensor = TelemetrySchema.NormalizeQuality(punto.CalidadSensor),
                SensorFlagsCsv = TelemetryCanonicalizer.NormalizeSensorFlags(punto.SensorFlags),
            };
            await _viajeRepository.AddTelemetryAsync(telemetry);
        }

        return request.Puntos;
    }

    public async Task<TelemetryIngestionResultDto> IngestTelemetryAsync(Guid usuarioId, Guid viajeId, TelemetryBatchRequest request, CancellationToken cancellationToken = default)
    {
        TelemetryBatchValidator.Validate(request);

        // Validación de propiedad con point-read por partición: un viaje ajeno
        // es indistinguible de uno inexistente (404 seguro, sin filtrar propiedad).
        var viaje = await GetOwnedViajeAsync(usuarioId, viajeId);

        // Mismas reglas de estado que la ingesta legacy: solo activo o pausado.
        if (viaje.Estado != "Activo" && viaje.Estado != "Pausado")
            throw new ConflictException("Solo se puede enviar telemetría de un viaje activo o pausado.");

        // Idempotencia por EventId: point-read por (viajeId, eventId). Los
        // eventos nuevos se escriben en un solo lote atómico; los duplicados
        // idénticos se omiten; un EventId con contenido diferente es 409.
        var nuevos = new List<ViajeTelemetry>(request.Eventos.Count);
        var duplicados = 0;

        foreach (var evento in request.Eventos)
        {
            var existente = await _viajeRepository.GetTelemetryByEventIdAsync(viajeId, evento.EventId, cancellationToken);

            if (existente is null)
            {
                var telemetry = new ViajeTelemetry
                {
                    Id = evento.EventId,
                    ViajeId = viajeId,
                    UsuarioId = usuarioId,
                    Timestamp = evento.Timestamp,
                    Lat = evento.Lat,
                    Lng = evento.Lng,
                    Velocidad = evento.Velocidad,
                    Altitud = evento.Altitud,
                    Heading = evento.Heading,
                    SchemaVersion = request.SchemaVersion,
                    BatchId = request.BatchId,
                    BatchSequence = request.BatchSequence,
                    SequenceNumber = evento.SequenceNumber,
                    CapturedOffline = request.CapturedOffline,
                    WearableDeviceId = TelemetryCanonicalizer.NormalizeOptionalText(request.WearableDeviceId),
                    WearableModel = TelemetryCanonicalizer.NormalizeOptionalText(request.WearableModel),
                    WearableAppVersion = TelemetryCanonicalizer.NormalizeOptionalText(request.WearableAppVersion),
                    WearableOsVersion = TelemetryCanonicalizer.NormalizeOptionalText(request.WearableOsVersion),
                    WearableFirmwareVersion = TelemetryCanonicalizer.NormalizeOptionalText(request.WearableFirmwareVersion),
                    VehiclePublicId = viaje.VehiclePublicId,
                    BatteryLevel = request.BatteryLevel,
                    ClockOffsetMilliseconds = request.ClockOffsetMilliseconds,
                    GpsAccuracyMeters = evento.GpsAccuracyMeters,
                    AceleracionX = evento.AceleracionX,
                    AceleracionY = evento.AceleracionY,
                    AceleracionZ = evento.AceleracionZ,
                    MagnitudAceleracion = TelemetryCanonicalizer.ResolveAccelerationMagnitude(evento),
                    GiroscopioX = evento.GiroscopioX,
                    GiroscopioY = evento.GiroscopioY,
                    GiroscopioZ = evento.GiroscopioZ,
                    MagnitudGiroscopio = TelemetryCanonicalizer.ResolveGyroscopeMagnitude(evento),
                    Desaceleracion = evento.Desaceleracion,
                    FrecuenciaCardiaca = evento.FrecuenciaCardiaca,
                    HrvMilisegundos = evento.HrvMilisegundos,
                    Spo2Porcentaje = evento.Spo2Porcentaje,
                    Pitch = evento.Pitch,
                    Roll = evento.Roll,
                    Yaw = evento.Yaw,
                    CalidadSensor = TelemetrySchema.NormalizeQuality(evento.CalidadSensor),
                    SensorFlagsCsv = TelemetryCanonicalizer.NormalizeSensorFlags(evento.SensorFlags),
                    RecibidoEn = DateTime.UtcNow,
                };

                ApplyServerDetection(telemetry);
                nuevos.Add(telemetry);
            }
            else if (TelemetryEventEquality.IsIdentical(existente, evento))
            {
                duplicados++;
            }
            else
            {
                throw new ConflictException("El evento ya existe con contenido diferente.");
            }
        }

        // Sin eventos nuevos: no se crea ningún batch (todo duplicado
        // idéntico); un batch fallido nunca cuenta inserciones.
        var escritura = nuevos.Count == 0
            ? new TelemetryBatchWriteResult()
            : await _viajeRepository.AddTelemetryBatchAsync(viajeId, nuevos, cancellationToken);

        if (escritura.Insertados > 0 && _impactAlertOrchestrator is not null)
        {
            await _impactAlertOrchestrator.ProcessDetectedEventsAsync(
                usuarioId,
                viaje,
                nuevos,
                cancellationToken);
        }

        // El timestamp del evento es el del cliente (UTC); la recepción del
        // servidor vive separada en ViajeTelemetry.RecibidoEn.
        var sequences = request.Eventos
            .Where(evento => evento.SequenceNumber is not null)
            .Select(evento => evento.SequenceNumber!.Value)
            .ToList();

        var resultado = new TelemetryIngestionResultDto
        {
            ViajeId = viajeId,
            BatchId = request.BatchId,
            SchemaVersion = request.SchemaVersion,
            CapturedOffline = request.CapturedOffline,
            Recibidos = request.Eventos.Count,
            Insertados = escritura.Insertados,
            Duplicados = duplicados + escritura.Duplicados,
            PrimerEventoUtc = request.Eventos.Min(e => e.Timestamp),
            UltimoEventoUtc = request.Eventos.Max(e => e.Timestamp),
            PrimeraSecuencia = sequences.Count == 0 ? null : sequences.Min(),
            UltimaSecuencia = sequences.Count == 0 ? null : sequences.Max(),
            ProcesadoEnUtc = DateTime.UtcNow,
        };

        // Logs únicamente con conteos: sin EventId, sin GPS, sin payload.
        // El correlation id se adjunta automáticamente vía logging scope.
        _logger.LogInformation(
            "Lote de telemetría procesado: {Recibidos} recibidos, {Insertados} insertados, {Duplicados} duplicados",
            resultado.Recibidos, resultado.Insertados, resultado.Duplicados);

        return resultado;
    }

    public async Task<ViajeDto?> GetActiveAsync(Guid usuarioId)
    {
        var viaje = await _viajeRepository.GetActiveByUserAsync(usuarioId);
        return viaje is null ? null : MapToDto(viaje);
    }

    public async Task<PagedResult<ViajeDto>> GetTripsPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken)
    {
        var size = PaginationValidator.Resolve(pageSize, continuationToken);
        var page = await _viajeRepository.GetByUserPagedAsync(usuarioId, size, continuationToken);
        return new PagedResult<ViajeDto>
        {
            Items = page.Items.Select(MapToDto).ToList(),
            ContinuationToken = page.ContinuationToken,
            HasMoreResults = page.HasMoreResults,
            PageSize = page.PageSize,
        };
    }

    public async Task<PagedResult<TelemetryPointDto>> GetTelemetryPagedAsync(Guid usuarioId, Guid viajeId, int? pageSize, string? continuationToken)
    {
        var size = PaginationValidator.Resolve(pageSize, continuationToken);

        // Validación de propiedad antes de consultar: un usuario no puede
        // obtener telemetría de un viaje ajeno (point-read por partición).
        var viaje = await _viajeRepository.GetByIdAsync(usuarioId, viajeId)
            ?? throw new NotFoundException("Viaje no encontrado.");

        if (viaje.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para acceder a este viaje.");

        var page = await _viajeRepository.GetTelemetryByViajePagedAsync(viajeId, size, continuationToken);
        return new PagedResult<TelemetryPointDto>
        {
            Items = page.Items.Select(MapToTelemetryPointDto).ToList(),
            ContinuationToken = page.ContinuationToken,
            HasMoreResults = page.HasMoreResults,
            PageSize = page.PageSize,
        };
    }

    private async Task<Viaje> GetOwnedViajeAsync(Guid usuarioId, Guid viajeId)
    {
        var viaje = await _viajeRepository.GetByIdAsync(usuarioId, viajeId)
            ?? throw new NotFoundException("Viaje no encontrado.");

        if (viaje.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para acceder a este viaje.");

        return viaje;
    }

    private async Task<string?> ResolveVehiclePublicIdAsync(Guid userId, string? requestedPublicVehicleId)
    {
        if (_vehicleRepository is null)
        {
            return string.IsNullOrWhiteSpace(requestedPublicVehicleId)
                ? null
                : requestedPublicVehicleId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(requestedPublicVehicleId))
        {
            var requested = await _vehicleRepository.GetByPublicIdAsync(
                userId,
                requestedPublicVehicleId.Trim());
            return requested?.PublicVehicleId
                ?? throw new NotFoundException("Vehículo no encontrado.");
        }

        var vehicles = await _vehicleRepository.GetAllByOwnerAsync(userId);
        return vehicles.FirstOrDefault(vehicle => vehicle.EsPrincipal)?.PublicVehicleId
            ?? vehicles.FirstOrDefault()?.PublicVehicleId;
    }

    private static double CalculateDistance(List<ViajeTelemetry> points)
    {
        double totalKm = 0;
        for (int i = 1; i < points.Count; i++)
        {
            totalKm += Haversine(points[i - 1].Lat, points[i - 1].Lng, points[i].Lat, points[i].Lng);
        }
        return Math.Round(totalKm, 2);
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371;
        var dLat = (lat2 - lat1) * Math.PI / 180;
        var dLon = (lon2 - lon1) * Math.PI / 180;
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
    }

    private void ApplyServerDetection(ViajeTelemetry telemetry)
    {
        if (_impactDetectionEngine is null)
            return;

        var decision = _impactDetectionEngine.Evaluate(telemetry);
        telemetry.ImpactCandidate = decision.IsCandidate;
        telemetry.DetectionLabel = decision.DetectionLabel;
        telemetry.SeverityLabel = decision.SeverityLabel;
        telemetry.RuleVersion = decision.RuleVersion;
        telemetry.DetectionScore = decision.Score;
        telemetry.ModelVersion = null;
        telemetry.LabeledAtUtc = DateTime.UtcNow;
    }

    private static ViajeDto MapToDto(Viaje v) => new()
    {
        Id = v.Id,
        DispositivoId = v.DispositivoId,
        VehiclePublicId = v.VehiclePublicId,
        ControlClient = v.ControlClient,
        MobileFallbackUsed = v.MobileFallbackUsed,
        FallbackReason = v.FallbackReason,
        Estado = v.Estado,
        Inicio = v.Inicio,
        Fin = v.Fin,
        DistanciaRecorridaKm = v.DistanciaRecorridaKm,
        DuracionMinutos = v.DuracionMinutos,
        VelocidadPromedio = v.VelocidadPromedio,
        VelocidadMaxima = v.VelocidadMaxima,
        RiesgoMaximo = v.RiesgoMaximo,
        Proposito = v.Proposito,
        RutaOrigen = v.RutaOrigen,
        RutaDestino = v.RutaDestino,
    };

    private static TelemetryPointDto MapToTelemetryPointDto(ViajeTelemetry t) => new()
    {
        Lat = t.Lat,
        Lng = t.Lng,
        Velocidad = t.Velocidad,
        Altitud = t.Altitud,
        Heading = t.Heading,
        Timestamp = t.Timestamp,
        SchemaVersion = t.SchemaVersion,
        SequenceNumber = t.SequenceNumber,
        CapturedOffline = t.CapturedOffline,
        WearableDeviceId = t.WearableDeviceId,
        WearableModel = t.WearableModel,
        VehiclePublicId = t.VehiclePublicId,
        BatteryLevel = t.BatteryLevel,
        GpsAccuracyMeters = t.GpsAccuracyMeters,
        AceleracionX = t.AceleracionX,
        AceleracionY = t.AceleracionY,
        AceleracionZ = t.AceleracionZ,
        MagnitudAceleracion = t.MagnitudAceleracion,
        GiroscopioX = t.GiroscopioX,
        GiroscopioY = t.GiroscopioY,
        GiroscopioZ = t.GiroscopioZ,
        MagnitudGiroscopio = t.MagnitudGiroscopio,
        Desaceleracion = t.Desaceleracion,
        FrecuenciaCardiaca = t.FrecuenciaCardiaca,
        HrvMilisegundos = t.HrvMilisegundos,
        Spo2Porcentaje = t.Spo2Porcentaje,
        Pitch = t.Pitch,
        Roll = t.Roll,
        Yaw = t.Yaw,
        CalidadSensor = t.CalidadSensor,
        SensorFlags = TelemetryCanonicalizer.ParseSensorFlags(t.SensorFlagsCsv),
        ImpactCandidate = t.ImpactCandidate,
        DetectionLabel = t.DetectionLabel,
        SeverityLabel = t.SeverityLabel,
        RuleVersion = t.RuleVersion,
        DetectionScore = t.DetectionScore,
        ModelVersion = t.ModelVersion,
    };
}
