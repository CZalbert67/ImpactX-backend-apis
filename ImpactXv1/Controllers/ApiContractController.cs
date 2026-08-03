using ImpactX.Core.ApiContract;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace ImpactX.Controllers;

[ApiController]
[Route("api/v1/meta")]
[AllowAnonymous]
public sealed class ApiContractController : ControllerBase
{
    private const string ContractEtag = "\"impactx-api-contract-2026.08.05\"";
    private readonly EndpointDataSource _endpointDataSource;

    public ApiContractController(EndpointDataSource endpointDataSource)
    {
        _endpointDataSource = endpointDataSource;
    }

    [HttpGet("contract")]
    [ProducesResponseType(typeof(ApiContractSnapshotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public ActionResult<ApiContractSnapshotDto> GetContract()
    {
        ApplyContractCacheHeaders();
        if (Request.Headers["If-None-Match"].Any(value =>
            string.Equals(value, ContractEtag, StringComparison.Ordinal)))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        var routes = _endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(ToRouteContracts)
            .Where(route => route.Path.StartsWith("/api/v1/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(route => route.Path, StringComparer.Ordinal)
            .ThenBy(route => route.Method, StringComparer.Ordinal)
            .ToArray();

        return Ok(new ApiContractSnapshotDto
        {
            ApiVersion = ApiContractDefinition.ApiVersion,
            ContractVersion = ApiContractDefinition.ContractVersion,
            Status = ApiContractDefinition.ContractStatus,
            LegacySunsetUtc = ApiContractDefinition.LegacySunsetUtc,
            SupportedClients = ApiContractDefinition.SupportedClients,
            CanonicalModules = ApiContractDefinition.CanonicalModules,
            LegacyModules = ApiContractDefinition.LegacyModules,
            RetentionDays = ApiContractDefinition.RetentionDays,
            Routes = routes
        });
    }

    [HttpGet("clients/{client}")]
    [ProducesResponseType(typeof(ClientCapabilityContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ClientCapabilityContractDto> GetClientCapabilities(string client)
    {
        if (!ApiContractDefinition.ClientCapabilities.TryGetValue(client, out var capabilities))
        {
            return NotFound();
        }

        Response.Headers["Cache-Control"] = "public, max-age=3600";
        return Ok(new ClientCapabilityContractDto
        {
            Client = client.Trim().ToLowerInvariant(),
            Capabilities = capabilities,
            ContractVersion = ApiContractDefinition.ContractVersion
        });
    }

    private IEnumerable<ApiRouteContractDto> ToRouteContracts(RouteEndpoint endpoint)
    {
        var rawPath = endpoint.RoutePattern.RawText;
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            yield break;
        }

        var path = rawPath.StartsWith('/') ? rawPath : $"/{rawPath}";
        var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"];
        var anonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        var allowedClients = endpoint.Metadata
            .GetMetadata<RequireClientCapabilityAttribute>()?
            .AllowedClients
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray() ?? [];

        foreach (var method in methods)
        {
            yield return new ApiRouteContractDto
            {
                Path = path,
                Method = method.ToUpperInvariant(),
                Anonymous = anonymous,
                AllowedClients = allowedClients
            };
        }
    }

    private void ApplyContractCacheHeaders()
    {
        Response.Headers["ETag"] = ContractEtag;
        Response.Headers["Cache-Control"] = "public, max-age=3600";
    }
}
