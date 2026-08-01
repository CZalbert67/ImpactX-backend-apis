using ImpactX.Core.Exceptions;
using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IRutaService
{
    Task<List<RutaDto>> GetFrequentAsync(Guid usuarioId);
    Task<List<RutaDto>> GetHistoryAsync(Guid usuarioId);
    Task<PagedResult<RutaDto>> GetFrequentPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken);
    Task<PagedResult<RutaDto>> GetHistoryPagedAsync(Guid usuarioId, int? pageSize, string? continuationToken);
    Task<RutaDto> CreateAsync(Guid usuarioId, CreateRutaRequest request);
    Task<RutaDto> UpdateAsync(Guid usuarioId, Guid id, UpdateRutaRequest request);
    Task DeleteAsync(Guid usuarioId, Guid id);
    Task<RutaDto> SelectTodayAsync(Guid usuarioId, SelectTodayRequest request);
}
