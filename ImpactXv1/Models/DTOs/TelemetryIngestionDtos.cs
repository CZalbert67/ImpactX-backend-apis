using System.Text.Json.Serialization;
using ImpactX.Converters;

namespace ImpactX.Models.DTOs;

/// <summary>
/// Lote de eventos de telemetría enviado por la app móvil o el wearable.
/// Mínimo 1 evento y máximo 100 por petición.
/// </summary>
public class TelemetryBatchRequest
{
    public List<TelemetryEventRequest> Eventos { get; set; } = [];
}

/// <summary>
/// Evento individual de telemetría. EventId es generado por el cliente y se
/// convierte en el identificador persistido del documento (point-read con
/// PartitionKey del viaje); reenviar el mismo EventId con el mismo contenido
/// es seguro (no se duplica).
/// </summary>
public class TelemetryEventRequest
{
    public Guid EventId { get; set; }

    /// <summary>Cuándo ocurrió el evento según el cliente. Debe ser UTC explícito.</summary>
    [JsonConverter(typeof(UtcTimestampJsonConverter))]
    public DateTime Timestamp { get; set; }

    public double Lat { get; set; }
    public double Lng { get; set; }
    public double Velocidad { get; set; }
    public double? Altitud { get; set; }
    public double? Heading { get; set; }
}

/// <summary>
/// Resultado de la ingesta de un lote. Solo contiene conteos y rangos
/// temporales; nunca expone documentos internos ni identificadores de evento.
/// </summary>
public class TelemetryIngestionResultDto
{
    public Guid ViajeId { get; set; }
    public int Recibidos { get; set; }
    public int Insertados { get; set; }
    public int Duplicados { get; set; }
    public DateTime PrimerEventoUtc { get; set; }
    public DateTime UltimoEventoUtc { get; set; }
}
