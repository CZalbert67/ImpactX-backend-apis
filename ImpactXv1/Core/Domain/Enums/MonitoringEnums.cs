using System.Text.Json.Serialization;

namespace ImpactX.Core.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum MonitoringRelationshipStatus
{
    Pending,
    Accepted,
    Rejected,
    Revoked,
    Blocked,
    Expired
}

[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum MonitoringRequestDirection
{
    MonitorInvitesMonitored,
    MonitoredRequestsMonitor
}

public enum MonitoringResourcePermission
{
    Incidents,
    CriticalAlerts,
    Routes,
    Telemetry,
    Location
}
