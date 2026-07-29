using ImpactX.Extensions;
using ImpactX.Infrastructure.Data;
using ImpactX.Middleware;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Scalar.AspNetCore;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
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

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Inicialización de Firebase Admin SDK (Notificaciones Push)
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

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseCors("AllowLocalhost");

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
