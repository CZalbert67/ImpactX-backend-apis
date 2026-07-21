using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IWearableRepository
{
    Task<Wearable?> GetByIdAsync(Guid id);
    Task<Wearable?> GetByUsuarioIdAsync(Guid usuarioId);
    Task<List<Wearable>> GetAllByUsuarioIdAsync(Guid usuarioId);
    Task<Wearable?> GetByPairingTokenAsync(string token);
    Task<Wearable?> GetByDispositivoIdAsync(string dispositivoId);
    Task AddAsync(Wearable wearable);
    Task UpdateAsync(Wearable wearable);
    Task DeleteAsync(Wearable wearable);
}
