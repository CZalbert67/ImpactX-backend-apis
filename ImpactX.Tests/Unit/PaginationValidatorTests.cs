using ImpactX.Core.Exceptions;
using ImpactX.Core.Pagination;

namespace ImpactX.Tests.Unit;

public class PaginationValidatorTests
{
    [Theory]
    [InlineData(null, 20)]
    [InlineData(1, 1)]
    [InlineData(20, 20)]
    [InlineData(100, 100)]
    public void ResolvePageSize_ValidValues_ReturnsSize(int? pageSize, int expected)
    {
        Assert.Equal(expected, PaginationValidator.ResolvePageSize(pageSize));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(1000)]
    public void ResolvePageSize_OutOfRange_ThrowsBadRequest(int pageSize)
    {
        Assert.Throws<BadRequestException>(() => PaginationValidator.ResolvePageSize(pageSize));
    }

    [Fact]
    public void ValidateContinuationToken_Null_IsAllowed()
    {
        PaginationValidator.ValidateContinuationToken(null);
    }

    [Fact]
    public void ValidateContinuationToken_Valid_IsAllowed()
    {
        PaginationValidator.ValidateContinuationToken("AQI");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateContinuationToken_EmptyOrWhitespace_Throws(string token)
    {
        Assert.Throws<BadRequestException>(() => PaginationValidator.ValidateContinuationToken(token));
    }

    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\rb")]
    [InlineData("a\tb")]
    public void ValidateContinuationToken_ControlCharacters_Throws(string token)
    {
        Assert.Throws<BadRequestException>(() => PaginationValidator.ValidateContinuationToken(token));
    }

    [Fact]
    public void ValidateContinuationToken_TooLong_Throws()
    {
        var token = new string('a', PaginationDefaults.MaxContinuationTokenLength + 1);
        Assert.Throws<BadRequestException>(() => PaginationValidator.ValidateContinuationToken(token));
    }

    [Fact]
    public void ValidateContinuationToken_MaxLength_IsAllowed()
    {
        var token = new string('a', PaginationDefaults.MaxContinuationTokenLength);
        PaginationValidator.ValidateContinuationToken(token);
    }
}
