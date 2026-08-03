namespace ImpactX.Core.ApiContract;

public static class ApiContractDefinition
{
    public const string ApiVersion = "v1";
    public const string ContractVersion = "2026.08.05";
    public const string ContractStatus = "frozen";
    public const string LegacySunsetHttpDate = "Tue, 02 Feb 2027 00:00:00 GMT";
    public const string LegacySunsetUtc = "2027-02-02T00:00:00Z";

    public static readonly string[] SupportedClients = ["web", "mobile", "wearable"];

    public static readonly IReadOnlyDictionary<string, string[]> ClientCapabilities =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["web"] =
            [
                "account:read", "account:write", "analytics:read", "alerts:read",
                "contacts:manage", "family:manage", "incidents:manage", "monitoring:manage",
                "notifications:manage", "profile:manage", "routes:manage", "subscriptions:manage",
                "trips:read", "vehicles:manage", "wearable:read"
            ],
            ["mobile"] =
            [
                "account:read", "account:write", "alerts:read", "contacts:manage",
                "family:manage", "incidents:confirm-ok", "incidents:read", "mobile-sync:offline",
                "monitoring:manage", "notifications:manage", "profile:manage", "quick-messages:manage",
                "subscriptions:manage", "trips:read", "vehicles:manage", "wearable:pair",
                "wearable:permissions", "wearable:read", "wearable:unlink"
            ],
            ["wearable"] =
            [
                "alerts:create", "telemetry:write", "trips:finish", "trips:pause",
                "trips:resume", "trips:start", "wearable:battery", "wearable:diagnostics",
                "wearable:heartbeat", "wearable:sync"
            ]
        };

    public static readonly IReadOnlyDictionary<string, int> RetentionDays =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["trips"] = 90,
            ["telemetry"] = 90,
            ["alerts"] = 365,
            ["incidents"] = 365,
            ["notifications"] = 30
        };

    public static readonly string[] CanonicalModules =
    [
        "account", "alerts", "analytics", "auth", "contacts", "devices",
        "family-subscriptions", "incidents", "mobile-sync", "monitoring-relationships",
        "notifications", "permissions", "plans", "profile", "quick-messages", "routes",
        "settings", "subscriptions", "trips", "vehicles", "wearable"
    ];

    public static readonly string[] LegacyModules = ["contacts", "monitors"];
}
