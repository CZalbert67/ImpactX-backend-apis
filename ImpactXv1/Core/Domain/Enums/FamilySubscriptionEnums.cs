using System.Text.Json.Serialization;

namespace ImpactX.Core.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum FamilySubscriptionStatus
{
    Active,
    PastDue,
    Suspended,
    Cancelled,
    Expired
}

[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum FamilyMembershipRole
{
    Owner,
    Member
}

[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum FamilyMembershipStatus
{
    Pending,
    Active,
    Rejected,
    Left,
    Removed,
    Expired
}

[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum FamilyInvitationStatus
{
    Pending,
    Accepted,
    Rejected,
    Expired,
    Revoked,
    Consumed
}
