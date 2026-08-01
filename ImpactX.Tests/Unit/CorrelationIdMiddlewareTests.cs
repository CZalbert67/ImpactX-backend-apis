using ImpactX.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace ImpactX.Tests.Unit;

public class CorrelationIdMiddlewareTests
{
    private static async Task<string?> InvokeAsync(string? incomingHeader)
    {
        var context = new DefaultHttpContext();
        if (incomingHeader is not null)
        {
            context.Request.Headers["X-Correlation-Id"] = incomingHeader;
        }

        RequestDelegate next = _ => Task.CompletedTask;
        var middleware = new CorrelationIdMiddleware(next, NullLogger<CorrelationIdMiddleware>.Instance);
        await middleware.InvokeAsync(context);

        return context.Items["CorrelationId"] as string;
    }

    [Fact]
    public async Task ValidCorrelationId_IsPreserved()
    {
        var correlationId = await InvokeAsync("client-trace-abc");
        Assert.Equal("client-trace-abc", correlationId);
    }

    [Fact]
    public async Task MaliciousCorrelationId_IsSanitized()
    {
        var correlationId = await InvokeAsync("abc\r\nX-Evil-Injected: yes\n");
        Assert.NotNull(correlationId);
        Assert.DoesNotContain('\r', correlationId);
        Assert.DoesNotContain('\n', correlationId);
        Assert.True(correlationId.Length <= 100);
    }

    [Fact]
    public async Task OverlongCorrelationId_IsReplacedOrLimited()
    {
        var correlationId = await InvokeAsync(new string('a', 200));
        Assert.NotNull(correlationId);
        Assert.True(correlationId.Length <= 100);
        Assert.DoesNotContain('\r', correlationId);
        Assert.DoesNotContain('\n', correlationId);
    }

    [Fact]
    public async Task MissingCorrelationId_IsGenerated()
    {
        var correlationId = await InvokeAsync(null);
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.True(correlationId.Length <= 100);
    }

    [Fact]
    public async Task StoresCorrelationIdInItemsAndTraceIdentifier()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Correlation-Id"] = "items-trace-1";

        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask, NullLogger<CorrelationIdMiddleware>.Instance);
        await middleware.InvokeAsync(context);

        Assert.Equal("items-trace-1", context.Items["CorrelationId"]);
        Assert.Equal("items-trace-1", context.TraceIdentifier);
    }

    [Fact]
    public async Task EmptyCorrelationId_IsReplaced()
    {
        var correlationId = await InvokeAsync("   ");
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.NotEqual("   ", correlationId);
    }
}
