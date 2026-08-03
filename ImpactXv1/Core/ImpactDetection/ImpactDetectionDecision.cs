namespace ImpactX.Core.ImpactDetection;

/// <summary>
/// Resultado determinista del motor de reglas. No representa un diagnóstico
/// médico ni sustituye al futuro modelo ML; únicamente clasifica señales
/// técnicas para decidir si debe abrirse una alerta interna de ImpactX.
/// </summary>
public sealed record ImpactDetectionDecision(
    bool IsCandidate,
    string DetectionLabel,
    string SeverityLabel,
    int Score,
    bool DispatchImmediately,
    int CancellationWindowSeconds,
    string RuleVersion,
    IReadOnlyList<string> Reasons);
