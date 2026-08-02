using ImpactX.Core.Domain;
using ImpactX.Core.Telemetry;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Unit;

public class TelemetryEventEqualityTests
{
    private static readonly DateTime Timestamp = new(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
    private static readonly Guid ViajeId = Guid.NewGuid();

    private static ViajeTelemetry Persistido(double lat = 19.43, double lng = -99.13, double velocidad = 50,
        double? altitud = 120.5, double? heading = 90.0) => new()
    {
        ViajeId = ViajeId,
        Timestamp = Timestamp,
        Lat = lat,
        Lng = lng,
        Velocidad = velocidad,
        Altitud = altitud,
        Heading = heading,
    };

    private static TelemetryEventRequest Recibido(double lat = 19.43, double lng = -99.13, double velocidad = 50,
        double? altitud = 120.5, double? heading = 90.0) => new()
    {
        EventId = Guid.NewGuid(),
        Timestamp = Timestamp,
        Lat = lat,
        Lng = lng,
        Velocidad = velocidad,
        Altitud = altitud,
        Heading = heading,
    };

    [Fact]
    public void IsIdentical_SameExactValues_ReturnsTrue()
    {
        Assert.True(TelemetryEventEquality.IsIdentical(Persistido(), Recibido()));
        Assert.True(TelemetryEventEquality.IsIdentical(Persistido(), Persistido()));
    }

    [Theory]
    [InlineData(20.0, -99.13, 50, 120.5, 90.0)]
    [InlineData(19.43, -98.0, 50, 120.5, 90.0)]
    [InlineData(19.43, -99.13, 60, 120.5, 90.0)]
    [InlineData(19.43, -99.13, 50, 130.0, 90.0)]
    [InlineData(19.43, -99.13, 50, 120.5, 91.0)]
    public void IsIdentical_AnySensorFieldChange_ReturnsFalse(double lat, double lng, double velocidad,
        double? altitud, double? heading)
    {
        Assert.False(TelemetryEventEquality.IsIdentical(Persistido(), Recibido(lat, lng, velocidad, altitud, heading)));
        Assert.False(TelemetryEventEquality.IsIdentical(Persistido(), Persistido(lat, lng, velocidad, altitud, heading)));
    }

    [Fact]
    public void IsIdentical_NullVsNull_ReturnsTrue()
    {
        Assert.True(TelemetryEventEquality.IsIdentical(
            Persistido(altitud: null, heading: null), Recibido(altitud: null, heading: null)));
        Assert.True(TelemetryEventEquality.IsIdentical(
            Persistido(altitud: null, heading: null), Persistido(altitud: null, heading: null)));
    }

    [Fact]
    public void IsIdentical_NullVsValue_ReturnsFalse()
    {
        Assert.False(TelemetryEventEquality.IsIdentical(Persistido(altitud: null), Recibido()));
        Assert.False(TelemetryEventEquality.IsIdentical(Persistido(), Recibido(altitud: null)));
        Assert.False(TelemetryEventEquality.IsIdentical(Persistido(), Recibido(heading: null)));
        Assert.False(TelemetryEventEquality.IsIdentical(Persistido(heading: null), Persistido()));
    }
}
