using Prueba1.Core.Domain;

namespace Prueba1.Core.Interfaces.Repositories;

public interface IPagoRepository
{
    Task<List<Pago>> GetByUserAsync(Guid usuarioId);
    Task<Pago?> GetByIdAsync(Guid id);
    Task AddAsync(Pago pago);
    Task UpdateAsync(Pago pago);
}
