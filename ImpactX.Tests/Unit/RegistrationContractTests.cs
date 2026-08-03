using ImpactX.Core.Identity;

namespace ImpactX.Tests.Unit;

public class RegistrationContractTests
{
    [Theory]
    [InlineData("Password123!", true)]
    [InlineData("password123!", false)]
    [InlineData("PASSWORD123!", false)]
    [InlineData("PasswordOnly!", false)]
    [InlineData("Password123", false)]
    [InlineData("Pass1!", false)]
    public void IsStrongPassword_EnforcesCurrentContract(string value, bool expected)
    {
        Assert.Equal(expected, RegistrationContract.IsStrongPassword(value));
    }

    [Theory]
    [InlineData("+52 773 123 4567", true)]
    [InlineData("7731234567", true)]
    [InlineData("(773) 123-4567", true)]
    [InlineData("123", false)]
    [InlineData("773-ABC-4567", false)]
    public void IsValidPhone_AcceptsOnlySafePhoneShapes(string value, bool expected)
    {
        Assert.Equal(expected, RegistrationContract.IsValidPhone(value));
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("ImpactX", true)]
    [InlineData("usuario_123", false)]
    public void UsernamePolicy_ReservedNamesAreCentralized(string value, bool expected)
    {
        Assert.Equal(expected, UsernamePolicy.IsReserved(value));
    }
}
