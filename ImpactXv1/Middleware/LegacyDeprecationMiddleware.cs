using ImpactX.Core.ApiContract;

namespace ImpactX.Middleware;

public class LegacyDeprecationMiddleware
{
    private readonly RequestDelegate _next;

    private static readonly HashSet<string> ExcludedPrefixes =
    [
        "/health",
        "/health/live",
        "/health/ready",
        "/openapi",
        "/swagger"
    ];

    public LegacyDeprecationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (IsLegacyRoute(context.Request.Path))
        {
            context.Response.OnStarting(() =>
            {
                if (!context.Response.HasStarted)
                {
                    context.Response.Headers["Deprecation"] = "true";
                    context.Response.Headers["Warning"] = "299 - \"Deprecated API. Use /api/v1.\"";
                    context.Response.Headers["Link"] = "</openapi/v1.json>; rel=\"successor-version\"";
                    context.Response.Headers["Sunset"] = ApiContractDefinition.LegacySunsetHttpDate;
                    context.Response.Headers["X-ImpactX-Legacy-Route"] = "true";
                }
                return Task.CompletedTask;
            });
        }

        await _next(context);
    }

    private static bool IsLegacyRoute(PathString path)
    {
        var pathStr = path.Value ?? string.Empty;

        if (ExcludedPrefixes.Any(excluded =>
            pathStr.StartsWith(excluded, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return pathStr.StartsWith("/api/", StringComparison.OrdinalIgnoreCase)
            && !pathStr.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase);
    }
}
