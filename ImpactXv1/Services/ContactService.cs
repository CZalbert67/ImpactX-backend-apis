using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class ContactService : IContactService
{
    private readonly IContactoRepository _contactoRepository;
    private readonly IPlanService _planService;
    private readonly ILogger<ContactService> _logger;

    public ContactService(
        IContactoRepository contactoRepository,
        IPlanService planService,
        ILogger<ContactService> logger)
    {
        _contactoRepository = contactoRepository;
        _planService = planService;
        _logger = logger;
    }

    public async Task<List<ContactoDto>> GetContactsAsync(Guid usuarioId)
    {
        var contactos = await _contactoRepository.GetByUserAsync(usuarioId);
        return contactos.Select(MapToDto).ToList();
    }

    public async Task<ContactoDto> GetContactByIdAsync(Guid usuarioId, Guid id)
    {
        var contacto = await _contactoRepository.GetByIdAsync(id)
            ?? throw new NotFoundException("Contacto no encontrado.");

        if (contacto.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para ver este contacto.");

        return MapToDto(contacto);
    }

    public async Task<ContactoDto> CreateContactAsync(Guid usuarioId, CreateContactoRequest request)
    {
        var suscripcion = await _planService.GetCurrentSubscriptionAsync(usuarioId);
        var planName = suscripcion?.PlanNombre ?? "Free";

        var maxContactos = planName switch
        {
            "Premium" => 10,
            "Basic" => 5,
            _ => 3,
        };

        var currentCount = await _contactoRepository.CountByUserAsync(usuarioId);
        if (currentCount >= maxContactos)
            throw new ConflictException(
                $"Límite de contactos alcanzado ({maxContactos}). Actualiza tu plan para agregar más.");

        var exists = await _contactoRepository.ExistsByTelefonoAsync(usuarioId, request.Telefono);
        if (exists)
            throw new ConflictException("Este número de teléfono ya está registrado como contacto.");

        var contacto = new ContactoEmergencia
        {
            UsuarioId = usuarioId,
            Nombre = request.Nombre,
            Telefono = request.Telefono,
            Parentesco = request.Parentesco,
            Username = request.Username,
            AppUserId = request.AppUserId,
            Priority = request.Priority,
            EsPrincipal = request.EsPrincipal,
        };

        if (contacto.EsPrincipal)
            await UnsetOtherPrimaryAsync(usuarioId);

        await _contactoRepository.AddAsync(contacto);

        _logger.LogInformation("Contacto {ContactoId} creado para usuario {UsuarioId}", contacto.Id, usuarioId);

        return MapToDto(contacto);
    }

    public async Task<ContactoDto> UpdateContactAsync(Guid usuarioId, Guid id, UpdateContactoRequest request)
    {
        var contacto = await _contactoRepository.GetByIdAsync(id)
            ?? throw new NotFoundException("Contacto no encontrado.");

        if (contacto.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar este contacto.");

        if (request.Nombre is not null)
            contacto.Nombre = request.Nombre;
        if (request.Telefono is not null)
            contacto.Telefono = request.Telefono;
        if (request.Parentesco is not null)
            contacto.Parentesco = request.Parentesco;
        if (request.Priority is not null)
            contacto.Priority = request.Priority;

        await _contactoRepository.UpdateAsync(contacto);

        return MapToDto(contacto);
    }

    public async Task DeleteContactAsync(Guid usuarioId, Guid id)
    {
        var contacto = await _contactoRepository.GetByIdAsync(id)
            ?? throw new NotFoundException("Contacto no encontrado.");

        if (contacto.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para eliminar este contacto.");

        await _contactoRepository.DeleteAsync(contacto);

        _logger.LogWarning("Contacto {ContactoId} eliminado por usuario {UsuarioId}", id, usuarioId);
    }

    public async Task<ContactoDto> MakePrimaryAsync(Guid usuarioId, MakePrimaryRequest request)
    {
        var contacto = await _contactoRepository.GetByIdAsync(request.ContactoId)
            ?? throw new NotFoundException("Contacto no encontrado.");

        if (contacto.UsuarioId != usuarioId)
            throw new ForbiddenException("No tienes permiso para modificar este contacto.");

        await UnsetOtherPrimaryAsync(usuarioId);

        contacto.EsPrincipal = true;
        await _contactoRepository.UpdateAsync(contacto);

        return MapToDto(contacto);
    }

    public async Task<SyncContactosResponse> GetSyncDataAsync(Guid usuarioId)
    {
        var contactos = await _contactoRepository.GetByUserAsync(usuarioId);
        return new SyncContactosResponse
        {
            Contactos = contactos.Select(MapToDto).ToList(),
        };
    }

    private async Task UnsetOtherPrimaryAsync(Guid usuarioId)
    {
        var currentPrincipal = await _contactoRepository.GetPrincipalAsync(usuarioId);
        if (currentPrincipal is not null)
        {
            currentPrincipal.EsPrincipal = false;
            await _contactoRepository.UpdateAsync(currentPrincipal);
        }
    }

    private static ContactoDto MapToDto(ContactoEmergencia c) => new()
    {
        Id = c.Id,
        Nombre = c.Nombre,
        Telefono = c.Telefono,
        Parentesco = c.Parentesco,
        Username = c.Username,
        AppUserId = c.AppUserId,
        Channel = c.Channel,
        Priority = c.Priority,
        EsPrincipal = c.EsPrincipal,
        CreadoEn = c.CreadoEn,
    };
}
