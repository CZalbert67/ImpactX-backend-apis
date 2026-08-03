using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IIncidentService
{
    Task<List<IncidenteListItemDto>> GetIncidentsAsync(Guid usuarioId, IncidentFilterRequest filter);
    Task<List<IncidenteListItemDto>> GetActiveIncidentsAsync(Guid usuarioId);
    Task<IncidenteDetailDto> GetIncidentDetailAsync(Guid usuarioId, Guid incidenteId);
    Task<IncidentActionResponse> ConfirmOkAsync(Guid usuarioId, Guid incidenteId);
    Task<IncidentActionResponse> CloseAsync(Guid usuarioId, Guid incidenteId, IncidentCloseRequest request);
    Task MarkAsFalseAlarmAsync(Guid usuarioId, Guid incidenteId, MarkFalseAlarmRequest request);
    Task UpdateNoteAsync(Guid usuarioId, Guid incidenteId, NoteRequest request);
    Task<MapDataDto> GetMapDataAsync(Guid usuarioId, Guid incidenteId);
    Task<byte[]> ExportAsync(Guid usuarioId, string formato);
}
