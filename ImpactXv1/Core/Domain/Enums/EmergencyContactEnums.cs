using System.Text.Json.Serialization;

namespace ImpactX.Core.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum EmergencyContactStatus
{
    LegacyUnverified = 0,
    Pending = 1,
    Accepted = 2,
    Rejected = 3,
    Revoked = 4,
    Blocked = 5,
    Expired = 6
}
