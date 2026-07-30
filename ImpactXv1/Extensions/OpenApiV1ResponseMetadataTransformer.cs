using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi;

namespace ImpactX.Extensions;

public sealed class OpenApiV1ResponseMetadataTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;
        var path = context.Description.RelativePath;
        var document = context.Document;

        var hasAuthorize = metadata.Any(m => m is AuthorizeAttribute);
        var hasAllowAnonymous = metadata.Any(m => m is AllowAnonymousAttribute);
        var hasFromBody = context.Description.ActionDescriptor.Parameters
            .Any(p => p.BindingInfo?.BindingSource?.Id == "Body");
        var hasEnableRateLimiting = metadata.Any(m => m is EnableRateLimitingAttribute);

        var responseStatuses = new HashSet<int> { 500 };

        if (hasFromBody)
            responseStatuses.Add(400);

        if (hasAuthorize && !hasAllowAnonymous)
        {
            responseStatuses.Add(401);
            responseStatuses.Add(403);
        }

        if (path is not null && (path.Contains("{id}") || path.Contains("{*") || path.Contains("{**")))
            responseStatuses.Add(404);

        if (hasEnableRateLimiting)
            responseStatuses.Add(429);

        foreach (var produces in metadata
            .OfType<ProducesResponseTypeAttribute>()
            .Where(produces => produces.StatusCode >= 400))
        {
            responseStatuses.Add(produces.StatusCode);
        }

        operation.Responses ??= [];

        foreach (var statusCode in responseStatuses)
        {
            var key = statusCode.ToString();

            var description = statusCode switch
            {
                400 => "Bad Request",
                401 => "Unauthorized",
                403 => "Forbidden",
                404 => "Not Found",
                429 => "Too Many Requests",
                500 => "Internal Server Error",
                _ => "Error"
            };

            var response = new OpenApiResponse
            {
                Description = description,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    ["application/problem+json"] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference("ProblemDetails", document, null)
                    }
                }
            };

            operation.Responses[key] = response;
        }

        return Task.CompletedTask;
    }
}
