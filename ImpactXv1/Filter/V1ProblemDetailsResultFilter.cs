using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ImpactX.Filter;

public class V1ProblemDetailsResultFilter : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        var path = context.HttpContext.Request.Path.Value;
        if (path is null || !path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
            return;

        if (context.Result is ObjectResult objectResult)
        {
            var statusCode = objectResult.StatusCode ?? 0;

            if (statusCode is >= 400 and < 600 and not 429)
            {
                if (objectResult.Value is ProblemDetails)
                    return;

                var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
                var correlationId = context.HttpContext.Items["CorrelationId"] as string ?? traceId;

                var title = statusCode switch
                {
                    400 => "Bad Request",
                    401 => "Unauthorized",
                    403 => "Forbidden",
                    404 => "Not Found",
                    409 => "Conflict",
                    _ => "Error"
                };

                var detail = statusCode switch
                {
                    401 => "Authentication is required.",
                    403 => "You do not have permission to access this resource.",
                    404 => "The requested resource was not found.",
                    _ => null
                };

                var problem = new ProblemDetails
                {
                    Type = statusCode switch
                    {
                        400 => "https://impactx.app/errors/validation",
                        401 => "https://impactx.app/errors/unauthorized",
                        403 => "https://impactx.app/errors/forbidden",
                        404 => "https://impactx.app/errors/not-found",
                        409 => "https://impactx.app/errors/conflict",
                        _ => "https://impactx.app/errors/internal-server-error"
                    },
                    Title = title,
                    Status = statusCode,
                    Detail = detail,
                    Instance = context.HttpContext.Request.Path
                };
                problem.Extensions["traceId"] = traceId;
                problem.Extensions["correlationId"] = correlationId;

                context.Result = new ObjectResult(problem)
                {
                    StatusCode = statusCode,
                    ContentTypes = { "application/problem+json" }
                };
            }
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }
}
