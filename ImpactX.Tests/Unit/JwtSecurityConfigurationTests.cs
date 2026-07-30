using Microsoft.Extensions.Configuration;
using ImpactX.Infrastructure.Security;

namespace ImpactX.Tests.Unit;

public class JwtSecurityConfigurationTests
{
    private static IConfiguration CreateConfig(string? secret)
    {
        var data = new List<KeyValuePair<string, string?>>
        {
            new("Jwt:Secret", secret)
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
    }

    [Fact]
    [Trait("Category", "Security")]
    public void GetRequiredSecret_WithValidSecret_ReturnsSecret()
    {
        var config = CreateConfig("this-is-a-test-secret-that-is-32-bytes-long!");
        var result = JwtSecurityConfiguration.GetRequiredSecret(config);
        Assert.Equal("this-is-a-test-secret-that-is-32-bytes-long!", result);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void GetRequiredSecret_WithNullSecret_ThrowsInvalidOperationException()
    {
        var config = CreateConfig(null);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityConfiguration.GetRequiredSecret(config));
        Assert.Contains("JWT signing key is not configured", ex.Message);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void GetRequiredSecret_WithEmptySecret_ThrowsInvalidOperationException()
    {
        var config = CreateConfig(string.Empty);
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityConfiguration.GetRequiredSecret(config));
        Assert.Contains("JWT signing key is not configured", ex.Message);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void GetRequiredSecret_WithWhitespaceSecret_ThrowsInvalidOperationException()
    {
        var config = CreateConfig("   ");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityConfiguration.GetRequiredSecret(config));
        Assert.Contains("JWT signing key is not configured", ex.Message);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void GetRequiredSecret_WithShortSecret_ThrowsInvalidOperationException()
    {
        var config = CreateConfig("short");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityConfiguration.GetRequiredSecret(config));
        Assert.Contains("at least 32 bytes", ex.Message);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void GetRequiredSecret_ExceptionDoesNotContainSecretValue()
    {
        var config = CreateConfig("short");
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityConfiguration.GetRequiredSecret(config));
        Assert.DoesNotContain("short", ex.Message);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void GetSigningKey_WithValidSecret_ReturnsSymmetricSecurityKey()
    {
        var config = CreateConfig("this-is-a-test-secret-that-is-32-bytes-long!");
        var key = JwtSecurityConfiguration.GetSigningKey(config);
        Assert.NotNull(key);
        Assert.True(key.KeySize > 0);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void GetRequiredSecret_RejectsSecretShorterThan32Bytes()
    {
        var config = CreateConfig("abcdefghijklmnopqrstuvw"); // 26 chars = 26 bytes
        var ex = Assert.Throws<InvalidOperationException>(() =>
            JwtSecurityConfiguration.GetRequiredSecret(config));
        Assert.Contains("32 bytes", ex.Message);
    }

    [Fact]
    [Trait("Category", "Security")]
    public void GetRequiredSecret_AcceptsSecretAtExactly32Bytes()
    {
        var config = CreateConfig("abcdefghijklmnopqrstuvwxyz123456"); // 32 bytes
        var result = JwtSecurityConfiguration.GetRequiredSecret(config);
        Assert.NotNull(result);
        Assert.Equal(32, System.Text.Encoding.UTF8.GetByteCount(result));
    }
}
