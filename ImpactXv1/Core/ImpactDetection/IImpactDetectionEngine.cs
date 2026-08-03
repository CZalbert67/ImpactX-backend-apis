using ImpactX.Core.Domain;

namespace ImpactX.Core.ImpactDetection;

public interface IImpactDetectionEngine
{
    ImpactDetectionDecision Evaluate(ViajeTelemetry telemetry);
}
