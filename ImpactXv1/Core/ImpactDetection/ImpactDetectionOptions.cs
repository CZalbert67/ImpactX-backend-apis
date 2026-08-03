namespace ImpactX.Core.ImpactDetection;

public sealed class ImpactDetectionOptions
{
    public const string SectionName = "ImpactDetection";

    public bool Enabled { get; set; } = true;
    public bool PendingDispatchWorkerEnabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 2;
    public int ActiveAlertCooldownSeconds { get; set; } = 60;
}
