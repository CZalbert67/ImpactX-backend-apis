using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Identity;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Pagination;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class EmergencyContactService : IEmergencyContactService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private readonly IContactoRepository _repository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IFamilySubscriptionService _familySubscriptionService;
    private readonly ILogger<EmergencyContactService> _logger;

    public EmergencyContactService(
        IContactoRepository repository,
        IUsuarioRepository usuarioRepository,
        IFamilySubscriptionService familySubscriptionService,
        ILogger<EmergencyContactService> logger)
    {
        _repository = repository;
        _usuarioRepository = usuarioRepository;
        _familySubscriptionService = familySubscriptionService;
        _logger = logger;
    }

    public async Task<PagedResult<EmergencyContactDto>> GetContactsPagedAsync(
        Guid userId,
        int? pageSize,
        string? continuationToken,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);
        var size = PaginationValidator.Resolve(pageSize, continuationToken);
        var email = GetNormalizedEmail(user);
        var page = await _repository.GetV1ForUserPagedAsync(
            userId,
            email,
            size,
            continuationToken,
            cancellationToken);

        var items = new List<EmergencyContactDto>(page.Items.Count);
        foreach (var contact in page.Items)
        {
            await ExpireIfNeededAsync(contact, cancellationToken);
            items.Add(await MapAsync(contact, userId));
        }

        return new PagedResult<EmergencyContactDto>
        {
            Items = items,
            ContinuationToken = page.ContinuationToken,
            HasMoreResults = page.HasMoreResults,
            PageSize = page.PageSize
        };
    }

    public async Task<EmergencyContactDto> GetByPublicIdAsync(
        Guid userId,
        string publicContactId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);
        var contact = await GetRelationshipAsync(publicContactId, cancellationToken);
        EnsureParticipant(contact, user);
        await ExpireIfNeededAsync(contact, cancellationToken);
        return await MapAsync(contact, userId);
    }

    public async Task<CreateEmergencyContactInvitationResponse> CreateInvitationAsync(
        Guid ownerUserId,
        CreateEmergencyContactInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var owner = await GetUserAsync(ownerUserId);
        var target = await ResolveTargetAsync(request);
        if (target.User?.Id == ownerUserId
            || string.Equals(target.EmailNormalized, GetNormalizedEmail(owner), StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("No puedes agregarte a ti mismo como contacto de emergencia.");
        }

        if (await _repository.ExistsV1BlockedAsync(
                ownerUserId,
                GetNormalizedEmail(owner),
                target.User?.Id,
                target.EmailNormalized,
                cancellationToken))
        {
            throw new ForbiddenException("No se puede invitar a esta persona.");
        }

        if (await _repository.ExistsV1ActiveOrPendingAsync(
                ownerUserId,
                target.User?.Id,
                target.EmailNormalized,
                cancellationToken: cancellationToken))
        {
            throw new ConflictException("Ya existe un contacto activo o una invitación pendiente para esta persona.");
        }

        var manualCode = FamilyPublicIdGenerator.GenerateManualCode();
        var now = DateTime.UtcNow;
        var relationship = new ContactoEmergencia
        {
            UsuarioId = ownerUserId,
            PublicContactId = EmergencyContactPublicIdGenerator.Generate(),
            ContactUserId = target.User?.Id,
            TargetEmailNormalized = target.EmailNormalized,
            TargetPublicProfileId = target.User?.PublicProfileId,
            TargetUsername = target.User?.Username,
            InvitationCodeHash = InvitationCodeHasher.Hash(manualCode),
            Status = EmergencyContactStatus.Pending,
            RequestedAtUtc = now,
            ExpiresAtUtc = now.Add(InvitationLifetime),
            UpdatedAtUtc = now,
            RequestedPrimary = request.MakePrimaryWhenAccepted,
            EsPrincipal = false,
            Parentesco = NormalizeRelationship(request.Relationship),
            Priority = NormalizePriority(request.Priority),
            Nombre = target.User?.Nombre ?? string.Empty,
            Username = target.User?.Username,
            AppUserId = target.User?.PublicProfileId,
            Channel = "ImpactX internal"
        };

        await _repository.AddAsync(relationship);
        _logger.LogInformation(
            "Emergency contact invitation {PublicContactId} created for owner {OwnerUserId}",
            relationship.PublicContactId,
            ownerUserId);

        return new CreateEmergencyContactInvitationResponse
        {
            Contact = await MapAsync(relationship, ownerUserId, owner, target.User),
            ManualCode = manualCode
        };
    }

    public async Task AcceptInvitationAsync(
        Guid userId,
        RespondEmergencyContactInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var contact = await ResolveInvitationAsync(request, cancellationToken);
        var user = await GetUserAsync(userId);
        ValidateInvitationTarget(contact, user);
        EnsurePreInvitationProof(contact, request);
        await EnsurePendingAsync(contact, cancellationToken);

        var owner = await GetUserAsync(contact.UsuarioId);
        if (await _repository.ExistsV1BlockedAsync(
                contact.UsuarioId,
                GetNormalizedEmail(owner),
                userId,
                GetNormalizedEmail(user),
                cancellationToken))
        {
            throw new ForbiddenException("No se puede aceptar esta invitación.");
        }

        if (await _repository.ExistsV1ActiveOrPendingAsync(
                contact.UsuarioId,
                userId,
                GetNormalizedEmail(user),
                contact.Id,
                cancellationToken))
        {
            throw new ConflictException("Ya existe un contacto activo o una invitación pendiente con esta persona.");
        }

        var planName = await _familySubscriptionService.GetEffectivePlanNameAsync(
            contact.UsuarioId,
            cancellationToken);
        var limit = GetAcceptedContactLimit(planName);
        var accepted = await _repository.CountAcceptedByOwnerAsync(
            contact.UsuarioId,
            cancellationToken);
        if (accepted >= limit)
        {
            throw new ConflictException("La cuenta propietaria alcanzó el límite de contactos de emergencia de su plan.");
        }

        if (contact.RequestedPrimary)
        {
            await UnsetAcceptedPrimaryAsync(contact.UsuarioId, cancellationToken);
        }

        var now = DateTime.UtcNow;
        contact.ContactUserId = userId;
        contact.TargetEmailNormalized = GetNormalizedEmail(user);
        contact.TargetPublicProfileId = user.PublicProfileId;
        contact.TargetUsername = user.Username;
        contact.Nombre = user.Nombre;
        contact.Username = user.Username;
        contact.AppUserId = user.PublicProfileId;
        contact.Status = EmergencyContactStatus.Accepted;
        contact.AcceptedAtUtc = now;
        contact.UpdatedAtUtc = now;
        contact.InvitationCodeHash = null;
        contact.EsPrincipal = contact.RequestedPrimary;
        await _repository.UpdateAsync(contact);
    }

    public async Task RejectInvitationAsync(
        Guid userId,
        RespondEmergencyContactInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var contact = await ResolveInvitationAsync(request, cancellationToken);
        var user = await GetUserAsync(userId);
        ValidateInvitationTarget(contact, user);
        EnsurePreInvitationProof(contact, request);
        await EnsurePendingAsync(contact, cancellationToken);

        var now = DateTime.UtcNow;
        contact.ContactUserId ??= userId;
        contact.TargetEmailNormalized = GetNormalizedEmail(user);
        contact.TargetPublicProfileId = user.PublicProfileId;
        contact.TargetUsername = user.Username;
        contact.Nombre = user.Nombre;
        contact.Username = user.Username;
        contact.AppUserId = user.PublicProfileId;
        contact.Status = EmergencyContactStatus.Rejected;
        contact.RejectedAtUtc = now;
        contact.UpdatedAtUtc = now;
        contact.InvitationCodeHash = null;
        contact.EsPrincipal = false;
        await _repository.UpdateAsync(contact);
    }

    public async Task<EmergencyContactDto> UpdateAsync(
        Guid ownerUserId,
        string publicContactId,
        UpdateEmergencyContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var contact = await GetRelationshipAsync(publicContactId, cancellationToken);
        EnsureOwner(contact, ownerUserId);
        EnsureEditable(contact);

        if (request.Relationship is not null)
        {
            contact.Parentesco = NormalizeRelationship(request.Relationship);
        }

        if (request.Priority is not null)
        {
            contact.Priority = NormalizePriority(request.Priority);
        }

        contact.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(contact);
        return await MapAsync(contact, ownerUserId);
    }

    public async Task<EmergencyContactDto> MakePrimaryAsync(
        Guid ownerUserId,
        string publicContactId,
        CancellationToken cancellationToken = default)
    {
        var contact = await GetRelationshipAsync(publicContactId, cancellationToken);
        EnsureOwner(contact, ownerUserId);
        if (contact.Status != EmergencyContactStatus.Accepted)
        {
            throw new ConflictException("Solo un contacto aceptado puede ser principal.");
        }

        await UnsetAcceptedPrimaryAsync(ownerUserId, cancellationToken);
        contact.EsPrincipal = true;
        contact.RequestedPrimary = true;
        contact.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(contact);
        return await MapAsync(contact, ownerUserId);
    }

    public async Task RevokeAsync(
        Guid userId,
        string publicContactId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);
        var contact = await GetRelationshipAsync(publicContactId, cancellationToken);
        EnsureParticipant(contact, user);
        if (contact.Status is EmergencyContactStatus.Revoked
            or EmergencyContactStatus.Rejected
            or EmergencyContactStatus.Expired
            or EmergencyContactStatus.Blocked)
        {
            throw new ConflictException("La relación de contacto ya no está activa.");
        }

        var now = DateTime.UtcNow;
        if (contact.UsuarioId != userId)
        {
            contact.ContactUserId ??= userId;
            contact.TargetEmailNormalized = GetNormalizedEmail(user);
            contact.TargetPublicProfileId = user.PublicProfileId;
            contact.TargetUsername = user.Username;
            contact.Nombre = user.Nombre;
            contact.Username = user.Username;
            contact.AppUserId = user.PublicProfileId;
        }

        contact.Status = EmergencyContactStatus.Revoked;
        contact.RevokedAtUtc = now;
        contact.UpdatedAtUtc = now;
        contact.InvitationCodeHash = null;
        contact.EsPrincipal = false;
        await _repository.UpdateAsync(contact);
    }

    public async Task BlockAsync(
        Guid userId,
        string publicContactId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);
        var contact = await GetRelationshipAsync(publicContactId, cancellationToken);
        EnsureParticipant(contact, user);
        if (contact.Status == EmergencyContactStatus.Blocked)
        {
            throw new ConflictException("La relación ya está bloqueada.");
        }

        var now = DateTime.UtcNow;
        if (contact.UsuarioId != userId)
        {
            contact.ContactUserId ??= userId;
            contact.TargetEmailNormalized = GetNormalizedEmail(user);
            contact.TargetPublicProfileId = user.PublicProfileId;
            contact.TargetUsername = user.Username;
            contact.Nombre = user.Nombre;
            contact.Username = user.Username;
            contact.AppUserId = user.PublicProfileId;
        }

        contact.Status = EmergencyContactStatus.Blocked;
        contact.BlockedAtUtc = now;
        contact.RevokedAtUtc = now;
        contact.UpdatedAtUtc = now;
        contact.InvitationCodeHash = null;
        contact.EsPrincipal = false;
        await _repository.UpdateAsync(contact);
    }

    public async Task<EmergencyContactSyncResponse> GetSyncAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);
        var contacts = await _repository.GetV1ForUserAsync(
            userId,
            GetNormalizedEmail(user),
            cancellationToken);
        var result = new List<EmergencyContactDto>(contacts.Count);
        foreach (var contact in contacts)
        {
            await ExpireIfNeededAsync(contact, cancellationToken);
            result.Add(await MapAsync(contact, userId));
        }

        return new EmergencyContactSyncResponse
        {
            Contacts = result,
            SynchronizedAtUtc = DateTime.UtcNow
        };
    }

    internal static int GetAcceptedContactLimit(string? planName)
    {
        return planName?.Trim().ToLowerInvariant() switch
        {
            "premium" => 10,
            "basic" or "standard" => 5,
            _ => 3
        };
    }

    private async Task<ResolvedTarget> ResolveTargetAsync(
        CreateEmergencyContactInvitationRequest request)
    {
        var provided = new[]
        {
            !string.IsNullOrWhiteSpace(request.Username),
            !string.IsNullOrWhiteSpace(request.PublicProfileId),
            !string.IsNullOrWhiteSpace(request.Email)
        }.Count(value => value);
        if (provided != 1)
        {
            throw new BadRequestException("Proporciona exactamente username, publicProfileId o email.");
        }

        Usuario? user;
        string emailNormalized;
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            user = await _usuarioRepository.GetByUsernameAsync(request.Username.Trim())
                ?? throw new NotFoundException("Usuario no encontrado.");
            emailNormalized = GetNormalizedEmail(user);
        }
        else if (!string.IsNullOrWhiteSpace(request.PublicProfileId))
        {
            user = await _usuarioRepository.GetByPublicProfileIdAsync(request.PublicProfileId.Trim())
                ?? throw new NotFoundException("Usuario no encontrado.");
            emailNormalized = GetNormalizedEmail(user);
        }
        else
        {
            emailNormalized = EmailNormalizer.Normalize(request.Email);
            if (emailNormalized.Length == 0)
            {
                throw new BadRequestException("Correo inválido.");
            }

            user = await _usuarioRepository.GetByCorreoAsync(emailNormalized);
        }

        return new ResolvedTarget(user, emailNormalized);
    }

    private async Task<ContactoEmergencia> ResolveInvitationAsync(
        RespondEmergencyContactInvitationRequest request,
        CancellationToken cancellationToken)
    {
        var provided = new[]
        {
            !string.IsNullOrWhiteSpace(request.PublicContactId),
            !string.IsNullOrWhiteSpace(request.Code)
        }.Count(value => value);
        if (provided != 1)
        {
            throw new BadRequestException("Proporciona publicContactId o code, pero no ambos.");
        }

        if (!string.IsNullOrWhiteSpace(request.PublicContactId))
        {
            return await GetRelationshipAsync(request.PublicContactId.Trim(), cancellationToken);
        }

        var hash = InvitationCodeHasher.Hash(request.Code!);
        return await _repository.GetByInvitationCodeHashAsync(hash, cancellationToken)
            ?? throw new NotFoundException("Código de invitación inválido o expirado.");
    }

    private async Task<ContactoEmergencia> GetRelationshipAsync(
        string publicContactId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(publicContactId))
        {
            throw new NotFoundException("Contacto de emergencia no encontrado.");
        }

        return await _repository.GetByPublicIdAsync(publicContactId.Trim(), cancellationToken)
            ?? throw new NotFoundException("Contacto de emergencia no encontrado.");
    }

    private async Task<Usuario> GetUserAsync(Guid userId)
    {
        var user = await _usuarioRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("Usuario no encontrado.");
        if (!user.IsActive)
        {
            throw new ForbiddenException("La cuenta no está activa.");
        }

        return user;
    }

    private async Task EnsurePendingAsync(
        ContactoEmergencia contact,
        CancellationToken cancellationToken)
    {
        await ExpireIfNeededAsync(contact, cancellationToken);
        if (contact.Status != EmergencyContactStatus.Pending)
        {
            throw new ConflictException("La invitación ya fue procesada o expiró.");
        }
    }

    private async Task ExpireIfNeededAsync(
        ContactoEmergencia contact,
        CancellationToken cancellationToken)
    {
        if (contact.Status != EmergencyContactStatus.Pending
            || !contact.ExpiresAtUtc.HasValue
            || contact.ExpiresAtUtc.Value > DateTime.UtcNow)
        {
            return;
        }

        contact.Status = EmergencyContactStatus.Expired;
        contact.InvitationCodeHash = null;
        contact.EsPrincipal = false;
        contact.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(contact);
    }

    private async Task UnsetAcceptedPrimaryAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var current = await _repository.GetAcceptedPrimaryByOwnerAsync(
            ownerUserId,
            cancellationToken);
        if (current is null)
        {
            return;
        }

        current.EsPrincipal = false;
        current.RequestedPrimary = false;
        current.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(current);
    }

    private static void EnsurePreInvitationProof(
        ContactoEmergencia contact,
        RespondEmergencyContactInvitationRequest request)
    {
        if (!contact.ContactUserId.HasValue && string.IsNullOrWhiteSpace(request.Code))
        {
            throw new ForbiddenException("La preinvitación requiere el código manual de un solo uso.");
        }
    }

    private static void ValidateInvitationTarget(ContactoEmergencia contact, Usuario user)
    {
        var email = GetNormalizedEmail(user);
        var matches = contact.ContactUserId == user.Id
            || string.Equals(contact.TargetEmailNormalized, email, StringComparison.OrdinalIgnoreCase)
            || string.Equals(contact.TargetPublicProfileId, user.PublicProfileId, StringComparison.Ordinal)
            || string.Equals(contact.TargetUsername, user.Username, StringComparison.OrdinalIgnoreCase);
        if (!matches || contact.UsuarioId == user.Id)
        {
            throw new NotFoundException("Invitación no encontrada.");
        }
    }

    private static void EnsureParticipant(ContactoEmergencia contact, Usuario user)
    {
        if (contact.UsuarioId == user.Id || contact.ContactUserId == user.Id)
        {
            return;
        }

        if (contact.Status == EmergencyContactStatus.Pending
            && (string.Equals(
                    contact.TargetEmailNormalized,
                    GetNormalizedEmail(user),
                    StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    contact.TargetPublicProfileId,
                    user.PublicProfileId,
                    StringComparison.Ordinal)
                || string.Equals(
                    contact.TargetUsername,
                    user.Username,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        throw new NotFoundException("Contacto de emergencia no encontrado.");
    }

    private static void EnsureOwner(ContactoEmergencia contact, Guid ownerUserId)
    {
        if (contact.UsuarioId != ownerUserId)
        {
            throw new NotFoundException("Contacto de emergencia no encontrado.");
        }
    }

    private static void EnsureEditable(ContactoEmergencia contact)
    {
        if (contact.Status is not (EmergencyContactStatus.Pending or EmergencyContactStatus.Accepted))
        {
            throw new ConflictException("La relación de contacto ya no puede modificarse.");
        }
    }

    private async Task<EmergencyContactDto> MapAsync(
        ContactoEmergencia contact,
        Guid viewerUserId,
        Usuario? owner = null,
        Usuario? contactUser = null)
    {
        owner ??= await _usuarioRepository.GetByIdAsync(contact.UsuarioId)
            ?? throw new NotFoundException("Usuario propietario no encontrado.");
        if (contactUser is null && contact.ContactUserId.HasValue)
        {
            contactUser = await _usuarioRepository.GetByIdAsync(contact.ContactUserId.Value);
        }

        var requestedAt = contact.RequestedAtUtc ?? contact.CreadoEn;
        var expiresAt = contact.ExpiresAtUtc ?? requestedAt.Add(InvitationLifetime);
        return new EmergencyContactDto
        {
            PublicContactId = contact.PublicContactId ?? string.Empty,
            Status = contact.Status,
            IsOwner = contact.UsuarioId == viewerUserId,
            OwnerPublicProfileId = owner.PublicProfileId,
            OwnerUsername = owner.Username,
            OwnerName = owner.Nombre,
            ContactPublicProfileId = contactUser?.PublicProfileId ?? contact.TargetPublicProfileId,
            ContactUsername = contactUser?.Username ?? contact.TargetUsername,
            ContactName = contactUser?.Nombre ?? EmptyToNull(contact.Nombre),
            TargetEmailHint = MaskEmail(contact.TargetEmailNormalized),
            Relationship = contact.Parentesco,
            Priority = contact.Priority,
            IsPrimary = contact.Status == EmergencyContactStatus.Accepted && contact.EsPrincipal,
            RequestedAtUtc = requestedAt,
            ExpiresAtUtc = expiresAt,
            AcceptedAtUtc = contact.AcceptedAtUtc,
            RejectedAtUtc = contact.RejectedAtUtc,
            RevokedAtUtc = contact.RevokedAtUtc,
            BlockedAtUtc = contact.BlockedAtUtc,
            UpdatedAtUtc = contact.UpdatedAtUtc ?? contact.CreadoEn
        };
    }

    private static string NormalizePriority(string? priority)
    {
        return priority?.Trim().ToLowerInvariant() switch
        {
            "primary" or "principal" => "Primary",
            "secondary" or "secundario" or null or "" => "Secondary",
            _ => throw new BadRequestException("Priority debe ser Primary o Secondary.")
        };
    }

    private static string? NormalizeRelationship(string? relationship)
    {
        var value = relationship?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string GetNormalizedEmail(Usuario user)
    {
        return string.IsNullOrWhiteSpace(user.CorreoNormalizado)
            ? EmailNormalizer.Normalize(user.Correo)
            : user.CorreoNormalizado;
    }

    private static string? MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var separator = email.IndexOf('@');
        if (separator <= 0 || separator == email.Length - 1)
        {
            return null;
        }

        var local = email[..separator];
        var visible = local[..1];
        return visible + new string('*', Math.Min(5, Math.Max(2, local.Length - 1))) + email[separator..];
    }

    private static string? EmptyToNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record ResolvedTarget(Usuario? User, string EmailNormalized);
}
