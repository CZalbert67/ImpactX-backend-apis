using ImpactX.Core.Domain;
using ImpactX.Core.Telemetry;

namespace ImpactX.Core.ImpactDetection;

/// <summary>
/// Motor inicial de reglas explicables. Las constantes son deliberadamente
/// conservadoras y versionadas. El futuro modelo ML podrá complementar la
/// decisión, pero nunca aceptar etiquetas enviadas por el cliente.
/// </summary>
public sealed class ImpactDetectionEngine : IImpactDetectionEngine
{
    public const string CurrentRuleVersion = "impact-rules-v1";
    public const int DefaultCancellationWindowSeconds = 10;

    public ImpactDetectionDecision Evaluate(ViajeTelemetry telemetry)
    {
        ArgumentNullException.ThrowIfNull(telemetry);

        var acceleration = PositiveFinite(telemetry.MagnitudAceleracion);
        var deceleration = PositiveFinite(telemetry.Desaceleracion);
        var gyroscope = PositiveFinite(telemetry.MagnitudGiroscopio);
        var speed = PositiveFinite(telemetry.Velocidad) ?? 0d;
        var quality = TelemetrySchema.NormalizeQuality(telemetry.CalidadSensor);
        var flags = TelemetryCanonicalizer.ParseSensorFlags(telemetry.SensorFlagsCsv);

        if (acceleration is null && deceleration is null)
        {
            return NoCandidate(
                "insufficient_data",
                0,
                ["missing_acceleration_and_deceleration"]);
        }

        var score = 0;
        var reasons = new List<string>();

        AddAccelerationScore(acceleration, ref score, reasons);
        AddDecelerationScore(deceleration, ref score, reasons);
        AddGyroscopeScore(gyroscope, ref score, reasons);
        AddSpeedScore(speed, ref score, reasons);
        AddOrientationScore(telemetry, ref score, reasons);
        AddBiometricScore(telemetry, ref score, reasons);

        if (quality == "low")
        {
            score--;
            reasons.Add("low_sensor_quality");
        }
        else if (quality == "high")
        {
            score++;
            reasons.Add("high_sensor_quality");
        }

        if (flags.Any(IsSevereSensorDegradation))
        {
            score -= 2;
            reasons.Add("degraded_required_sensor");
        }

        score = Math.Max(0, score);

        var hasPrimaryImpactSignal = acceleration >= 16d || deceleration >= 8d;
        var hasCorroboratingSignal = deceleration >= 8d
            || gyroscope >= 3d
            || speed >= 20d
            || HasExtremeOrientation(telemetry);
        var candidate = score >= 4 && hasPrimaryImpactSignal && hasCorroboratingSignal;

        if (!candidate)
        {
            return NoCandidate(
                score == 0 ? "no_impact" : "below_threshold",
                score,
                reasons);
        }

        var severity = ResolveSeverity(score, acceleration, deceleration, speed);
        var immediate = severity is "severe" or "critical";

        return new ImpactDetectionDecision(
            true,
            "impact_candidate",
            severity,
            score,
            immediate,
            immediate ? 0 : DefaultCancellationWindowSeconds,
            CurrentRuleVersion,
            reasons);
    }

    private static ImpactDetectionDecision NoCandidate(
        string label,
        int score,
        IReadOnlyList<string> reasons)
    {
        return new ImpactDetectionDecision(
            false,
            label,
            "none",
            score,
            false,
            0,
            CurrentRuleVersion,
            reasons);
    }

    private static void AddAccelerationScore(double? value, ref int score, ICollection<string> reasons)
    {
        if (value >= 45d) { score += 6; reasons.Add("acceleration_gte_45"); }
        else if (value >= 35d) { score += 5; reasons.Add("acceleration_gte_35"); }
        else if (value >= 28d) { score += 4; reasons.Add("acceleration_gte_28"); }
        else if (value >= 22d) { score += 3; reasons.Add("acceleration_gte_22"); }
        else if (value >= 16d) { score += 1; reasons.Add("acceleration_gte_16"); }
    }

    private static void AddDecelerationScore(double? value, ref int score, ICollection<string> reasons)
    {
        if (value >= 24d) { score += 5; reasons.Add("deceleration_gte_24"); }
        else if (value >= 18d) { score += 4; reasons.Add("deceleration_gte_18"); }
        else if (value >= 12d) { score += 3; reasons.Add("deceleration_gte_12"); }
        else if (value >= 8d) { score += 2; reasons.Add("deceleration_gte_8"); }
    }

    private static void AddGyroscopeScore(double? value, ref int score, ICollection<string> reasons)
    {
        if (value >= 6d) { score += 2; reasons.Add("gyroscope_gte_6"); }
        else if (value >= 3d) { score += 1; reasons.Add("gyroscope_gte_3"); }
    }

    private static void AddSpeedScore(double value, ref int score, ICollection<string> reasons)
    {
        if (value >= 80d) { score += 2; reasons.Add("speed_gte_80"); }
        else if (value >= 40d) { score += 1; reasons.Add("speed_gte_40"); }
    }

    private static void AddOrientationScore(ViajeTelemetry telemetry, ref int score, ICollection<string> reasons)
    {
        if (!HasExtremeOrientation(telemetry))
            return;

        score++;
        reasons.Add("extreme_orientation");
    }

    private static void AddBiometricScore(ViajeTelemetry telemetry, ref int score, ICollection<string> reasons)
    {
        if (telemetry.FrecuenciaCardiaca >= 140)
        {
            score++;
            reasons.Add("heart_rate_gte_140");
        }

        if (telemetry.Spo2Porcentaje is <= 90d and > 0d)
        {
            score++;
            reasons.Add("spo2_lte_90");
        }
    }

    private static bool HasExtremeOrientation(ViajeTelemetry telemetry)
    {
        return AbsoluteFinite(telemetry.Pitch) >= 60d
            || AbsoluteFinite(telemetry.Roll) >= 60d;
    }

    private static bool IsSevereSensorDegradation(string flag)
    {
        return flag is "accelerometer_unavailable"
            or "gyroscope_unavailable"
            or "sensor_saturated"
            or "clock_unreliable";
    }

    private static string ResolveSeverity(int score, double? acceleration, double? deceleration, double speed)
    {
        if (score >= 10 || acceleration >= 45d || deceleration >= 24d)
            return "critical";

        if (score >= 7 || acceleration >= 35d || deceleration >= 18d)
            return "severe";

        if (score >= 5 || acceleration >= 28d || deceleration >= 12d || speed >= 80d)
            return "moderate";

        return "bump";
    }

    private static double? PositiveFinite(double? value)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return null;

        return Math.Max(0d, value.Value);
    }

    private static double? AbsoluteFinite(double? value)
    {
        if (value is null || double.IsNaN(value.Value) || double.IsInfinity(value.Value))
            return null;

        return Math.Abs(value.Value);
    }
}
