using System.Text.RegularExpressions;

namespace ImpactX.Middleware;

public partial class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;
    private const int MaxLength = 100;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
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
