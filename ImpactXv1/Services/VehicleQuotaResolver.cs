using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;

namespace ImpactX.Services;

public class VehicleQuotaResolver : IVehicleQuotaResolver
{
    private readonly IFamilySubscriptionRepository _familySubscriptionRepository;
    private readonly IUsuarioRepository _usuarioRepository;

    public VehicleQuotaResolver(
        IFamilySubscriptionRepository familySubscriptionRepository,
        IUsuarioRepository usuarioRepository)
    {
        _familySubscriptionRepository = familySubscriptionRepository;
        _usuarioRepository = usuarioRepository;
    }

    public async Task<int> GetMaxVehiclesAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        var family = await _familySubscriptionRepository.GetActiveByUserAsync(
            usuarioId,
            cancellationToken);
        var planName = family?.PlanName;

        if (string.IsNullOrWhiteSpace(planName))
        {
            var usuario = await _usuarioRepository.GetByIdAsync(usuarioId)
                ?? throw new NotFoundException("Usuario no encontrado.");
            planName = usuario.PlanActivo;
        }

        return NormalizePlan(planName) switch
        {
            "free" or "gratuito" or "trial" => 1,
            "basic" or "standard" or "estandar" => 3,
            "premium" or "enterprise" => int.MaxValue,
            _ => 1
        };
    }

    private static string NormalizePlan(string? plan)
    {
        return (plan ?? "Free")
            .Trim()
            .ToLowerInvariant()
            .Replace("á", "a", StringComparison.Ordinal);
    }
}
