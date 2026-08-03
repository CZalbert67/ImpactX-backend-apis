namespace ImpactX.Core.Domain;

/// <summary>
/// Resultado de la escritura por lotes de telemetría (una sola transacción).
/// Solo contiene conteos; nunca expone identificadores ni contenido.
/// </summary>
public sealed class TelemetryBatchWriteResult
{
    public int Insertados { get; init; }
    public int Duplicados { get; init; }
}
