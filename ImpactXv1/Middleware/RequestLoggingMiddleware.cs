using System.Diagnostics;

namespace ImpactX.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Items["CorrelationId"] as string ?? context.TraceIdentifier;
        var method = Sanitize(context.Request.Method);
        var path = Sanitize(context.Request.Path.Value ?? string.Empty);

        var sw = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        finally
        {
            sw.Stop();

            _logger.LogInformation(
                "HTTP {Method} {Path} completed with status {StatusCode} in {ElapsedMs}ms, correlationId {CorrelationId}",
                method, path, context.Response.StatusCode, sw.ElapsedMilliseconds, correlationId);
        }
    }

    private static string Sanitize(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ').Trim();
    }
}
