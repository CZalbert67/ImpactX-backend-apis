using System;
using System.Threading;
using System.Threading.Tasks;

namespace ImpactX.Core.Interfaces.Services;

/// <summary>
/// Resolves how many vehicles a user is allowed to register based on
/// their active subscription plan.
/// </summary>
public interface IVehicleQuotaResolver
{
    /// <summary>
    /// Returns the maximum number of active vehicles the user may own.
    /// </summary>
    Task<int> GetMaxVehiclesAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
