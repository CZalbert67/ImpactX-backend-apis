using ImpactX.Core.Domain;

namespace ImpactX.Core.Interfaces.Repositories;

public interface IDispositivoRepository
{
    Task<List<Dispositivo>> GetByUsuarioIdAsync(Guid usuarioId);
    Task<List<Dispositivo>> GetActiveByUsuarioIdAsync(Guid usuarioId);
    Task<Dispositivo?> GetByIdAsync(Guid usuarioId, Guid id);
    Task<Dispositivo?> GetByDeviceIdAsync(Guid usuarioId, string deviceId);
    Task<Dispositivo?> GetByTokenFcmAsync(string tokenFcm);
    Task AddAsync(Dispositivo dispositivo);
    Task UpdateAsync(Dispositivo dispositivo);
    Task DeleteAsync(Dispositivo dispositivo);
    Task<int> DeleteAllByUsuarioIdAsync(Guid usuarioId, CancellationToken cancellationToken = default);
}
