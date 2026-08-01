namespace ImpactX.Infrastructure.Data;

public enum DatabaseInitializationStatus
{
    NotStarted,
    Running,
    Succeeded,
    Failed
}

public sealed class DatabaseInitializationState
{
    private readonly object _gate = new();

    public DatabaseInitializationStatus Status { get; private set; } = DatabaseInitializationStatus.NotStarted;
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }
    public string? FailureDescription { get; private set; }

    public bool IsReady => Status == DatabaseInitializationStatus.Succeeded;

    public void MarkRunning(int maxAttempts)
    {
        lock (_gate)
        {
            Status = DatabaseInitializationStatus.Running;
            Attempts = 0;
            MaxAttempts = maxAttempts;
            StartedAt = DateTimeOffset.UtcNow;
            FinishedAt = null;
            FailureDescription = null;
        }
    }

    public void MarkAttempt()
    {
        lock (_gate)
        {
            Attempts++;
        }
    }

    public void MarkSucceeded()
    {
        lock (_gate)
        {
            Status = DatabaseInitializationStatus.Succeeded;
            FinishedAt = DateTimeOffset.UtcNow;
            FailureDescription = null;
        }
    }

    public void MarkFailed(string safeDescription)
    {
        lock (_gate)
        {
            Status = DatabaseInitializationStatus.Failed;
            FinishedAt = DateTimeOffset.UtcNow;
            FailureDescription = safeDescription;
        }
    }
}
