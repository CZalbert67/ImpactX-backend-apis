using ImpactX.Core.Exceptions;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IRutaService
{
    Task<List<RutaDto>> GetFrequentAsync(Guid usuarioId);
    Task<List<RutaDto>> GetHistoryAsync(Guid usuarioId);
    Task<RutaDto> CreateAsync(Guid usuarioId, CreateRutaRequest request);
    Task<RutaDto> UpdateAsync(Guid usuarioId, Guid id, UpdateRutaRequest request);
    Task DeleteAsync(Guid usuarioId, Guid id);
    Task<RutaDto> SelectTodayAsync(Guid usuarioId, SelectTodayRequest request);
}
