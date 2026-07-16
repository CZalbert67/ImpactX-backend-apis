using Microsoft.EntityFrameworkCore;
using Prueba1.Core.Domain;
using Prueba1.Core.Interfaces.Repositories;

namespace Prueba1.Infrastructure.Data.Repositories.EF;

public class PlanRepository : IPlanRepository
{
    private readonly ApplicationDbContext _context;

    public PlanRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Plan>> GetAllAsync()
    {
        return await _context.Set<Plan>().ToListAsync();
    }

    public async Task<Plan?> GetByIdAsync(Guid id)
    {
        return await _context.Set<Plan>().FindAsync(id);
    }

    public async Task<Plan?> GetByNameAsync(string name)
    {
        return await _context.Set<Plan>().FirstOrDefaultAsync(p => p.Nombre == name);
    }
}
