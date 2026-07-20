using Microsoft.EntityFrameworkCore;
using Prueba1.Extensions;
using Prueba1.Infrastructure.Data;
using Prueba1.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

var useCosmosDb = builder.Configuration.GetValue<bool>("UseCosmosDb");
var useInMemory = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");

if (useCosmosDb)
{
    builder.Services.RegisterApplicationServices(builder.Configuration);
}
else if (useInMemory)
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseInMemoryDatabase("ImpactXDb"));
    builder.Services.RegisterApplicationServices(builder.Configuration);
}
else
{
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
    builder.Services.RegisterApplicationServices(builder.Configuration);
}

builder.Services.ConfigureJwtAuthentication(builder.Configuration);

var app = builder.Build();

if (useCosmosDb)
{
    var cosmosDb = app.Services.GetRequiredService<CosmosDbContext>();
    await cosmosDb.EnsureContainersAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ImpactX API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHealthChecks("/health");

app.Run();

public partial class Program { }
