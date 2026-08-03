namespace ImpactX.Configuration;

public sealed class SubscriptionLifecycleOptions
{
    public const string SectionName = "SubscriptionLifecycle";
    public bool Enabled { get; set; }
    public int PollIntervalMinutes { get; set; } = 15;
}
