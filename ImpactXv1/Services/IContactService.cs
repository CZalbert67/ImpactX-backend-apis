using ImpactX.Core.Exceptions;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public interface IContactService
{
    Task<List<ContactoDto>> GetContactsAsync(Guid usuarioId);
    Task<ContactoDto> GetContactByIdAsync(Guid usuarioId, Guid id);
    Task<ContactoDto> CreateContactAsync(Guid usuarioId, CreateContactoRequest request);
    Task<ContactoDto> UpdateContactAsync(Guid usuarioId, Guid id, UpdateContactoRequest request);
    Task DeleteContactAsync(Guid usuarioId, Guid id);
    Task<ContactoDto> MakePrimaryAsync(Guid usuarioId, MakePrimaryRequest request);
    Task<SyncContactosResponse> GetSyncDataAsync(Guid usuarioId);
}
