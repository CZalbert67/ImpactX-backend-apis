using ImpactX.Services;

namespace ImpactX.Tests.Unit;

public sealed class FamilyPlanCapacityContractTests
{
    [Theory]
    [InlineData("Free", 1)]
    [InlineData("Standard", 2)]
    [InlineData("Basic", 2)]
    [InlineData("Premium", 5)]
    public void InvitedMemberLimit_IncludesOwnerInAdvertisedTotal(
        string planName,
        int expectedInvitedLimit)
    {
        Assert.Equal(expectedInvitedLimit, FamilySubscriptionService.GetMemberLimit(planName));
    }

    [Theory]
    [InlineData("Free", 1)]
    [InlineData("Standard", 3)]
    [InlineData("Basic", 3)]
    [InlineData("Premium", 6)]
    public void MonitoringLimit_RemainsIndependentFromFamilyCapacity(
        string planName,
        int expectedMonitoringLimit)
    {
        Assert.Equal(expectedMonitoringLimit, FamilySubscriptionService.GetMonitoringLimit(planName));
    }
}
