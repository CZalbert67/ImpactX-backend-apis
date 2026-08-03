using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImpactX.Converters;

/// <summary>
/// Convierte timestamps del contrato de ingesta de telemetría exigiendo un
/// indicador UTC explícito ('Z' o '+00:00'). Rechaza timestamps sin zona
/// horaria, con offset distinto de cero o malformados (400 en el binding).
/// El valor resultante siempre tiene <see cref="DateTimeKind.Utc"/>.
/// El mensaje de error es seguro: no expone internos del servidor.
/// </summary>
public sealed class UtcTimestampJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("El timestamp debe ser una cadena ISO 8601 con indicador UTC.");

        var raw = reader.GetString();
        if (raw is null || !HasExplicitUtcDesignator(raw))
            throw new JsonException("El timestamp debe estar en formato UTC (sufijo 'Z' o '+00:00').");

        if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            throw new JsonException("El timestamp no es una fecha válida.");
        }

        return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture));
    }

    private static bool HasExplicitUtcDesignator(string raw)
        => raw.EndsWith("Z", StringComparison.OrdinalIgnoreCase)
           || raw.EndsWith("+00:00", StringComparison.Ordinal);
}
