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

        EnrichPaginationParameters(operation);

        return Task.CompletedTask;
    }

    private static void EnrichPaginationParameters(OpenApiOperation operation)
    {
        if (operation.Parameters is null)
        {
            return;
        }

        foreach (var parameter in operation.Parameters)
        {
            if (string.Equals(parameter.Name, "pageSize", StringComparison.OrdinalIgnoreCase))
            {
                parameter.Description = "Tamaño de página (1 a 100, default 20).";
                if (parameter is OpenApiParameter p)
                {
                    p.Schema ??= new OpenApiSchema { Type = JsonSchemaType.Integer };
                    if (p.Schema is OpenApiSchema schema)
                    {
                        schema.Minimum = "1";
                        schema.Maximum = "100";
                    }
                }
            }
            else if (string.Equals(parameter.Name, "continuationToken", StringComparison.OrdinalIgnoreCase))
            {
                parameter.Description = "Token opaco de continuación devuelto en el header X-Continuation-Token de la respuesta anterior. No se debe inventar ni modificar.";
                if (parameter is OpenApiParameter p2)
                {
                    p2.Schema ??= new OpenApiSchema { Type = JsonSchemaType.String };
                }
            }
        }
    }
}
