using ImpactX.Core.Exceptions;
using ImpactX.Models.DTOs;

namespace ImpactX.Core.Telemetry;

/// <summary>
/// Validación centralizada del contrato de ingesta de telemetría por lotes.
/// Reglas:
/// - Lote de 1 a 100 eventos (fuera de rango → 400).
/// - EventId obligatorio (GUID), único dentro del lote, nunca vacío.
/// - Timestamp obligatorio en UTC (lo garantiza UtcTimestampJsonConverter),
///   con tolerancia máxima de 5 minutos hacia el futuro.
/// - Valores numéricos finitos (sin NaN/Infinity) y dentro de los rangos del
///   dominio (latitud, longitud, velocidad, altitud y rumbo).
/// - Nunca se modifican valores inválidos: se rechaza el lote completo.
/// </summary>
public static class TelemetryBatchValidator
{
    public static void Validate(TelemetryBatchRequest request)
    {
        if (request.Eventos is null || request.Eventos.Count < TelemetryIngestionLimits.MinEventsPerBatch)
            throw new BadRequestException($"El lote debe contener al menos {TelemetryIngestionLimits.MinEventsPerBatch} evento.");

        if (request.Eventos.Count > TelemetryIngestionLimits.MaxEventsPerBatch)
            throw new BadRequestException($"El lote no puede superar los {TelemetryIngestionLimits.MaxEventsPerBatch} eventos.");

        var vistos = new HashSet<Guid>(request.Eventos.Count);
        foreach (var evento in request.Eventos)
        {
            if (evento.EventId == Guid.Empty)
                throw new BadRequestException("Cada evento debe incluir un EventId no vacío.");

            if (!vistos.Add(evento.EventId))
                throw new BadRequestException("El EventId no puede repetirse dentro del mismo lote.");

            ValidateEvento(evento);
        }
    }

    private static void ValidateEvento(TelemetryEventRequest evento)
    {
        if (evento.Timestamp == default || evento.Timestamp.Kind != DateTimeKind.Utc)
            throw new BadRequestException("El timestamp de cada evento debe estar en UTC.");

        if (evento.Timestamp > DateTime.UtcNow.Add(TelemetryIngestionLimits.MaxFutureTolerance))
            throw new BadRequestException("El timestamp de un evento no puede estar más de 5 minutos en el futuro.");

        RequireFinite(evento.Lat, "latitud");
        RequireFinite(evento.Lng, "longitud");
        RequireFinite(evento.Velocidad, "velocidad");

        if (evento.Lat < TelemetryIngestionLimits.MinLatitude || evento.Lat > TelemetryIngestionLimits.MaxLatitude)
            throw new BadRequestException("La latitud debe estar entre -90 y 90 grados.");

        if (evento.Lng < TelemetryIngestionLimits.MinLongitude || evento.Lng > TelemetryIngestionLimits.MaxLongitude)
            throw new BadRequestException("La longitud debe estar entre -180 y 180 grados.");

        if (evento.Velocidad < TelemetryIngestionLimits.MinSpeedKmh || evento.Velocidad > TelemetryIngestionLimits.MaxSpeedKmh)
            throw new BadRequestException("La velocidad debe estar entre 0 y 500 km/h.");

        if (evento.Altitud is not null)
        {
            RequireFinite(evento.Altitud.Value, "altitud");
            if (evento.Altitud.Value < TelemetryIngestionLimits.MinAltitudeMeters ||
                evento.Altitud.Value > TelemetryIngestionLimits.MaxAltitudeMeters)
            {
                throw new BadRequestException("La altitud debe estar entre -500 y 10000 metros.");
            }
        }

        if (evento.Heading is not null)
        {
            RequireFinite(evento.Heading.Value, "rumbo");
            if (evento.Heading.Value < TelemetryIngestionLimits.MinHeadingDegrees ||
                evento.Heading.Value >= TelemetryIngestionLimits.MaxHeadingDegrees)
            {
                throw new BadRequestException("El rumbo debe estar entre 0 y 359 grados.");
            }
        }
    }

    private static void RequireFinite(double valor, string campo)
    {
        if (double.IsNaN(valor) || double.IsInfinity(valor))
            throw new BadRequestException($"El valor de {campo} no puede ser NaN o infinito.");
    }
}
