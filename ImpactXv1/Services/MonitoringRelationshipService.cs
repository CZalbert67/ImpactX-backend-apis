using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Identity;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs;
using ImpactX.Models.DTOs.Monitoring;

namespace ImpactX.Services;

public class MonitoringRelationshipService : IMonitoringRelationshipService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);

    private readonly IMonitoringRelationshipRepository _repository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IFamilySubscriptionService _familySubscriptionService;
    private readonly INotificacionRepository _notificacionRepository;

    public MonitoringRelationshipService(
        IMonitoringRelationshipRepository repository,
        IUsuarioRepository usuarioRepository,
        IFamilySubscriptionService familySubscriptionService,
        INotificacionRepository notificacionRepository)
    {
        _repository = repository;
        _usuarioRepository = usuarioRepository;
        _familySubscriptionService = familySubscriptionService;
        _notificacionRepository = notificacionRepository;
    }

    public async Task<IReadOnlyList<MonitoringRelationshipDto>> GetRelationshipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var relationships = await _repository.GetForUserAsync(userId, cancellationToken);
        var result = new List<MonitoringRelationshipDto>(relationships.Count);
        foreach (var relationship in relationships)
        {
            await ExpirePendingInvitationIfNeededAsync(relationship, cancellationToken);
            result.Add(await MapAsync(relationship));
        }

        return result;
    }

    public async Task<CreateMonitoringInvitationResponse> CreateInvitationAsync(
        Guid currentUserId,
        CreateMonitoringInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var currentUser = await GetUserAsync(currentUserId);
        var target = await ResolveTargetAsync(request);
        if (target.User?.Id == currentUserId)
        {
            throw new ConflictException("No puedes crear una relación de monitoreo contigo mismo.");
        }

        var direction = request.Direction == MonitoringRequestDirection.MonitoredRequestsMonitor
            ? MonitoringRequestDirection.MonitoredRequestsMonitor
            : MonitoringRequestDirection.MonitorInvitesMonitored;
        Usuario monitor;
        Usuario? monitored;

        if (direction == MonitoringRequestDirection.MonitoredRequestsMonitor)
        {
            monitor = target.User
                ?? throw new NotFoundException(
                    "Para solicitar un monitor, la persona debe tener una cuenta ImpactX.");
            monitored = currentUser;
        }
        else
        {
            monitor = currentUser;
            monitored = target.User;
        }

        if (monitored is not null)
        {
            if (await _repository.ExistsBlockedAsync(
                    monitor.Id,
                    monitored.Id,
                    cancellationToken))
            {
                throw new ForbiddenException("No se puede invitar a esta persona.");
            }

            if (await _repository.ExistsActiveOrPendingAsync(
                    monitor.Id,
                    monitored.Id,
                    cancellationToken))
            {
                throw new ConflictException(
                    "Ya existe una relación activa o pendiente con este usuario.");
            }

            var acceptedMonitors = await _repository.CountAcceptedForMonitoredAsync(
                monitored.Id,
                cancellationToken);
            var planName = await _familySubscriptionService.GetEffectivePlanNameAsync(
                monitored.Id,
                cancellationToken);
            if (acceptedMonitors >= FamilySubscriptionService.GetMonitoringLimit(planName))
            {
                throw new ConflictException(
                    "La persona monitoreada ya alcanzó el límite de monitores de su plan.");
            }
        }
        else
        {
            await EnsureNoDuplicatePendingTargetAsync(
                monitor.Id,
                target,
                cancellationToken);
        }

        var manualCode = FamilyPublicIdGenerator.GenerateManualCode();
        var now = DateTime.UtcNow;
        var relationship = new MonitoringRelationship
        {
            Id = Guid.NewGuid(),
            PublicRelationshipId = MonitoringPublicIdGenerator.GenerateRelationshipId(),
            MonitorUserId = monitor.Id,
            MonitoredUserId = monitored?.Id,
            InitiatedByUserId = currentUserId,
            Direction = direction,
            Status = MonitoringRelationshipStatus.Pending,
            TargetEmailNormalized = target.User is null
                ? target.EmailNormalized
                : string.IsNullOrWhiteSpace(target.User.CorreoNormalizado)
                    ? EmailNormalizer.Normalize(target.User.Correo)
                    : target.User.CorreoNormalizado,
            TargetPublicProfileId = target.User?.PublicProfileId,
            TargetUsername = target.User?.Username,
            InvitationCodeHash = InvitationCodeHasher.Hash(manualCode),
            Permissions = MapRequestedPermissions(request.Permissions),
            RequestedAtUtc = now,
            ExpiresAtUtc = now.Add(InvitationLifetime),
            UpdatedAtUtc = now
        };

        await _repository.AddAsync(relationship, cancellationToken);

        if (target.User is not null)
        {
            try
            {
                var notif = new Notificacion
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = target.User.Id,
                    Titulo = "Te han mandado una invitación",
                    Mensaje = $"El usuario @{monitor.Username} te ha enviado una invitación para ser tu monitor.",
                    Tipo = "Invitation",
                    PublicRelationshipId = relationship.PublicRelationshipId,
                    CreadoEn = DateTime.UtcNow
                };
                await _notificacionRepository.AddAsync(notif);
            }
            catch (Exception)
            {
                // Non-blocking notification fail
            }
        }

        return new CreateMonitoringInvitationResponse
        {
            Relationship = await MapAsync(relationship, monitor),
            ManualCode = manualCode
        };
    }

    public async Task AcceptAsync(
        Guid userId,
        AcceptMonitoringInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var relationship = await ResolveInvitationAsync(
            request.PublicRelationshipId,
            request.Code,
            cancellationToken);
        var acceptingUser = await GetUserAsync(userId);
        ValidateTarget(relationship, acceptingUser);
        EnsurePending(relationship);

        Guid monitorUserId;
        Guid acceptingUserId;
        if (relationship.Direction == MonitoringRequestDirection.MonitoredRequestsMonitor)
        {
            if (relationship.MonitorUserId != userId
                || !relationship.MonitoredUserId.HasValue)
            {
                throw new ForbiddenException(
                    "Esta solicitud de monitoreo no está dirigida a este usuario.");
            }

            monitorUserId = userId;
            acceptingUserId = relationship.MonitoredUserId.Value;
        }
        else
        {
            if (relationship.MonitorUserId == userId)
            {
                throw new ConflictException(
                    "No puedes aceptar una relación de monitoreo contigo mismo.");
            }

            monitorUserId = relationship.MonitorUserId;
            acceptingUserId = userId;
            relationship.MonitoredUserId = userId;
        }

        if (await _repository.ExistsBlockedAsync(
                monitorUserId,
                acceptingUserId,
                cancellationToken))
        {
            throw new ForbiddenException(
                "No se puede aceptar esta relación de monitoreo.");
        }

        var existingRelationships = await _repository.GetForUserAsync(
            monitorUserId,
            cancellationToken);
        var duplicate = existingRelationships.Any(value =>
            value.Id != relationship.Id
            && value.MonitorUserId == monitorUserId
            && value.MonitoredUserId == acceptingUserId
            && (value.Status == MonitoringRelationshipStatus.Pending
                || value.Status == MonitoringRelationshipStatus.Accepted));
        if (duplicate)
        {
            throw new ConflictException(
                "Ya existe una relación activa o pendiente con este usuario.");
        }

        var planName = await _familySubscriptionService.GetEffectivePlanNameAsync(
            acceptingUserId,
            cancellationToken);
        var limit = FamilySubscriptionService.GetMonitoringLimit(planName);
        var accepted = await _repository.CountAcceptedForMonitoredAsync(
            acceptingUserId,
            cancellationToken);
        if (accepted >= limit)
        {
            throw new ConflictException(
                "La red de monitoreo de esta persona ya alcanzó el límite del plan.");
        }

        relationship.TargetEmailNormalized = string.IsNullOrWhiteSpace(acceptingUser.CorreoNormalizado)
            ? EmailNormalizer.Normalize(acceptingUser.Correo)
            : acceptingUser.CorreoNormalizado;
        relationship.TargetPublicProfileId = acceptingUser.PublicProfileId;
        relationship.TargetUsername = acceptingUser.Username;
        relationship.Status = MonitoringRelationshipStatus.Accepted;
        relationship.AcceptedAtUtc = DateTime.UtcNow;
        relationship.UpdatedAtUtc = DateTime.UtcNow;
        relationship.InvitationCodeHash = string.Empty;
        relationship.Permissions.ViewMedicalProfile = false;
        relationship.MedicalConsentGrantedAtUtc = null;
        await _repository.UpdateAsync(relationship, cancellationToken);

        try
        {
            var notif = new Notificacion
            {
                Id = Guid.NewGuid(),
                UsuarioId = relationship.InitiatedByUserId,
                Titulo = "Invitación aceptada",
                Mensaje = $"El usuario @{acceptingUser.Username} ha aceptado tu invitación.",
                Tipo = "Accepted",
                PublicRelationshipId = relationship.PublicRelationshipId,
                CreadoEn = DateTime.UtcNow
            };
            await _notificacionRepository.AddAsync(notif);
        }
        catch (Exception)
        {
            // Non-blocking notification fail
        }
    }

    public async Task RejectAsync(
        Guid userId,
        RespondMonitoringInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var relationship = await ResolveInvitationAsync(
            request.PublicRelationshipId,
            request.Code,
            cancellationToken);
        var user = await GetUserAsync(userId);
        ValidateTarget(relationship, user);
        EnsurePending(relationship);

        relationship.Status = MonitoringRelationshipStatus.Rejected;
        relationship.UpdatedAtUtc = DateTime.UtcNow;
        relationship.InvitationCodeHash = string.Empty;
        await _repository.UpdateAsync(relationship, cancellationToken);
    }

    public async Task BlockAsync(
        Guid monitoredUserId,
        string publicRelationshipId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _repository.GetByPublicIdAsync(
            publicRelationshipId,
            cancellationToken)
            ?? throw new NotFoundException("Relación de monitoreo no encontrada.");
        if (relationship.MonitoredUserId != monitoredUserId)
        {
            throw new NotFoundException("Relación de monitoreo no encontrada.");
        }

        relationship.Status = MonitoringRelationshipStatus.Blocked;
        relationship.RevokedAtUtc = DateTime.UtcNow;
        relationship.UpdatedAtUtc = DateTime.UtcNow;
        relationship.InvitationCodeHash = string.Empty;
        relationship.Permissions = new MonitoringPermissions
        {
            ViewRoutes = false,
            ViewLocation = false,
            ViewEmergencyLocation = false,
            ViewIncidents = false,
            ReceiveCriticalAlerts = false,
            ViewMedicalProfile = false,
            SendMessages = false,
            ViewTelemetry = false,
            ReceiveNotifications = false
        };
        relationship.MedicalConsentGrantedAtUtc = null;
        await _repository.UpdateAsync(relationship, cancellationToken);
    }

    public async Task RevokeAsync(
        Guid userId,
        string publicRelationshipId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _repository.GetByPublicIdAsync(
            publicRelationshipId,
            cancellationToken)
            ?? throw new NotFoundException("Relación de monitoreo no encontrada.");
        EnsureParticipant(relationship, userId);

        if (relationship.Status is MonitoringRelationshipStatus.Revoked
            or MonitoringRelationshipStatus.Rejected
            or MonitoringRelationshipStatus.Expired)
        {
            throw new ConflictException("La relación ya no está activa.");
        }

        relationship.Status = MonitoringRelationshipStatus.Revoked;
        relationship.RevokedAtUtc = DateTime.UtcNow;
        relationship.UpdatedAtUtc = DateTime.UtcNow;
        relationship.InvitationCodeHash = string.Empty;
        await _repository.UpdateAsync(relationship, cancellationToken);
    }

    public async Task<MonitoringRelationshipDto> UpdatePermissionsAsync(
        Guid userId,
        string publicRelationshipId,
        UpdateMonitoringPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _repository.GetByPublicIdAsync(
            publicRelationshipId,
            cancellationToken)
            ?? throw new NotFoundException("Relación de monitoreo no encontrada.");
        if (relationship.Status != MonitoringRelationshipStatus.Accepted)
        {
            throw new ConflictException("La relación de monitoreo no está activa.");
        }

        if (relationship.MonitoredUserId != userId)
        {
            throw new ForbiddenException("Solo la persona monitoreada puede modificar estos permisos.");
        }

        if (request.ViewMedicalProfile && !request.ConfirmMedicalConsent)
        {
            throw new BadRequestException("Se requiere consentimiento médico explícito.");
        }

        relationship.Permissions = new MonitoringPermissions
        {
            ViewRoutes = request.ViewRoutes,
            ViewLocation = request.ViewLocation,
            ViewEmergencyLocation = request.ViewEmergencyLocation,
            ViewIncidents = request.ViewIncidents,
            ReceiveCriticalAlerts = request.ReceiveCriticalAlerts,
            ViewMedicalProfile = request.ViewMedicalProfile,
            SendMessages = request.SendMessages,
            ViewTelemetry = request.ViewTelemetry,
            ReceiveNotifications = request.ReceiveNotifications
        };
        relationship.MedicalConsentGrantedAtUtc = request.ViewMedicalProfile
            ? DateTime.UtcNow
            : null;
        relationship.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(relationship, cancellationToken);
        return await MapAsync(relationship);
    }

    public async Task<Guid> ResolveAuthorizedMonitoredUserIdAsync(
        Guid monitorUserId,
        string publicRelationshipId,
        MonitoringResourcePermission permission,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _repository.GetByPublicIdAsync(
            publicRelationshipId,
            cancellationToken)
            ?? throw new NotFoundException("Relación de monitoreo no encontrada.");
        if (relationship.Status != MonitoringRelationshipStatus.Accepted
            || relationship.MonitorUserId != monitorUserId
            || !relationship.MonitoredUserId.HasValue)
        {
            throw new NotFoundException("Relación de monitoreo no encontrada.");
        }

        var permitted = permission switch
        {
            MonitoringResourcePermission.Incidents => relationship.Permissions.ViewIncidents,
            MonitoringResourcePermission.CriticalAlerts => relationship.Permissions.ReceiveCriticalAlerts,
            MonitoringResourcePermission.Routes => relationship.Permissions.ViewRoutes,
            MonitoringResourcePermission.Telemetry => relationship.Permissions.ViewTelemetry,
            MonitoringResourcePermission.Location => relationship.Permissions.ViewLocation,
            _ => false
        };
        if (!permitted)
        {
            throw new ForbiddenException("La relación no autoriza consultar este recurso.");
        }

        return relationship.MonitoredUserId.Value;
    }

    public async Task<MedicalProfileDto> GetAuthorizedMedicalProfileAsync(
        Guid monitorUserId,
        string publicRelationshipId,
        CancellationToken cancellationToken = default)
    {
        var relationship = await _repository.GetByPublicIdAsync(
            publicRelationshipId,
            cancellationToken)
            ?? throw new NotFoundException("Relación de monitoreo no encontrada.");
        if (relationship.Status != MonitoringRelationshipStatus.Accepted
            || relationship.MonitorUserId != monitorUserId
            || !relationship.MonitoredUserId.HasValue)
        {
            throw new NotFoundException("Relación de monitoreo no encontrada.");
        }

        if (!relationship.Permissions.ViewMedicalProfile
            || !relationship.MedicalConsentGrantedAtUtc.HasValue)
        {
            throw new ForbiddenException("No existe consentimiento para consultar la ficha médica.");
        }

        var monitored = await GetUserAsync(relationship.MonitoredUserId.Value);
        var medical = monitored.FichaMedica;
        return new MedicalProfileDto
        {
            TipoSangre = medical?.TipoSangre,
            Alergias = medical?.Alergias,
            Condiciones = medical?.Condiciones,
            Medicamentos = medical?.Medicamentos,
            Nota = medical?.Nota
        };
    }

    public async Task<bool> CanMessageAsync(
        Guid senderUserId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var relationships = await _repository.GetForUserAsync(senderUserId, cancellationToken);
        return relationships.Any(relationship =>
            CanMessageBetween(relationship, senderUserId, recipientUserId));
    }

    public async Task<MonitoringRelationshipDto> GetAcceptedBetweenAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default)
    {
        var relationships = await _repository.GetForUserAsync(firstUserId, cancellationToken);
        var relationship = relationships.FirstOrDefault(value =>
            IsAcceptedBetween(value, firstUserId, secondUserId))
            ?? throw new NotFoundException("Relación de monitoreo no encontrada.");
        return await MapAsync(relationship);
    }

    private async Task<MonitoringRelationship> ResolveInvitationAsync(
        string? publicRelationshipId,
        string? code,
        CancellationToken cancellationToken)
    {
        var provided = new[]
        {
            !string.IsNullOrWhiteSpace(publicRelationshipId),
            !string.IsNullOrWhiteSpace(code)
        }.Count(value => value);
        if (provided != 1)
        {
            throw new BadRequestException("Proporciona publicRelationshipId o code, pero no ambos.");
        }

        MonitoringRelationship relationship;
        if (!string.IsNullOrWhiteSpace(publicRelationshipId))
        {
            relationship = await _repository.GetByPublicIdAsync(
                publicRelationshipId.Trim(),
                cancellationToken)
                ?? throw new NotFoundException("Invitación no encontrada.");
        }
        else
        {
            var hash = InvitationCodeHasher.Hash(code!);
            relationship = await _repository.GetByInvitationCodeHashAsync(hash, cancellationToken)
                ?? throw new NotFoundException("Código de invitación inválido o expirado.");
        }

        if (await ExpirePendingInvitationIfNeededAsync(relationship, cancellationToken))
        {
            throw new ConflictException("La invitación ya expiró.");
        }

        return relationship;
    }


    private async Task EnsureNoDuplicatePendingTargetAsync(
        Guid monitorUserId,
        ResolvedTarget target,
        CancellationToken cancellationToken)
    {
        var relationships = await _repository.GetForUserAsync(monitorUserId, cancellationToken);
        var duplicate = relationships.Any(relationship =>
            IsActiveOrPendingForTarget(relationship, monitorUserId, target));

        if (duplicate)
        {
            throw new ConflictException("Ya existe una relación activa o pendiente con este usuario.");
        }
    }

    private async Task<ResolvedTarget> ResolveTargetAsync(
        CreateMonitoringInvitationRequest request)
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
        string? email = null;
        if (!string.IsNullOrWhiteSpace(request.Username))
        {
            user = await _usuarioRepository.GetByUsernameAsync(request.Username.Trim())
                ?? throw new NotFoundException("Usuario no encontrado.");
        }
        else if (!string.IsNullOrWhiteSpace(request.PublicProfileId))
        {
            user = await _usuarioRepository.GetByPublicProfileIdAsync(request.PublicProfileId.Trim())
                ?? throw new NotFoundException("Usuario no encontrado.");
        }
        else
        {
            email = EmailNormalizer.Normalize(request.Email!);
            user = await _usuarioRepository.GetByCorreoAsync(request.Email!);
            if (user is not null)
            {
                email = user.CorreoNormalizado;
            }
        }

        return new ResolvedTarget(user, email);
    }

    private async Task<Usuario> GetUserAsync(Guid userId)
    {
        return await _usuarioRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("Usuario no encontrado.");
    }

    private async Task<MonitoringRelationshipDto> MapAsync(
        MonitoringRelationship relationship,
        Usuario? monitor = null)
    {
        monitor ??= await GetUserAsync(relationship.MonitorUserId);
        Usuario? monitored = null;
        if (relationship.MonitoredUserId.HasValue)
        {
            monitored = await _usuarioRepository.GetByIdAsync(relationship.MonitoredUserId.Value);
        }

        return new MonitoringRelationshipDto
        {
            PublicRelationshipId = relationship.PublicRelationshipId,
            Status = relationship.Status,
            Direction = relationship.Direction,
            MonitorPublicProfileId = monitor.PublicProfileId,
            MonitorUsername = monitor.Username,
            MonitorName = monitor.Nombre,
            MonitoredPublicProfileId = monitored?.PublicProfileId ?? relationship.TargetPublicProfileId,
            MonitoredUsername = monitored?.Username ?? relationship.TargetUsername,
            MonitoredName = monitored?.Nombre,
            Permissions = MapPermissions(relationship.Permissions),
            RequestedAtUtc = relationship.RequestedAtUtc,
            ExpiresAtUtc = relationship.ExpiresAtUtc,
            AcceptedAtUtc = relationship.AcceptedAtUtc,
            RevokedAtUtc = relationship.RevokedAtUtc
        };
    }

    private static MonitoringPermissions MapRequestedPermissions(
        MonitoringPermissionsRequest? request)
    {
        return new MonitoringPermissions
        {
            ViewRoutes = request?.ViewRoutes ?? true,
            ViewLocation = request?.ViewLocation ?? true,
            ViewEmergencyLocation = request?.ViewEmergencyLocation ?? true,
            ViewIncidents = request?.ViewIncidents ?? true,
            ReceiveCriticalAlerts = request?.ReceiveCriticalAlerts ?? true,
            ViewMedicalProfile = false,
            SendMessages = request?.SendMessages ?? true,
            ViewTelemetry = request?.ViewTelemetry ?? true,
            ReceiveNotifications = request?.ReceiveNotifications ?? true
        };
    }

    private static MonitoringPermissionsDto MapPermissions(MonitoringPermissions permissions)
    {
        return new MonitoringPermissionsDto
        {
            ViewRoutes = permissions.ViewRoutes,
            ViewLocation = permissions.ViewLocation,
            ViewEmergencyLocation = permissions.ViewEmergencyLocation,
            ViewIncidents = permissions.ViewIncidents,
            ReceiveCriticalAlerts = permissions.ReceiveCriticalAlerts,
            ViewMedicalProfile = permissions.ViewMedicalProfile,
            SendMessages = permissions.SendMessages,
            ViewTelemetry = permissions.ViewTelemetry,
            ReceiveNotifications = permissions.ReceiveNotifications
        };
    }

    private static void ValidateTarget(MonitoringRelationship relationship, Usuario user)
    {
        var email = string.IsNullOrWhiteSpace(user.CorreoNormalizado)
            ? EmailNormalizer.Normalize(user.Correo)
            : user.CorreoNormalizado;
        if (!RelationshipTargetsUser(relationship, user, email))
        {
            throw new ForbiddenException("Esta invitación no está dirigida a este usuario.");
        }
    }


    private static bool CanMessageBetween(
        MonitoringRelationship relationship,
        Guid senderUserId,
        Guid recipientUserId)
    {
        if (!IsAcceptedBetween(relationship, senderUserId, recipientUserId))
        {
            return false;
        }

        return relationship.Permissions.SendMessages;
    }

    private static bool IsAcceptedBetween(
        MonitoringRelationship relationship,
        Guid firstUserId,
        Guid secondUserId)
    {
        if (relationship.Status != MonitoringRelationshipStatus.Accepted)
        {
            return false;
        }

        return IsBetween(relationship, firstUserId, secondUserId);
    }

    private static bool IsBetween(
        MonitoringRelationship relationship,
        Guid firstUserId,
        Guid secondUserId)
    {
        if (relationship.MonitorUserId == firstUserId)
        {
            return relationship.MonitoredUserId == secondUserId;
        }

        return relationship.MonitorUserId == secondUserId
            && relationship.MonitoredUserId == firstUserId;
    }

    private static bool IsActiveOrPendingForTarget(
        MonitoringRelationship relationship,
        Guid monitorUserId,
        ResolvedTarget target)
    {
        if (relationship.MonitorUserId != monitorUserId)
        {
            return false;
        }

        if (!IsPendingOrAccepted(relationship.Status))
        {
            return false;
        }

        if (target.User is not null && relationship.MonitoredUserId == target.User.Id)
        {
            return true;
        }

        if (MatchesTarget(
                relationship.TargetEmailNormalized,
                target.EmailNormalized,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (target.User is null)
        {
            return false;
        }

        if (MatchesTarget(
                relationship.TargetPublicProfileId,
                target.User.PublicProfileId,
                StringComparison.Ordinal))
        {
            return true;
        }

        return MatchesTarget(
            relationship.TargetUsername,
            target.User.Username,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPendingOrAccepted(MonitoringRelationshipStatus status)
    {
        return status == MonitoringRelationshipStatus.Pending
            || status == MonitoringRelationshipStatus.Accepted;
    }

    private static bool RelationshipTargetsUser(
        MonitoringRelationship relationship,
        Usuario user,
        string email)
    {
        if (relationship.MonitoredUserId == user.Id)
        {
            return true;
        }

        if (MatchesTarget(
                relationship.TargetEmailNormalized,
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (MatchesTarget(
                relationship.TargetPublicProfileId,
                user.PublicProfileId,
                StringComparison.Ordinal))
        {
            return true;
        }

        return MatchesTarget(
            relationship.TargetUsername,
            user.Username,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesTarget(
        string? storedValue,
        string? targetValue,
        StringComparison comparison)
    {
        if (string.IsNullOrWhiteSpace(storedValue))
        {
            return false;
        }

        return string.Equals(storedValue, targetValue, comparison);
    }

    private async Task<bool> ExpirePendingInvitationIfNeededAsync(
        MonitoringRelationship relationship,
        CancellationToken cancellationToken)
    {
        if (relationship.Status != MonitoringRelationshipStatus.Pending
            || relationship.ExpiresAtUtc > DateTime.UtcNow)
        {
            return false;
        }

        relationship.Status = MonitoringRelationshipStatus.Expired;
        relationship.InvitationCodeHash = string.Empty;
        relationship.UpdatedAtUtc = DateTime.UtcNow;
        await _repository.UpdateAsync(relationship, cancellationToken);
        return true;
    }

    private static void EnsurePending(MonitoringRelationship relationship)
    {
        if (relationship.Status != MonitoringRelationshipStatus.Pending
            || relationship.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new ConflictException("La invitación ya fue procesada o expiró.");
        }
    }

    private static void EnsureParticipant(MonitoringRelationship relationship, Guid userId)
    {
        if (relationship.MonitorUserId != userId && relationship.MonitoredUserId != userId)
        {
            throw new NotFoundException("Relación de monitoreo no encontrada.");
        }
    }

    private sealed record ResolvedTarget(Usuario? User, string? EmailNormalized);
}
