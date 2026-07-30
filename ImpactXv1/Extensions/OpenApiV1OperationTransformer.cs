using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ImpactX.Extensions;

public sealed class OpenApiV1OperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var document = context.Document;
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        var hasAuthorize = metadata.Any(m => m is AuthorizeAttribute);
        var hasAllowAnonymous = metadata.Any(m => m is AllowAnonymousAttribute);

        if (hasAuthorize && !hasAllowAnonymous)
        {
            operation.Security ??= [];
            var schemeRef = new OpenApiSecuritySchemeReference("Bearer", document, null);
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                { schemeRef, [] }
            });
        }

        return Task.CompletedTask;
    }
}
