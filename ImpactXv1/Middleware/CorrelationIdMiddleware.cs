using System.Text.RegularExpressions;

namespace ImpactX.Middleware;

public partial class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;
    private const int MaxLength = 100;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(correlationId) || !IsValid(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }

        correlationId = Sanitize(correlationId);

        context.Items["CorrelationId"] = correlationId;
        context.TraceIdentifier = correlationId;

        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-Correlation-Id"] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId
        });

        await _next(context);
    }

    private static bool IsValid(string value)
    {
        return value.Length <= MaxLength && !ContainsCrLf(value);
    }

    private static string Sanitize(string value)
    {
        var sanitized = CrLfPattern().Replace(value, "");
        return sanitized.Length > MaxLength ? sanitized[..MaxLength] : sanitized;
    }

    private static bool ContainsCrLf(string value)
    {
        return value.Contains('\r') || value.Contains('\n');
    }

    [GeneratedRegex("[\r\n]")]
    private static partial Regex CrLfPattern();
}
