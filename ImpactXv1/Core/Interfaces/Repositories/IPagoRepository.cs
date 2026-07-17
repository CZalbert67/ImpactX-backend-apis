using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IPagoRepository
{
    Task<List<Pago>> GetByUserAsync(Guid usuarioId);
    Task<Pago?> GetByIdAsync(Guid id);
    Task AddAsync(Pago pago);
    Task UpdateAsync(Pago pago);
}
