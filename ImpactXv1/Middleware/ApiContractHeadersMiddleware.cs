using ImpactX.Core.ApiContract;

namespace ImpactX.Middleware;

public sealed class ApiContractHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public ApiContractHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers["X-ImpactX-Api-Version"] = ApiContractDefinition.ApiVersion;
            context.Response.Headers["X-ImpactX-Contract-Version"] = ApiContractDefinition.ContractVersion;
            return Task.CompletedTask;
        });

        await _next(context);
    }
}
