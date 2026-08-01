namespace ImpactX.Configuration;

public class ReadinessOptions
{
    public bool InitializationRequired { get; set; } = true;
    public int CosmosAccessTimeoutSeconds { get; set; } = 5;
}
