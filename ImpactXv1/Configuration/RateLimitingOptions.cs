namespace ImpactX.Configuration;

public class RateLimitingOptions
{
    public AuthRateLimitingOptions Auth { get; set; } = new();
    public MonitorRateLimitingOptions Monitors { get; set; } = new();
    public DeviceRateLimitingOptions Devices { get; set; } = new();
    public TelemetryRateLimitingOptions Telemetry { get; set; } = new();
    public IncidentRateLimitingOptions Incidents { get; set; } = new();
    public AlertRateLimitingOptions Alerts { get; set; } = new();
}

public class AuthRateLimitingOptions
{
    public int RegisterPerMinute { get; set; } = 5;
    public int LoginPerMinute { get; set; } = 10;
    public int RefreshPerMinute { get; set; } = 30;
    public int RecoverEveryMinutes { get; set; } = 15;
    public int RecoverPerWindow { get; set; } = 3;
    public int ResetEveryMinutes { get; set; } = 15;
    public int ResetPerWindow { get; set; } = 5;
}

public class MonitorRateLimitingOptions
{
    public int InviteDetailsPerMinute { get; set; } = 20;
    public int InviteActionPerMinutePerUser { get; set; } = 20;
    public int InviteCreatePerHourPerUser { get; set; } = 10;
}

public class DeviceRateLimitingOptions
{
    public int FcmTokenPerMinutePerUser { get; set; } = 30;
}

public class TelemetryRateLimitingOptions
{
    public int IngestionPerMinutePerUser { get; set; } = 120;
}

public class IncidentRateLimitingOptions
{
    public int CreatePerMinutePerUser { get; set; } = 20;
}

public class AlertRateLimitingOptions
{
    public int DetectPerMinutePerUser { get; set; } = 30;
    public int SosPerMinutePerUser { get; set; } = 10;
}
