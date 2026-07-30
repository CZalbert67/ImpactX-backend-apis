using System.Threading.RateLimiting;
using ImpactX.Configuration;
using ImpactX.Extensions;
using ImpactX.Filter;
using ImpactX.Infrastructure.Data;
using ImpactX.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllers(options =>
{
    options.Filters.Add<V1ProblemDetailsResultFilter>();
});
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var httpContext = context.HttpContext;
        var correlationId = httpContext.Items["CorrelationId"] as string ?? httpContext.TraceIdentifier;
        var traceId = System.Diagnostics.Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var problem = ProblemDetailsMiddleware.CreateValidationProblemDetails(
            context.ModelState,
            httpContext.Request.Path,
            traceId,
            correlationId);

        return new ObjectResult(problem)
        {
            StatusCode = 400,
            ContentTypes = { "application/problem+json" }
        };
    };
});
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi(options =>
{
    options.ShouldInclude = description =>
        description.RelativePath?.StartsWith("api/v1/", StringComparison.OrdinalIgnoreCase) == true;
    options.AddDocumentTransformer<OpenApiV1DocumentTransformer>();
    options.AddOperationTransformer<OpenApiV1OperationTransformer>();
    options.AddOperationTransformer<OpenApiV1ResponseMetadataTransformer>();
});

builder.Services.AddHealthChecks()
    .AddCheck("live", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck("ready", () => HealthCheckResult.Healthy(), tags: ["ready"]);

var useCosmosDb = builder.Configuration.GetValue<bool>("UseCosmosDb");
var useInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");

if (useCosmosDb)
{
    builder.Services.RegisterApplicationServices(builder.Configuration);
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("ImpactXDb"));
    builder.Services.RegisterApplicationServices(builder.Configuration);
}

builder.Services.ConfigureJwtAuthentication(builder.Configuration);

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

if (corsOrigins.Length == 0 && builder.Environment.IsDevelopment())
{
    corsOrigins = ["http://localhost:5173"];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCors", policy =>
    {
        if (corsOrigins.Length > 0)
        {
            policy.WithOrigins(corsOrigins)
                  .WithHeaders("Authorization", "Content-Type", "X-Correlation-Id", "Idempotency-Key", "traceparent")
                  .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD");
        }
    });
});

var rateLimitingOptions = builder.Configuration.GetSection("RateLimiting").Get<RateLimitingOptions>() ?? new();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;

    options.AddPolicy("auth-register", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Auth.RegisterPerMinute,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("auth-login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Auth.LoginPerMinute,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("auth-refresh", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Auth.RefreshPerMinute,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("auth-recover", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Auth.RecoverPerWindow,
                Window = TimeSpan.FromMinutes(rateLimitingOptions.Auth.RecoverEveryMinutes)
            }));

    options.AddPolicy("auth-reset", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Auth.ResetPerWindow,
                Window = TimeSpan.FromMinutes(rateLimitingOptions.Auth.ResetEveryMinutes)
            }));

    options.AddPolicy("monitor-invite-details", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Monitors.InviteDetailsPerMinute,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.AddPolicy("monitor-invitation-action", context =>
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Monitors.InviteActionPerMinutePerUser,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    options.AddPolicy("monitor-invite-create", context =>
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Monitors.InviteCreatePerHourPerUser,
                Window = TimeSpan.FromHours(1)
            });
    });

    options.AddPolicy("fcm-token", context =>
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Devices.FcmTokenPerMinutePerUser,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    options.AddPolicy("telemetry-ingestion", context =>
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Telemetry.IngestionPerMinutePerUser,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    options.AddPolicy("incident-create", context =>
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Incidents.CreatePerMinutePerUser,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    options.AddPolicy("alert-detect", context =>
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Alerts.DetectPerMinutePerUser,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    options.AddPolicy("alert-sos", context =>
    {
        var userId = context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: userId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitingOptions.Alerts.SosPerMinutePerUser,
                Window = TimeSpan.FromMinutes(1)
            });
    });

    options.OnRejected = async (context, cancellationToken) =>
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
            ? (int)retryAfterValue.TotalSeconds
            : 60;

        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.ContentType = "application/problem+json";
        context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();

        var correlationId = context.HttpContext.Items["CorrelationId"] as string ?? context.HttpContext.TraceIdentifier;
        var traceId = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

        var problem = ProblemDetailsMiddleware.CreateRateLimitProblemDetails(
            context.HttpContext.Request.Path,
            traceId,
            correlationId,
            retryAfter);

        await context.HttpContext.Response.WriteAsync(
            JsonSerializer.Serialize(problem, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }), cancellationToken);
    };
});

try
{
    var firebaseCredentialsFile = builder.Configuration["Firebase:CredentialsPath"] ?? "firebase-credentials.json";
    var firebaseCredentialsPath = Path.Combine(builder.Environment.ContentRootPath, firebaseCredentialsFile);

    if (File.Exists(firebaseCredentialsPath))
    {
        FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions()
        {
            Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromFile(firebaseCredentialsPath)
        });
        Console.WriteLine($"[Firebase] Inicializado con éxito desde archivo: {firebaseCredentialsPath}");
    }
    else
    {
        var credentialsEnv = Environment.GetEnvironmentVariable("FIREBASE_CREDENTIALS");
        if (!string.IsNullOrEmpty(credentialsEnv))
        {
            FirebaseAdmin.FirebaseApp.Create(new FirebaseAdmin.AppOptions()
            {
                Credential = Google.Apis.Auth.OAuth2.GoogleCredential.FromJson(credentialsEnv)
            });
            Console.WriteLine("[Firebase] Inicializado con éxito desde variable de entorno.");
        }
        else
        {
            Console.WriteLine($"[Firebase] ADVERTENCIA: Archivo de credenciales no encontrado en '{firebaseCredentialsPath}' ni en variable de entorno. Las notificaciones push de Firebase no se enviarán.");
        }
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Firebase] Error crítico al inicializar Firebase Admin SDK: {ex.Message}");
}

var app = builder.Build();

await app.SeedDatabaseAsync(useCosmosDb, useInMemory);

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ProblemDetailsMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<LegacyDeprecationMiddleware>();

app.UseCors("ApiCors");

app.UseRateLimiter();

app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ImpactX API v1");
    });
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = WriteHealthCheckResponse
});

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("live"),
    ResponseWriter = WriteHealthCheckResponse
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
    ResponseWriter = WriteHealthCheckResponse
});

app.Run();

static async Task WriteHealthCheckResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    var env = context.RequestServices.GetRequiredService<IWebHostEnvironment>();
    var response = new
    {
        status = report.Status switch
        {
            HealthStatus.Healthy => "healthy",
            HealthStatus.Degraded => "degraded",
            _ => "unhealthy"
        },
        service = "impactx-api",
        environment = env.EnvironmentName,
        timestamp = DateTimeOffset.UtcNow.ToString("o")
    };
    await context.Response.WriteAsync(JsonSerializer.Serialize(response));
}

public partial class Program { }
