using ImpactX.Core.Domain;
using ImpactX.Core.ImpactDetection;

namespace ImpactX.Tests.Unit;

public sealed class ImpactDetectionEngineTests
{
    private readonly ImpactDetectionEngine _engine = new();

    [Fact]
    public void MissingPrimarySignals_IsInsufficientData()
    {
        var result = _engine.Evaluate(new ViajeTelemetry());

        Assert.False(result.IsCandidate);
        Assert.Equal("insufficient_data", result.DetectionLabel);
        Assert.Equal("none", result.SeverityLabel);
        Assert.Equal(ImpactDetectionEngine.CurrentRuleVersion, result.RuleVersion);
    }

    [Fact]
    public void OrdinaryDriving_DoesNotCreateCandidate()
    {
        var result = _engine.Evaluate(new ViajeTelemetry
        {
            MagnitudAceleracion = 11.2,
            Desaceleracion = 2.5,
            MagnitudGiroscopio = 0.8,
            Velocidad = 45,
            CalidadSensor = "high"
        });

        Assert.False(result.IsCandidate);
        Assert.Equal("below_threshold", result.DetectionLabel);
    }

    [Fact]
    public void ModerateCorroboratedImpact_HasCancellationWindow()
    {
        var result = _engine.Evaluate(new ViajeTelemetry
        {
            MagnitudAceleracion = 22,
            Desaceleracion = 8,
            MagnitudGiroscopio = 1.0,
            Velocidad = 30,
            CalidadSensor = "medium"
        });

        Assert.True(result.IsCandidate);
        Assert.Equal("moderate", result.SeverityLabel);
        Assert.False(result.DispatchImmediately);
        Assert.Equal(10, result.CancellationWindowSeconds);
        Assert.True(result.Score >= 5);
    }

    [Fact]
    public void SevereImpact_DispatchesImmediately()
    {
        var result = _engine.Evaluate(new ViajeTelemetry
        {
            MagnitudAceleracion = 37,
            Desaceleracion = 19,
            MagnitudGiroscopio = 6.2,
            Velocidad = 90,
            CalidadSensor = "high"
        });

        Assert.True(result.IsCandidate);
        Assert.Contains(result.SeverityLabel, new[] { "severe", "critical" });
        Assert.True(result.DispatchImmediately);
        Assert.Equal(0, result.CancellationWindowSeconds);
    }

    [Fact]
    public void CriticalAcceleration_IsCritical()
    {
        var result = _engine.Evaluate(new ViajeTelemetry
        {
            MagnitudAceleracion = 46,
            Desaceleracion = 9,
            MagnitudGiroscopio = 3.2,
            Velocidad = 30,
            CalidadSensor = "medium"
        });

        Assert.True(result.IsCandidate);
        Assert.Equal("critical", result.SeverityLabel);
        Assert.True(result.DispatchImmediately);
    }

    [Fact]
    public void DegradedRequiredSensors_ReduceScore()
    {
        var normal = _engine.Evaluate(new ViajeTelemetry
        {
            MagnitudAceleracion = 22,
            Desaceleracion = 8,
            MagnitudGiroscopio = 3,
            Velocidad = 40,
            CalidadSensor = "medium"
        });
        var degraded = _engine.Evaluate(new ViajeTelemetry
        {
            MagnitudAceleracion = 22,
            Desaceleracion = 8,
            MagnitudGiroscopio = 3,
            Velocidad = 40,
            CalidadSensor = "medium",
            SensorFlagsCsv = "accelerometer_unavailable,sensor_saturated"
        });

        Assert.True(normal.Score > degraded.Score);
        Assert.Contains("degraded_required_sensor", degraded.Reasons);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void InvalidAcceleration_IsTreatedAsMissing(double acceleration)
    {
        var result = _engine.Evaluate(new ViajeTelemetry
        {
            MagnitudAceleracion = acceleration
        });

        Assert.False(result.IsCandidate);
        Assert.Equal("insufficient_data", result.DetectionLabel);
    }
}
