using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ImpactX.Core.Security;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireClientCapabilityAttribute : Attribute, IAuthorizationFilter
{
    private readonly HashSet<string> _allowedClients;

    public IReadOnlyCollection<string> AllowedClients => _allowedClients;

    public RequireClientCapabilityAttribute(params string[] allowedClients)
    {
        if (allowedClients is null || allowedClients.Length == 0)
        {
            throw new ArgumentException("Debe especificarse al menos un cliente permitido.", nameof(allowedClients));
        }

        _allowedClients = allowedClients
            .Select(ClientTypePolicy.Normalize)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        // Las operaciones con capacidad de cliente requieren la claim firmada
        // explícita. Un token legacy sin `client` no recibe permisos por defecto.
        var client = context.HttpContext.User.FindFirst("client")?.Value;
        if (string.IsNullOrWhiteSpace(client))
        {
            context.Result = new ForbidResult();
            return;
        }

        string normalizedClient;
        try
        {
            normalizedClient = ClientTypePolicy.Normalize(client);
        }
        catch (ArgumentException)
        {
            context.Result = new ForbidResult();
            return;
        }

        if (!_allowedClients.Contains(normalizedClient))
        {
            context.Result = new ForbidResult();
        }
    }
}
