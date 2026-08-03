using ImpactX.Core.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Routing;
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
        EnrichTelemetryIngestionOperation(operation, context, metadata);
        EnrichClientCapabilities(operation, metadata);

        return Task.CompletedTask;
    }

    private static void EnrichTelemetryIngestionOperation(OpenApiOperation operation, OpenApiOperationTransformerContext context, IList<object> metadata)
    {
        var path = context.Description.RelativePath;
        if (path is null ||
            !path.EndsWith("trips/{id}/telemetry", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var esPost = metadata
            .OfType<HttpMethodAttribute>()
            .SelectMany(attribute => attribute.HttpMethods)
            .Any(method => string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase));

        if (!esPost)
            return;

        operation.Description = "Ingesta por lotes de telemetría de un viaje (1 a 100 eventos, hasta 256 KiB). La versión 1 conserva GPS/velocidad; la versión 2 agrega batchId, secuencia offline, procedencia del Galaxy Watch 8, acelerómetro, giroscopio, biometría, orientación y calidad de sensores. Cada evento requiere EventId y timestamp UTC. Los reintentos con eventos idénticos son seguros; reenviar un EventId con contenido diferente devuelve 409.";
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
    private static void EnrichClientCapabilities(OpenApiOperation operation, IList<object> metadata)
    {
        var capability = metadata.OfType<RequireClientCapabilityAttribute>().LastOrDefault();
        if (capability is null)
        {
            return;
        }

        var clients = string.Join(", ", capability.AllowedClients.OrderBy(value => value, StringComparer.Ordinal));
        var note = $"Clientes permitidos: {clients}.";
        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? note
            : $"{operation.Description}\n\n{note}";
    }

}
