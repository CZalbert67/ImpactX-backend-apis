using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IIncidentService
{
    Task<List<IncidenteListItemDto>> GetIncidentsAsync(Guid usuarioId, IncidentFilterRequest filter);
    Task<IncidenteDetailDto> GetIncidentDetailAsync(Guid usuarioId, Guid incidenteId);
    Task MarkAsFalseAlarmAsync(Guid usuarioId, Guid incidenteId, MarkFalseAlarmRequest request);
    Task UpdateNoteAsync(Guid usuarioId, Guid incidenteId, NoteRequest request);
    Task<MapDataDto> GetMapDataAsync(Guid usuarioId, Guid incidenteId);
    Task<byte[]> ExportAsync(Guid usuarioId, string formato);
}
