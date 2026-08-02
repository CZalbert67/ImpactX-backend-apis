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
           && ExactEquals(persistido.Lat, recibido.Lat)
           && ExactEquals(persistido.Lng, recibido.Lng)
           && ExactEquals(persistido.Velocidad, recibido.Velocidad)
           && ExactEquals(persistido.Altitud, recibido.Altitud)
           && ExactEquals(persistido.Heading, recibido.Heading);

    public static bool IsIdentical(ViajeTelemetry a, ViajeTelemetry b)
        => a.ViajeId == b.ViajeId
           && a.Timestamp == b.Timestamp
           && ExactEquals(a.Lat, b.Lat)
           && ExactEquals(a.Lng, b.Lng)
           && ExactEquals(a.Velocidad, b.Velocidad)
           && ExactEquals(a.Altitud, b.Altitud)
           && ExactEquals(a.Heading, b.Heading);

    // La comparación exacta de punto flotante es intencional: determina la
    // idempotencia por EventId; ninguna tolerancia debe convertir contenido
    // diferente en idéntico.
    private static bool ExactEquals(double left, double right)
        => left.Equals(right);

    private static bool ExactEquals(double? left, double? right)
        => Nullable.Equals(left, right);
}
