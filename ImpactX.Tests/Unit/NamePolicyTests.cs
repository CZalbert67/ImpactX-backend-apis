using ImpactX.Core.Exceptions;
using ImpactX.Core.Identity;

namespace ImpactX.Tests.Unit;

public class NamePolicyTests
{
    [Theory]
    [InlineData("Juan Perez")]
    [InlineData("María González")]
    [InlineData("Ana-Karen Suárez")]
    [InlineData("Carlos")]
    [InlineData("")]
    [InlineData(null)]
    public void ContainsOffensiveWord_CleanNames_ReturnsFalse(string? value)
    {
        Assert.False(NamePolicy.ContainsOffensiveWord(value));
    }

    [Theory]
    [InlineData("Juan puta Lopez")]
    [InlineData("juan puto")]
    [InlineData("PUTA")]
    [InlineData("maricon-n")]
    [InlineData("idiota.")]
    [InlineData("Joder")]
    [InlineData("cabrón")]
    [InlineData("estúpido")]
    [InlineData("maricon")]
    public void ContainsOffensiveWord_OffensiveTerms_ReturnsTrue(string value)
    {
        Assert.True(NamePolicy.ContainsOffensiveWord(value));
    }

    [Theory]
    [InlineData("  Juan   Perez  ", "Juan Perez")]
    [InlineData("  Ana   ", "Ana")]
    public void Normalize_CollapsesWhitespace(string input, string expected)
    {
        Assert.Equal(expected, NamePolicy.Normalize(input));
    }

    [Theory]
    [InlineData("Juan puta Lopez")]
    [InlineData("juan maricon")]
    public void Validate_OffensiveName_ThrowsBadRequest(string value)
    {
        Assert.Throws<BadRequestException>(() => NamePolicy.Validate(value));
    }

    [Fact]
    public void Validate_CleanName_DoesNotThrow()
    {
        NamePolicy.Validate("Juan Perez");
    }
}
