using ImpactX.Infrastructure.Data;
using Microsoft.Extensions.Configuration;

namespace ImpactX.Tests.Unit;

public class CosmosDatabaseOptionsTests
{
    private static CosmosDatabaseOptions BuildOptions(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        var options = new CosmosDatabaseOptions();
        configuration.GetSection(CosmosDatabaseOptions.SectionName).Bind(options);
        return options;
    }

    [Fact]
    public void Bind_ValidConfiguration_SetsAllValues()
    {
        var options = BuildOptions(new Dictionary<string, string?>
        {
            ["AzureCosmosDb:Endpoint"] = "https://impactx-db-west-final.documents.azure.com:443/",
            ["AzureCosmosDb:Key"] = "dGVzdC1rZXk=",
            ["AzureCosmosDb:DatabaseName"] = "ImpactX-Data",
            ["AzureCosmosDb:SharedThroughput"] = "400",
            ["AzureCosmosDb:RequestTimeoutSeconds"] = "30",
            ["AzureCosmosDb:MaxRetryAttemptsOnRateLimitedRequests"] = "3",
            ["AzureCosmosDb:MaxRetryWaitTimeSeconds"] = "30"
        });

        Assert.Equal("https://impactx-db-west-final.documents.azure.com:443/", options.Endpoint);
        Assert.Equal("dGVzdC1rZXk=", options.Key);
        Assert.Equal("ImpactX-Data", options.DatabaseName);
        Assert.Equal(400, options.SharedThroughput);
        Assert.Equal(30, options.RequestTimeoutSeconds);
        Assert.Equal(3, options.MaxRetryAttemptsOnRateLimitedRequests);
        Assert.Equal(30, options.MaxRetryWaitTimeSeconds);
        Assert.Null(CosmosDatabaseOptions.Validate(options));
    }

    [Fact]
    public void Defaults_ApplyWhenSectionAbsent()
    {
        var options = BuildOptions(new Dictionary<string, string?>
        {
            ["AzureCosmosDb:Endpoint"] = "https://localhost:443/"
        });

        Assert.Equal(CosmosDatabaseOptions.DefaultDatabaseName, options.DatabaseName);
        Assert.Equal(CosmosDatabaseOptions.DefaultSharedThroughput, options.SharedThroughput);
        Assert.Equal(30, options.RequestTimeoutSeconds);
        Assert.Equal(3, options.MaxRetryAttemptsOnRateLimitedRequests);
        Assert.Null(CosmosDatabaseOptions.Validate(options));
    }

    [Fact]
    public void Validate_RejectsEmptyOrInvalidEndpoint()
    {
        Assert.NotNull(CosmosDatabaseOptions.Validate(new CosmosDatabaseOptions { Endpoint = "" }));
        Assert.NotNull(CosmosDatabaseOptions.Validate(new CosmosDatabaseOptions { Endpoint = "not-a-uri" }));
        Assert.NotNull(CosmosDatabaseOptions.Validate(new CosmosDatabaseOptions { Endpoint = "file:///etc/passwd" }));
    }

    [Fact]
    public void Validate_RejectsEmptyDatabaseName()
    {
        var options = new CosmosDatabaseOptions
        {
            Endpoint = "https://localhost:443/",
            DatabaseName = " "
        };
        Assert.NotNull(CosmosDatabaseOptions.Validate(options));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(1001)]
    public void Validate_RejectsSharedThroughputOutsideAllowedRange(int throughput)
    {
        var options = new CosmosDatabaseOptions
        {
            Endpoint = "https://localhost:443/",
            SharedThroughput = throughput
        };
        Assert.NotNull(CosmosDatabaseOptions.Validate(options));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(400)]
    [InlineData(1000)]
    public void Validate_AcceptsSharedThroughputWithinAllowedRange(int throughput)
    {
        var options = new CosmosDatabaseOptions
        {
            Endpoint = "https://localhost:443/",
            SharedThroughput = throughput
        };
        Assert.Null(CosmosDatabaseOptions.Validate(options));
    }

    [Fact]
    public void Validate_RejectsInvalidTimeoutsAndRetries()
    {
        Assert.NotNull(CosmosDatabaseOptions.Validate(new CosmosDatabaseOptions
        {
            Endpoint = "https://localhost:443/",
            RequestTimeoutSeconds = 0
        }));
        Assert.NotNull(CosmosDatabaseOptions.Validate(new CosmosDatabaseOptions
        {
            Endpoint = "https://localhost:443/",
            MaxRetryAttemptsOnRateLimitedRequests = -1
        }));
        Assert.NotNull(CosmosDatabaseOptions.Validate(new CosmosDatabaseOptions
        {
            Endpoint = "https://localhost:443/",
            MaxRetryWaitTimeSeconds = -1
        }));
    }

    [Fact]
    public void Validate_DoesNotRejectPlaceholderKey_ReadinessOwnsThatCheck()
    {
        // La Key nunca va en appsettings con valor real; su ausencia/placeholder
        // lo verifica ConfigurationReadinessCheck, no la validación de opciones,
        // para no impedir el arranque en Development.
        var options = new CosmosDatabaseOptions
        {
            Endpoint = "https://localhost:443/",
            Key = CosmosDatabaseDefaults.PlaceholderKey
        };
        Assert.Null(CosmosDatabaseOptions.Validate(options));
    }
}
