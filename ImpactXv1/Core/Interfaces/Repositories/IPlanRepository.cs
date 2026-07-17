using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IPlanRepository
{
    Task<List<Plan>> GetAllAsync();
    Task<Plan?> GetByIdAsync(Guid id);
    Task<Plan?> GetByNameAsync(string name);
}
