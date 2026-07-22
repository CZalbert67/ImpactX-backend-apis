using ImpactX.Extensions;
using ImpactX.Infrastructure.Data;
using ImpactX.Middleware;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

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
        policy.WithOrigins("http://localhost:5173", "http://localhost:3000", "http://127.0.0.1:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

await app.SeedDatabaseAsync(useCosmosDb, useInMemory);

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseCors("AllowLocalhost");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
