namespace ImpactX.Infrastructure.Data;

public enum DatabaseInitializationMode
{
    Ensure,
    ValidateOnly
}

public class DatabaseInitializationOptions
{
    public bool Enabled { get; set; } = true;
    public DatabaseInitializationMode Mode { get; set; } = DatabaseInitializationMode.Ensure;
    public int MaxAttempts { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 5;
    public int TimeoutSeconds { get; set; } = 60;
}
