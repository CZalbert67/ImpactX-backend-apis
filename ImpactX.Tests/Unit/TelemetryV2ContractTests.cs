using System.Text;
using System.Text.Json;
using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Telemetry;
using ImpactX.Models.DTOs;
using ImpactX.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace ImpactX.Tests.Unit;

public class TelemetryV2ContractTests
{
    private static TelemetryEventRequest EnrichedEvent(long sequence = 1) => new()
    {
        EventId = Guid.NewGuid(),
        Timestamp = DateTime.UtcNow.AddSeconds(-5),
        SequenceNumber = sequence,
        Lat = 19.4326,
        Lng = -99.1332,
        Velocidad = 52.4,
        Altitud = 2240,
        Heading = 91,
        GpsAccuracyMeters = 4.5,
        AceleracionX = 1.2,
        AceleracionY = -0.4,
        AceleracionZ = 9.7,
        GiroscopioX = 0.1,
        GiroscopioY = -0.2,
        GiroscopioZ = 0.3,
        Desaceleracion = 2.4,
        FrecuenciaCardiaca = 88,
        HrvMilisegundos = 47,
        Spo2Porcentaje = 97,
        Pitch = 2,
        Roll = -3,
        Yaw = 91,
        CalidadSensor = "HIGH",
        SensorFlags = ["gps_degraded", "heart_rate_available"],
    };

    private static TelemetryBatchRequest EnrichedBatch(params TelemetryEventRequest[] events) => new()
    {
        SchemaVersion = TelemetrySchema.EnrichedVersion,
        BatchId = Guid.NewGuid(),
        BatchSequence = 7,
        CapturedOffline = true,
        WearableDeviceId = "GW8-UNIT-001",
        WearableModel = "Galaxy Watch 8",
        WearableAppVersion = "1.2.3",
        WearableOsVersion = "WearOS-test",
        WearableFirmwareVersion = "FW-test",
        BatteryLevel = 73,
        ClockOffsetMilliseconds = 125,
        Eventos = events.Length == 0 ? [EnrichedEvent()] : [.. events],
    };

    [Fact]
    public void Validate_EnrichedBatch_Passes()
    {
        TelemetryBatchValidator.Validate(EnrichedBatch());
    }

    [Theory]
    [InlineData("batchId")]
    [InlineData("batchSequence")]
    [InlineData("device")]
    [InlineData("model")]
    [InlineData("battery")]
    [InlineData("sequence")]
    [InlineData("gpsAccuracy")]
    [InlineData("acceleration")]
    [InlineData("gyroscope")]
    [InlineData("quality")]
    public void Validate_EnrichedBatch_MissingRequiredField_Throws(string field)
    {
        var batch = EnrichedBatch();
        var item = batch.Eventos[0];

        switch (field)
        {
            case "batchId": batch.BatchId = null; break;
            case "batchSequence": batch.BatchSequence = null; break;
            case "device": batch.WearableDeviceId = null; break;
            case "model": batch.WearableModel = "Other Watch"; break;
            case "battery": batch.BatteryLevel = null; break;
            case "sequence": item.SequenceNumber = null; break;
            case "gpsAccuracy": item.GpsAccuracyMeters = null; break;
            case "acceleration": item.AceleracionX = null; break;
            case "gyroscope": item.GiroscopioZ = null; break;
            case "quality": item.CalidadSensor = null; break;
        }

        Assert.Throws<BadRequestException>(() => TelemetryBatchValidator.Validate(batch));
    }

    [Theory]
    [InlineData("heartRate")]
    [InlineData("spo2")]
    [InlineData("acceleration")]
    [InlineData("gyroscope")]
    [InlineData("yaw")]
    [InlineData("gps")]
    public void Validate_EnrichedSensorOutOfRange_Throws(string field)
    {
        var batch = EnrichedBatch();
        var item = batch.Eventos[0];

        switch (field)
        {
            case "heartRate": item.FrecuenciaCardiaca = 500; break;
            case "spo2": item.Spo2Porcentaje = 120; break;
            case "acceleration": item.AceleracionX = 300; break;
            case "gyroscope": item.GiroscopioY = 60; break;
            case "yaw": item.Yaw = 360; break;
            case "gps": item.GpsAccuracyMeters = -1; break;
        }

        Assert.Throws<BadRequestException>(() => TelemetryBatchValidator.Validate(batch));
    }

    [Fact]
    public void Validate_SensorFlagWithCsvDelimiter_Throws()
    {
        var batch = EnrichedBatch();
        batch.Eventos[0].SensorFlags = ["gps,degraded"];

        Assert.Throws<BadRequestException>(() => TelemetryBatchValidator.Validate(batch));
    }

    [Fact]
    public void Validate_DuplicateSequenceWithinBatch_Throws()
    {
        var batch = EnrichedBatch(EnrichedEvent(10), EnrichedEvent(10));
        Assert.Throws<BadRequestException>(() => TelemetryBatchValidator.Validate(batch));
    }

    [Fact]
    public void Canonicalizer_WhenAxesExist_IgnoresInconsistentSuppliedMagnitude()
    {
        var request = EnrichedEvent();
        request.AceleracionX = 3;
        request.AceleracionY = 4;
        request.AceleracionZ = 0;
        request.MagnitudAceleracion = 999;

        Assert.Equal(5d, TelemetryCanonicalizer.ResolveAccelerationMagnitude(request));
    }

    [Fact]
    public void Equality_DerivedMagnitudesAndFlagOrder_AreCanonical()
    {
        var request = EnrichedEvent();
        request.MagnitudAceleracion = null;
        request.MagnitudGiroscopio = null;
        request.SensorFlags = ["heart_rate_available", "gps_degraded"];

        var persisted = new ViajeTelemetry
        {
            Timestamp = request.Timestamp,
            SequenceNumber = request.SequenceNumber,
            Lat = request.Lat,
            Lng = request.Lng,
            Velocidad = request.Velocidad,
            Altitud = request.Altitud,
            Heading = request.Heading,
            GpsAccuracyMeters = request.GpsAccuracyMeters,
            AceleracionX = request.AceleracionX,
            AceleracionY = request.AceleracionY,
            AceleracionZ = request.AceleracionZ,
            MagnitudAceleracion = TelemetryCanonicalizer.ResolveAccelerationMagnitude(request),
            GiroscopioX = request.GiroscopioX,
            GiroscopioY = request.GiroscopioY,
            GiroscopioZ = request.GiroscopioZ,
            MagnitudGiroscopio = TelemetryCanonicalizer.ResolveGyroscopeMagnitude(request),
            Desaceleracion = request.Desaceleracion,
            FrecuenciaCardiaca = request.FrecuenciaCardiaca,
            HrvMilisegundos = request.HrvMilisegundos,
            Spo2Porcentaje = request.Spo2Porcentaje,
            Pitch = request.Pitch,
            Roll = request.Roll,
            Yaw = request.Yaw,
            CalidadSensor = "high",
            SensorFlagsCsv = "gps_degraded,heart_rate_available",
        };

        Assert.True(TelemetryEventEquality.IsIdentical(persisted, request));
    }

    [Fact]
    public void Equality_ChangedEnrichedField_ReturnsFalse()
    {
        var request = EnrichedEvent();
        var persisted = new ViajeTelemetry
        {
            Timestamp = request.Timestamp,
            SequenceNumber = request.SequenceNumber,
            Lat = request.Lat,
            Lng = request.Lng,
            Velocidad = request.Velocidad,
            Altitud = request.Altitud,
            Heading = request.Heading,
            GpsAccuracyMeters = request.GpsAccuracyMeters,
            AceleracionX = request.AceleracionX,
            AceleracionY = request.AceleracionY,
            AceleracionZ = request.AceleracionZ,
            MagnitudAceleracion = TelemetryCanonicalizer.ResolveAccelerationMagnitude(request),
            GiroscopioX = request.GiroscopioX,
            GiroscopioY = request.GiroscopioY,
            GiroscopioZ = request.GiroscopioZ,
            MagnitudGiroscopio = TelemetryCanonicalizer.ResolveGyroscopeMagnitude(request),
            Desaceleracion = request.Desaceleracion,
            FrecuenciaCardiaca = request.FrecuenciaCardiaca,
            HrvMilisegundos = request.HrvMilisegundos,
            Spo2Porcentaje = request.Spo2Porcentaje,
            Pitch = request.Pitch,
            Roll = request.Roll,
            Yaw = request.Yaw,
            CalidadSensor = "high",
            SensorFlagsCsv = TelemetryCanonicalizer.NormalizeSensorFlags(request.SensorFlags),
        };

        request.GiroscopioZ = 0.8;
        Assert.False(TelemetryEventEquality.IsIdentical(persisted, request));
    }

    [Fact]
    public async Task IngestTelemetry_EnrichedBatch_PersistsProvenanceAndReturnsOfflineMetadata()
    {
        var userId = Guid.NewGuid();
        var trip = new Viaje
        {
            Id = Guid.NewGuid(),
            UsuarioId = userId,
            Estado = "Activo",
            DispositivoId = "GW8-UNIT-001",
            VehiclePublicId = "VEH-test-001",
        };
        var repository = new Mock<IViajeRepository>();
        repository.Setup(value => value.GetByIdAsync(userId, trip.Id)).ReturnsAsync(trip);
        repository.Setup(value => value.GetTelemetryByEventIdAsync(trip.Id, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ViajeTelemetry?)null);
        repository.Setup(value => value.AddTelemetryBatchAsync(trip.Id, It.IsAny<IReadOnlyList<ViajeTelemetry>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelemetryBatchWriteResult { Insertados = 1 });

        var service = new ViajeService(repository.Object, NullLogger<ViajeService>.Instance);
        var batch = EnrichedBatch();
        var result = await service.IngestTelemetryAsync(userId, trip.Id, batch);

        Assert.Equal(batch.BatchId, result.BatchId);
        Assert.Equal(TelemetrySchema.EnrichedVersion, result.SchemaVersion);
        Assert.True(result.CapturedOffline);
        Assert.Equal(1L, result.PrimeraSecuencia);
        Assert.Equal(1L, result.UltimaSecuencia);
        Assert.True(result.ProcesadoEnUtc.Kind == DateTimeKind.Utc);

        repository.Verify(value => value.AddTelemetryBatchAsync(
            trip.Id,
            It.Is<IReadOnlyList<ViajeTelemetry>>(items =>
                items.Count == 1
                && items[0].SchemaVersion == TelemetrySchema.EnrichedVersion
                && items[0].BatchId == batch.BatchId
                && items[0].WearableDeviceId == "GW8-UNIT-001"
                && items[0].WearableModel == "Galaxy Watch 8"
                && items[0].VehiclePublicId == "VEH-test-001"
                && items[0].SequenceNumber == 1
                && items[0].MagnitudAceleracion.HasValue
                && items[0].MagnitudAceleracion.GetValueOrDefault() > 0
                && items[0].MagnitudGiroscopio.HasValue
                && items[0].MagnitudGiroscopio.GetValueOrDefault() > 0
                && items[0].CalidadSensor == "high"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void MaxEnrichedBatch_SerializedSize_FitsConfiguredLimit()
    {
        var events = Enumerable.Range(0, TelemetryIngestionLimits.MaxEventsPerBatch)
            .Select(index => EnrichedEvent(index))
            .ToArray();
        var batch = EnrichedBatch(events);

        TelemetryBatchValidator.Validate(batch);
        var json = JsonSerializer.Serialize(batch, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var bytes = Encoding.UTF8.GetByteCount(json);

        Assert.True(bytes <= TelemetryIngestionLimits.MaxBodyBytes,
            $"Lote v2 de {bytes} bytes supera {TelemetryIngestionLimits.MaxBodyBytes} bytes.");
    }
}
