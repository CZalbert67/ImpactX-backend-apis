using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace ImpactX.Extensions;

public sealed class OpenApiV1DocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        if (document.Info is null)
        {
            document.Info = new OpenApiInfo();
        }

        document.Info.Title = "ImpactX API v1";
        document.Info.Version = "v1";
        document.Info.Description = "ImpactX Backend API — Version 1";

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        if (!document.Components.SecuritySchemes.ContainsKey("Bearer"))
        {
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Enter your JWT token"
            };
        }

        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();

        if (!document.Components.Schemas.ContainsKey("ProblemDetails"))
        {
            document.Components.Schemas["ProblemDetails"] = new OpenApiSchema
            {
                Type = JsonSchemaType.Object,
                Description = "RFC 7807 Problem Details",
                Properties = new Dictionary<string, IOpenApiSchema>
                {
                    ["type"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A URI reference identifying the problem type" },
                    ["title"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A short summary of the problem" },
                    ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Description = "The HTTP status code" },
                    ["detail"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A human-readable explanation" },
                    ["instance"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "A URI reference identifying the specific occurrence" },
                    ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "The trace identifier" },
                    ["correlationId"] = new OpenApiSchema { Type = JsonSchemaType.String, Description = "The correlation identifier" }
                }
            };
        }

        return Task.CompletedTask;
    }
}
