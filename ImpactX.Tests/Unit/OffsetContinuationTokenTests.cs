using ImpactX.Core.Exceptions;
using ImpactX.Infrastructure.Data.Repositories.EF;

namespace ImpactX.Tests.Unit;

public class OffsetContinuationTokenTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(20)]
    [InlineData(1000)]
    public void EncodeDecode_RoundTrip(int offset)
    {
        var token = OffsetContinuationToken.Encode(offset);
        Assert.NotEmpty(token);
        Assert.Equal(offset, OffsetContinuationToken.Decode(token));
    }

    [Theory]
    [InlineData("not-base64!!")]
    [InlineData("AQI=")]
    [InlineData("b2Zmc2V0Oi0x")]
    [InlineData("b2Zmc2V0Og==")]
    public void Decode_InvalidToken_ThrowsBadRequest(string token)
    {
        Assert.Throws<BadRequestException>(() => OffsetContinuationToken.Decode(token));
    }

    [Fact]
    public void Decode_EmptyToken_ThrowsBadRequest()
    {
        Assert.Throws<BadRequestException>(() => OffsetContinuationToken.Decode(""));
    }
}
