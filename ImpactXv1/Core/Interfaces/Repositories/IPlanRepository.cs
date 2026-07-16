using Prueba1.Core.Domain;

namespace Prueba1.Core.Interfaces.Repositories;

public interface IPlanRepository
{
    Task<List<Plan>> GetAllAsync();
    Task<Plan?> GetByIdAsync(Guid id);
    Task<Plan?> GetByNameAsync(string name);
}
