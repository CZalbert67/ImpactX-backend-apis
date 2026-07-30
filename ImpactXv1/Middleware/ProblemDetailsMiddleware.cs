using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using ImpactX.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace ImpactX.Middleware;

public class ProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public ProblemDetailsMiddleware(RequestDelegate next, ILogger<ProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);

            if (context.Response.StatusCode is >= 400 and < 600 and not 204 and not 304)
            {
                var correlationId = context.Items["CorrelationId"] as string ?? context.TraceIdentifier;
                var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

                if (!context.Response.HasStarted)
                {
                    context.Response.ContentType = "application/problem+json";
                    var problem = new ProblemDetails
                    {
                        Type = context.Response.StatusCode switch
                        {
                            400 => "https://impactx.app/errors/validation",
                            401 => "https://impactx.app/errors/unauthorized",
                            403 => "https://impactx.app/errors/forbidden",
                            404 => "https://impactx.app/errors/not-found",
                            409 => "https://impactx.app/errors/conflict",
                            429 => "https://impactx.app/errors/rate-limited",
                            _ => "https://impactx.app/errors/internal-server-error"
                        },
                        Title = GetDefaultTitle(context.Response.StatusCode),
                        Status = context.Response.StatusCode,
                        Detail = context.Response.StatusCode switch
                        {
                            401 => "Authentication is required.",
                            403 => "You do not have permission to access this resource.",
                            404 => "The requested resource was not found.",
                            _ => null
                        },
                        Instance = context.Request.Path,
                        Extensions = new Dictionary<string, object?>
                        {
                            ["traceId"] = traceId,
                            ["correlationId"] = correlationId
                        }
                    };
                    var json = JsonSerializer.Serialize(problem, JsonOptions);
                    await context.Response.WriteAsync(json);
                }
            }
        }
        catch (OperationCanceledException)
            when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception while processing the request.");

            var correlationId = context.Items["CorrelationId"] as string ?? context.TraceIdentifier;
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            var (statusCode, title, detail) = MapException(ex);

            if (!context.Response.HasStarted)
            {
                context.Response.Clear();
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";

                ProblemDetails problem;

                if (ex is BadRequestException && ex.Data["Errors"] is IDictionary<string, string[]> fieldErrors)
                {
                    var validationProblem = new ValidationProblemDetails(new ModelStateDictionary())
                    {
                        Type = "https://impactx.app/errors/validation",
                        Title = title,
                        Status = statusCode,
                        Detail = detail,
                        Instance = context.Request.Path,
                    };
                    foreach (var kv in fieldErrors)
                    {
                        validationProblem.Errors[kv.Key] = kv.Value;
                    }
                    problem = validationProblem;
                }
                else
                {
                    problem = new ProblemDetails
                    {
                        Type = statusCode switch
                        {
                            400 => "https://impactx.app/errors/validation",
                            401 => "https://impactx.app/errors/unauthorized",
                            403 => "https://impactx.app/errors/forbidden",
                            404 => "https://impactx.app/errors/not-found",
                            409 => "https://impactx.app/errors/conflict",
                            429 => "https://impactx.app/errors/rate-limited",
                            _ => "https://impactx.app/errors/internal-server-error"
                        },
                        Title = title,
                        Status = statusCode,
                        Detail = detail,
                        Instance = context.Request.Path
                    };
                }

                problem.Extensions["traceId"] = traceId;
                problem.Extensions["correlationId"] = correlationId;

                var json = JsonSerializer.Serialize(problem, JsonOptions);
                await context.Response.WriteAsync(json);
            }
        }
    }

    private static (int statusCode, string title, string? detail) MapException(Exception ex)
    {
        return ex switch
        {
            BadRequestException => (400, "Validation error", ex.Message),
            UnauthorizedAccessException => (401, "Unauthorized", "Authentication is required."),
            ForbiddenException => (403, "Forbidden", ex.Message),
            NotFoundException => (404, "Not Found", ex.Message),
            KeyNotFoundException => (404, "Not Found", ex.Message),
            ConflictException => (409, "Conflict", ex.Message),
            _ => (500, "Internal Server Error", null)
        };
    }

    public static ProblemDetails CreateValidationProblemDetails(ModelStateDictionary modelState, PathString instance, string traceId, string correlationId)
    {
        var problem = new ValidationProblemDetails(modelState)
        {
            Type = "https://impactx.app/errors/validation",
            Title = "Validation error",
            Status = 400,
            Detail = "One or more fields are invalid.",
            Instance = instance
        };
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["correlationId"] = correlationId;
        return problem;
    }

    public static ProblemDetails CreateRateLimitProblemDetails(PathString instance, string traceId, string correlationId, int retryAfterSeconds)
    {
        var problem = new ProblemDetails
        {
            Type = "https://impactx.app/errors/rate-limited",
            Title = "Too Many Requests",
            Status = 429,
            Detail = $"You have exceeded the rate limit. Please retry after {retryAfterSeconds} seconds.",
            Instance = instance
        };
        problem.Extensions["traceId"] = traceId;
        problem.Extensions["correlationId"] = correlationId;
        return problem;
    }

    private static string GetDefaultTitle(int statusCode) => statusCode switch
    {
        400 => "Bad Request",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "Not Found",
        409 => "Conflict",
        429 => "Too Many Requests",
        500 => "Internal Server Error",
        _ => "Error"
    };
}
