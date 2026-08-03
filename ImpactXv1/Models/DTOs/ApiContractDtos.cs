namespace ImpactX.Models.DTOs;

public sealed class ApiContractSnapshotDto
{
    public string ApiVersion { get; init; } = string.Empty;
    public string ContractVersion { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Authentication { get; init; } = "Bearer JWT";
    public string OpenApiDocument { get; init; } = "/openapi/v1.json";
    public string LegacySunsetUtc { get; init; } = string.Empty;
    public IReadOnlyList<string> SupportedClients { get; init; } = [];
    public IReadOnlyList<string> CanonicalModules { get; init; } = [];
    public IReadOnlyList<string> LegacyModules { get; init; } = [];
    public IReadOnlyDictionary<string, int> RetentionDays { get; init; } = new Dictionary<string, int>();
    public IReadOnlyList<ApiRouteContractDto> Routes { get; init; } = [];
}

public sealed class ApiRouteContractDto
{
    public string Path { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public bool Anonymous { get; init; }
    public IReadOnlyList<string> AllowedClients { get; init; } = [];
}

public sealed class ClientCapabilityContractDto
{
    public string Client { get; init; } = string.Empty;
    public IReadOnlyList<string> Capabilities { get; init; } = [];
    public string ContractVersion { get; init; } = string.Empty;
}
