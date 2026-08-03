using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Identity;
using ImpactX.Models.DTOs;
using ImpactX.Services;

namespace ImpactX.Tests.Unit;

public class EmergencyContactContractTests
{
    [Fact]
    public void PublicId_UsesSafePrefixAndIsNotGuid()
    {
        var first = EmergencyContactPublicIdGenerator.Generate();
        var second = EmergencyContactPublicIdGenerator.Generate();

        Assert.StartsWith("ECT-", first, StringComparison.Ordinal);
        Assert.False(Guid.TryParse(first, out _));
        Assert.NotEqual(first, second);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void PublicDto_DoesNotExposeInternalIdsPhoneOrTokenHash()
    {
        var properties = typeof(EmergencyContactDto)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain("Id", properties);
        Assert.DoesNotContain("UsuarioId", properties);
        Assert.DoesNotContain("OwnerUserId", properties);
        Assert.DoesNotContain("ContactUserId", properties);
        Assert.DoesNotContain("Telefono", properties);
        Assert.DoesNotContain("Phone", properties);
        Assert.DoesNotContain("InvitationCodeHash", properties);
        Assert.DoesNotContain("TargetEmailNormalized", properties);
        Assert.DoesNotContain("Email", properties);
    }

    [Theory]
    [InlineData(null, 3)]
    [InlineData("Free", 3)]
    [InlineData("Basic", 5)]
    [InlineData("Standard", 5)]
    [InlineData("Premium", 10)]
    public void ContactLimits_PreserveExistingPlanPolicy(string? plan, int expected)
    {
        Assert.Equal(expected, EmergencyContactService.GetAcceptedContactLimit(plan));
    }

    [Fact]
    public void LegacyDocument_DefaultsToUnverified()
    {
        var contact = new ContactoEmergencia();

        Assert.Equal(EmergencyContactStatus.LegacyUnverified, contact.Status);
        Assert.Null(contact.PublicContactId);
    }
}
