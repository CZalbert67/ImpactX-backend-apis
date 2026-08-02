using System.Text;
using System.Text.Json;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Telemetry;
using ImpactX.Models.DTOs;

namespace ImpactX.Tests.Unit;

public class TelemetryBatchValidatorTests
{
    private static TelemetryEventRequest ValidEvent(Guid? eventId = null) => new()
    {
        EventId = eventId ?? Guid.NewGuid(),
        Timestamp = DateTime.UtcNow,
        Lat = 19.43,
        Lng = -99.13,
        Velocidad = 50,
    };

    private static TelemetryBatchRequest ValidBatch(int count) => new()
    {
        Eventos = Enumerable.Range(0, count).Select(_ => ValidEvent()).ToList(),
    };

    private static void AssertBadRequest(Action validate) =>
        Assert.Throws<BadRequestException>(validate);

    [Fact]
    public void Validate_SingleValidEvent_Passes()
    {
        TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [ValidEvent()] });
    }

    [Fact]
    public void Validate_NullEvents_Throws()
    {
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = null! }));
    }

    [Fact]
    public void Validate_EmptyBatch_Throws()
    {
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest()));
    }

    [Fact]
    public void Validate_101Events_Throws()
    {
        AssertBadRequest(() => TelemetryBatchValidator.Validate(ValidBatch(TelemetryIngestionLimits.MaxEventsPerBatch + 1)));
    }

    [Fact]
    public void Validate_100Events_Passes()
    {
        TelemetryBatchValidator.Validate(ValidBatch(TelemetryIngestionLimits.MaxEventsPerBatch));
    }

    [Fact]
    public void Validate_EmptyEventId_Throws()
    {
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest
        {
            Eventos = [ValidEvent(Guid.Empty)]
        }));
    }

    [Fact]
    public void Validate_DuplicateEventIdWithinBatch_Throws()
    {
        var eventId = Guid.NewGuid();
        var evento = ValidEvent(eventId);
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest
        {
            Eventos = [evento, ValidEvent(eventId)]
        }));
    }

    [Fact]
    public void Validate_NonUtcTimestamp_Throws()
    {
        var evento = ValidEvent();
        evento.Timestamp = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] }));
    }

    [Fact]
    public void Validate_DefaultTimestamp_Throws()
    {
        var evento = ValidEvent();
        evento.Timestamp = default;
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] }));
    }

    [Fact]
    public void Validate_FutureBeyondTolerance_Throws()
    {
        var evento = ValidEvent();
        evento.Timestamp = DateTime.UtcNow.AddMinutes(6);
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] }));
    }

    [Fact]
    public void Validate_FutureWithinTolerance_Passes()
    {
        var evento = ValidEvent();
        evento.Timestamp = DateTime.UtcNow.AddMinutes(4);
        TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] });
    }

    [Fact]
    public void Validate_LateEvent_Passes()
    {
        var evento = ValidEvent();
        evento.Timestamp = DateTime.UtcNow.AddMinutes(-30);
        TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] });
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Validate_LatNotFinite_Throws(double value)
    {
        var evento = ValidEvent();
        evento.Lat = value;
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] }));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_LngNotFinite_Throws(double value)
    {
        var evento = ValidEvent();
        evento.Lng = value;
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] }));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Validate_VelocidadNotFinite_Throws(double value)
    {
        var evento = ValidEvent();
        evento.Velocidad = value;
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] }));
    }

    [Theory]
    [InlineData(-90.0001)]
    [InlineData(90.0001)]
    public void Validate_LatOutOfRange_Throws(double lat)
    {
        var evento = ValidEvent();
        evento.Lat = lat;
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] }));
    }

    [Theory]
    [InlineData(-180.0001)]
    [InlineData(180.0001)]
    public void Validate_LngOutOfRange_Throws(double lng)
    {
        var evento = ValidEvent();
        evento.Lng = lng;
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] }));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(500.01)]
    public void Validate_VelocidadOutOfRange_Throws(double velocidad)
    {
        var evento = ValidEvent();
        evento.Velocidad = velocidad;
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] }));
    }

    [Theory]
    [InlineData(-500.01)]
    [InlineData(10000.01)]
    public void Validate_AltitudOutOfRange_Throws(double altitud)
    {
        var evento = ValidEvent();
        evento.Altitud = altitud;
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] }));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(360)]
    public void Validate_HeadingOutOfRange_Throws(double heading)
    {
        var evento = ValidEvent();
        evento.Heading = heading;
        AssertBadRequest(() => TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] }));
    }

    [Fact]
    public void Validate_NullOptionalSensors_Passes()
    {
        var evento = ValidEvent();
        evento.Altitud = null;
        evento.Heading = null;
        TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = [evento] });
    }

    [Fact]
    public void MaxValidBatch_SerializedSize_FitsWithinBodyLimit()
    {
        // Cálculo del tamaño del payload máximo válido (100 eventos con todos
        // los campos): cada evento serializa ≈ 190 bytes
        // ({"eventId":"<36>","timestamp":"<27>","lat":..,"lng":..,"velocidad":..,
        // "altitud":..,"heading":..}) → ≈ 19 KB + wrapper "{"eventos":[...]}".
        // 32 KB de TelemetryIngestionLimits.MaxBodyBytes dejan margen para
        // cabeceras/whitespace del cliente.
        var eventos = Enumerable.Range(0, TelemetryIngestionLimits.MaxEventsPerBatch)
            .Select(i => new TelemetryEventRequest
            {
                EventId = Guid.NewGuid(),
                Timestamp = DateTime.UtcNow,
                Lat = 89.999999,
                Lng = -179.999999,
                Velocidad = TelemetryIngestionLimits.MaxSpeedKmh,
                Altitud = TelemetryIngestionLimits.MaxAltitudeMeters,
                Heading = 359.999,
            })
            .ToList();

        TelemetryBatchValidator.Validate(new TelemetryBatchRequest { Eventos = eventos });

        // Mismas opciones que la API (camelCase + converter por atributo).
        var json = JsonSerializer.Serialize(new TelemetryBatchRequest { Eventos = eventos },
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var bytes = Encoding.UTF8.GetByteCount(json);

        Assert.True(bytes <= TelemetryIngestionLimits.MaxBodyBytes,
            $"Lote máximo válido ({bytes} bytes) supera MaxBodyBytes ({TelemetryIngestionLimits.MaxBodyBytes}).");
    }
}
