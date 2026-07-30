using System.Collections.Concurrent;
using ImpactX.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ImpactX.Tests.Integration;

public class TestLogCapture : ILoggerProvider
{
    public ConcurrentBag<string> LogEntries { get; } = [];

    public ILogger CreateLogger(string categoryName) => new TestLogger(LogEntries);

    public void Dispose() { }

    private sealed class TestLogger(ConcurrentBag<string> logEntries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            logEntries.Add(message);
        }
    }
}

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public TestLogCapture LogCapture { get; } = new();

    public ApplicationDbContext CreateDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    public async Task<T> ExecuteInDbContextAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    public async Task ExecuteInDbContextAsync(Func<ApplicationDbContext, Task> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await action(db);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("UseCosmosDb", "false");
        builder.UseSetting("UseInMemoryDatabase", "true");
        builder.UseSetting("Jwt:Secret", "test-secret-key-that-is-at-least-32-characters-long-for-hmac");
        builder.UseSetting("Jwt:Issuer", "ImpactX-Test");
        builder.UseSetting("Jwt:Audience", "ImpactX-Client-Test");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(LogCapture);
            logging.AddConsole();
        });
    }
}
