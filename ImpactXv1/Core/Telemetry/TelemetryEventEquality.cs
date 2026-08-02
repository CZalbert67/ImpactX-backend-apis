using ImpactX.Core.Domain;
using ImpactX.Models.DTOs;

namespace ImpactX.Core.Telemetry;

/// <summary>
/// Igualdad idempotente entre un evento persistido y uno recibido.
/// Compara únicamente los campos del evento (viaje, timestamp y sensores);
/// nunca incluye fechas de recepción del servidor (RecibidoEn) ni campos
/// calculados. Dos eventos con el mismo EventId y el mismo contenido son el
/// mismo evento: reenviarlo es seguro y no se duplica.
/// </summary>
public static class TelemetryEventEquality
{
    public static bool IsIdentical(ViajeTelemetry persistido, TelemetryEventRequest recibido)
        => persistido.Timestamp == recibido.Timestamp
           && persistido.Lat == recibido.Lat
           && persistido.Lng == recibido.Lng
           && persistido.Velocidad == recibido.Velocidad
           && persistido.Altitud == recibido.Altitud
           && persistido.Heading == recibido.Heading;

    public static bool IsIdentical(ViajeTelemetry a, ViajeTelemetry b)
        => a.ViajeId == b.ViajeId
           && a.Timestamp == b.Timestamp
           && a.Lat == b.Lat
           && a.Lng == b.Lng
           && a.Velocidad == b.Velocidad
           && a.Altitud == b.Altitud
           && a.Heading == b.Heading;
}
