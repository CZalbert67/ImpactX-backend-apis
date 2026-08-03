using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Identity;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Notifications;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs.FamilySubscriptions;
using ImpactX.Models.DTOs.Monitoring;

namespace ImpactX.Services;

public class FamilySubscriptionService : IFamilySubscriptionService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(3);
    private const int MaxPendingInvitations = 20;

    private readonly IFamilySubscriptionRepository _familyRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPlanRepository _planRepository;
    private readonly INotificationService? _notificationService;

    public FamilySubscriptionService(
        IFamilySubscriptionRepository familyRepository,
        IUsuarioRepository usuarioRepository,
        IPlanRepository planRepository,
        INotificationService? notificationService = null)
    {
        _familyRepository = familyRepository;
        _usuarioRepository = usuarioRepository;
        _planRepository = planRepository;
        _notificationService = notificationService;
    }

    public async Task<FamilySubscriptionSummaryDto?> GetCurrentAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _familyRepository.GetActiveByUserAsync(
            userId,
            cancellationToken);
        if (subscription is null)
            return null;

        await ApplyLifecycleAsync(subscription, DateTime.UtcNow, cancellationToken);
        if (subscription.Status == FamilySubscriptionStatus.Expired)
        {
            return null;
        }

        await ReconcileAcceptedMembershipsAsync(subscription, cancellationToken);
        await EnsureUnifiedAccessPoliciesAsync(subscription, cancellationToken);
        return await MapSummaryAsync(subscription, userId, cancellationToken);
    }

    public async Task<FamilySubscriptionSummaryDto> ActivateAsync(
        Guid userId,
        ActivateFamilySubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var existing = await _familyRepository.GetActiveByUserAsync(userId, cancellationToken);
        if (existing is not null)
        {
            await ApplyLifecycleAsync(existing, DateTime.UtcNow, cancellationToken);
            if (existing.Status != FamilySubscriptionStatus.Expired)
                throw new ConflictException("Ya perteneces a una suscripción familiar activa.");
        }

        var user = await GetUserAsync(userId);
        var plan = await ResolvePlanAsync(request.PlanName);
        var now = DateTime.UtcNow;
        var subscription = new FamilySubscription
        {
            Id = Guid.NewGuid(),
            PublicSubscriptionId = FamilyPublicIdGenerator.GenerateSubscriptionId(),
            OwnerUserId = userId,
            PlanName = plan.Nombre,
            Status = FamilySubscriptionStatus.Active,
            PeriodStartUtc = now,
            PeriodEndUtc = now.AddMonths(1),
            NextBillingAtUtc = now.AddMonths(1),
            GraceEndsAtUtc = null,
            AutoRenew = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Memberships =
            [
                new FamilyMembership
                {
                    Id = Guid.NewGuid(),
                    PublicMembershipId = FamilyPublicIdGenerator.GenerateMembershipId(),
                    UserId = userId,
                    Role = FamilyMembershipRole.Owner,
                    Status = FamilyMembershipStatus.Active,
                    AcceptedAtUtc = now,
                    PublicProfileIdSnapshot = user.PublicProfileId,
                    UsernameSnapshot = user.Username,
                    DisplayNameSnapshot = user.Nombre
                }
            ],
            Payments = [CreatePayment(plan, now)]
        };

        await _familyRepository.AddAsync(subscription, cancellationToken);
        await SetUserPlanAsync(user, plan.Nombre);
        return await MapSummaryAsync(subscription, userId, cancellationToken);
    }

    public async Task<FamilySubscriptionSummaryDto> ChangePlanAsync(
        Guid userId,
        ChangeFamilyPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var subscription = await RequireOwnedActiveSubscriptionAsync(userId, cancellationToken);
        var plan = await ResolvePlanAsync(request.PlanName);
        var activeMembers = CountAcceptedInvitedMembers(subscription);
        var newLimit = GetMemberLimit(plan.Nombre);

        if (activeMembers > newLimit)
        {
            subscription.PendingAdjustment = true;
            subscription.PendingPlanName = plan.Nombre;
            subscription.UpdatedAtUtc = DateTime.UtcNow;
            await _familyRepository.UpdateAsync(subscription, cancellationToken);
            return await MapSummaryAsync(subscription, userId, cancellationToken);
        }

        var previousPlan = PlanNamePolicy.ToPublicName(subscription.PlanName);
        await ApplyPlanAsync(subscription, plan, cancellationToken);
        var paymentId = subscription.Payments
            .OrderByDescending(value => value.OccurredAtUtc)
            .First().PublicPaymentId;
        await NotifyUsersSafelyAsync(
            GetActiveParticipantIds(subscription),
            "Plan del grupo actualizado",
            $"El grupo cambió de {previousPlan} a {PlanNamePolicy.ToPublicName(subscription.PlanName)}.",
            "Subscription",
            "GroupPlanChanged",
            subscription.PublicSubscriptionId,
            "FamilySubscription",
            "/app/family",
            $"group-plan-changed:{subscription.PublicSubscriptionId}:{paymentId}",
            cancellationToken);
        return await MapSummaryAsync(subscription, userId, cancellationToken);
    }

    public async Task<FamilySubscriptionSummaryDto> RenewAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await RequireOwnedCurrentSubscriptionAsync(userId, cancellationToken);
        var plan = await ResolvePlanAsync(subscription.PlanName);
        var now = DateTime.UtcNow;

        subscription.Status = FamilySubscriptionStatus.Active;
        subscription.PeriodStartUtc = subscription.PeriodEndUtc > now
            ? subscription.PeriodEndUtc
            : now;
        subscription.PeriodEndUtc = subscription.PeriodStartUtc.AddMonths(1);
        subscription.NextBillingAtUtc = subscription.PeriodEndUtc;
        subscription.GraceEndsAtUtc = null;
        subscription.UpdatedAtUtc = now;
        subscription.Payments.Add(CreatePayment(plan, now));

        await _familyRepository.UpdateAsync(subscription, cancellationToken);
        var paymentId = subscription.Payments
            .OrderByDescending(value => value.OccurredAtUtc)
            .First().PublicPaymentId;
        await NotifyUsersSafelyAsync(
            GetActiveParticipantIds(subscription),
            "Plan del grupo renovado",
            $"El plan {PlanNamePolicy.ToPublicName(subscription.PlanName)} fue renovado correctamente.",
            "Subscription",
            "GroupPlanRenewed",
            subscription.PublicSubscriptionId,
            "FamilySubscription",
            "/app/family",
            $"group-plan-renewed:{subscription.PublicSubscriptionId}:{paymentId}",
            cancellationToken);
        return await MapSummaryAsync(subscription, userId, cancellationToken);
    }

    public async Task CancelAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await RequireOwnedCurrentSubscriptionAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;
        var affectedUserIds = subscription.Memberships
            .Where(value => value.Status == FamilyMembershipStatus.Active)
            .Select(value => value.UserId)
            .Append(subscription.OwnerUserId)
            .Distinct()
            .ToArray();

        subscription.Status = FamilySubscriptionStatus.Cancelled;
        subscription.PendingAdjustment = false;
        subscription.PendingPlanName = null;
        subscription.GraceEndsAtUtc = null;
        subscription.NextBillingAtUtc = null;
        subscription.AutoRenew = false;
        subscription.UpdatedAtUtc = now;

        foreach (var membership in subscription.Memberships.Where(value =>
                     value.Role == FamilyMembershipRole.Member
                     && value.Status == FamilyMembershipStatus.Active))
        {
            membership.Status = FamilyMembershipStatus.Removed;
            membership.EndedAtUtc = now;
        }

        foreach (var invitation in subscription.Invitations.Where(value =>
                     value.Status == FamilyInvitationStatus.Pending))
        {
            invitation.Status = FamilyInvitationStatus.Revoked;
            invitation.RespondedAtUtc = now;
            invitation.CodeHash = string.Empty;
        }

        subscription.AccessPolicies.Clear();
        await _familyRepository.UpdateAsync(subscription, cancellationToken);
        await ResetUserPlansAsync(affectedUserIds, cancellationToken);
        await NotifyUsersSafelyAsync(
            affectedUserIds,
            "Grupo cancelado",
            "La suscripción del grupo terminó. Tu plan Gratuito personal está activo.",
            "Subscription",
            "GroupPlanCancelled",
            subscription.PublicSubscriptionId,
            "FamilySubscription",
            "/app/family",
            $"group-cancelled:{subscription.PublicSubscriptionId}:{now:O}",
            cancellationToken);
    }

    public async Task<IReadOnlyList<FamilyMemberDto>> GetMembersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await RequireMembershipAsync(userId, cancellationToken);
        await ReconcileAcceptedMembershipsAsync(subscription, cancellationToken);
        await EnsureUnifiedAccessPoliciesAsync(subscription, cancellationToken);
        return subscription.Memberships
            .Where(value => value.Status == FamilyMembershipStatus.Active)
            .OrderBy(value => value.Role)
            .ThenBy(value => value.AcceptedAtUtc)
            .Select(MapMember)
            .ToList();
    }

    public async Task<IReadOnlyList<FamilyInvitationDto>> GetInvitationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _familyRepository.GetActiveByUserAsync(
            userId,
            cancellationToken);
        if (subscription is not null)
        {
            await ApplyLifecycleAsync(subscription, DateTime.UtcNow, cancellationToken);
            if (subscription.Status == FamilySubscriptionStatus.Expired)
                subscription = null;
        }

        // No pertenecer a un plan familiar no es un error de lectura. Las
        // invitaciones son privadas del propietario; un integrante recibe [].
        if (subscription is null || subscription.OwnerUserId != userId)
        {
            return [];
        }

        ExpireInvitations(subscription);
        await _familyRepository.UpdateAsync(subscription, cancellationToken);
        return subscription.Invitations
            .OrderByDescending(value => value.CreatedAtUtc)
            .Select(MapInvitation)
            .ToList();
    }

    public async Task<IReadOnlyList<IncomingFamilyInvitationDto>> GetIncomingInvitationsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await GetUserAsync(userId);
        var now = DateTime.UtcNow;
        var subscriptions = await _familyRepository.GetPendingInvitationsForTargetAsync(
            user.Id,
            user.Username,
            user.PublicProfileId,
            NormalizeUserEmail(user),
            now,
            cancellationToken);

        var results = new List<IncomingFamilyInvitationDto>();
        foreach (var subscription in subscriptions)
        {
            var owner = await GetUserAsync(subscription.OwnerUserId);
            results.AddRange(subscription.Invitations
                .Where(invitation =>
                    invitation.Status == FamilyInvitationStatus.Pending
                    && invitation.ExpiresAtUtc > now
                    && InvitationTargetsUser(invitation, user, NormalizeUserEmail(user)))
                .Select(invitation => MapIncomingInvitation(subscription, invitation, owner)));
        }

        return results
            .OrderByDescending(value => value.CreatedAtUtc)
            .ToList();
    }

    public async Task<CreateFamilyInvitationResponse> CreateInvitationAsync(
        Guid userId,
        CreateFamilyInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var subscription = await RequireOwnedActiveSubscriptionAsync(userId, cancellationToken);
        ExpireInvitations(subscription);
        if (subscription.PendingAdjustment)
        {
            throw new ConflictException(
                "La suscripción debe ajustar sus integrantes antes de crear nuevas invitaciones.");
        }

        var pendingCount = subscription.Invitations.Count(value =>
            value.Status == FamilyInvitationStatus.Pending);
        if (pendingCount >= MaxPendingInvitations)
        {
            throw new ConflictException("Has alcanzado el límite temporal de invitaciones pendientes.");
        }

        var memberLimit = GetMemberLimit(subscription.PlanName);
        var acceptedCount = CountAcceptedInvitedMembers(subscription);
        if (acceptedCount >= memberLimit)
        {
            throw new ConflictException("El plan ya alcanzó su límite de personas.");
        }

        if (acceptedCount + pendingCount >= memberLimit)
        {
            throw new ConflictException(
                "Todos los espacios disponibles ya están ocupados o reservados por invitaciones pendientes.");
        }

        var target = await ResolveTargetAsync(request);
        if (target.User?.Id == userId)
        {
            throw new ConflictException("No puedes invitarte a ti mismo.");
        }

        if (target.User is not null)
        {
            var activeFamily = await _familyRepository.GetActiveByUserAsync(
                target.User.Id,
                cancellationToken);
            if (activeFamily is not null
                && activeFamily.Id != subscription.Id
                && !IsSuspendablePersonalFree(activeFamily, target.User.Id))
            {
                throw new ConflictException(
                    "El usuario ya pertenece a otro grupo activo o administra integrantes propios.");
            }
        }

        EnsureNoDuplicateInvitation(subscription, target);

        var code = FamilyPublicIdGenerator.GenerateManualCode();
        var now = DateTime.UtcNow;
        var invitation = new FamilyInvitation
        {
            Id = Guid.NewGuid(),
            PublicInvitationId = FamilyPublicIdGenerator.GenerateInvitationId(),
            TargetUserId = target.User?.Id,
            TargetEmailNormalized = target.EmailNormalized,
            TargetPublicProfileId = target.User?.PublicProfileId,
            TargetUsername = target.User?.Username,
            CodeHash = InvitationCodeHasher.Hash(code),
            Status = FamilyInvitationStatus.Pending,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.Add(InvitationLifetime),
            CreateMonitoringRelationship = false
        };

        subscription.Invitations.Add(invitation);
        subscription.UpdatedAtUtc = now;
        await _familyRepository.UpdateAsync(subscription, cancellationToken);
        if (target.User is not null)
        {
            var owner = await GetUserAsync(subscription.OwnerUserId);
            await NotifySafelyAsync(new AppNotificationCommand(
                target.User.Id,
                "Invitación a grupo ImpactX",
                $"@{owner.Username} te invitó a su grupo {PlanNamePolicy.ToPublicName(subscription.PlanName)}.",
                "Invitation",
                "GroupInvitationReceived",
                null,
                invitation.PublicInvitationId,
                "FamilyInvitation",
                "/app/family",
                $"family-invitation:{invitation.PublicInvitationId}:recipient:{target.User.Id}"),
                cancellationToken);
        }

        return new CreateFamilyInvitationResponse
        {
            Invitation = MapInvitation(invitation),
            ManualCode = code
        };
    }

    public async Task AcceptInvitationAsync(
        Guid userId,
        string publicInvitationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(publicInvitationId))
        {
            throw new NotFoundException("Invitación no encontrada.");
        }

        var subscription = await _familyRepository.GetByInvitationPublicIdAsync(
            publicInvitationId.Trim(),
            cancellationToken)
            ?? throw new NotFoundException("Invitación no encontrada.");
        var invitation = subscription.Invitations.First(value =>
            value.PublicInvitationId == publicInvitationId.Trim());
        await AcceptInvitationCoreAsync(subscription, invitation, userId, cancellationToken);
    }

    public async Task RedeemInvitationCodeAsync(
        Guid userId,
        RedeemFamilyInvitationRequest request,
        CancellationToken cancellationToken = default)
    {
        var codeHash = InvitationCodeHasher.Hash(request.Code);
        if (codeHash.Length == 0)
        {
            throw new NotFoundException("Código de invitación inválido o expirado.");
        }

        var subscription = await _familyRepository.GetByInvitationCodeHashAsync(
            codeHash,
            cancellationToken)
            ?? throw new NotFoundException("Código de invitación inválido o expirado.");
        var invitation = subscription.Invitations.First(value =>
            value.CodeHash == codeHash
            && value.Status == FamilyInvitationStatus.Pending);
        await AcceptInvitationCoreAsync(subscription, invitation, userId, cancellationToken);
    }

    public async Task RejectInvitationAsync(
        Guid userId,
        string publicInvitationId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _familyRepository.GetByInvitationPublicIdAsync(
            publicInvitationId,
            cancellationToken)
            ?? throw new NotFoundException("Invitación no encontrada.");
        var invitation = subscription.Invitations.First(value =>
            value.PublicInvitationId == publicInvitationId);
        var user = await GetUserAsync(userId);
        ValidateInvitationTarget(invitation, user);
        EnsureInvitationPending(invitation);

        invitation.Status = FamilyInvitationStatus.Rejected;
        invitation.RespondedAtUtc = DateTime.UtcNow;
        invitation.CodeHash = string.Empty;
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await _familyRepository.UpdateAsync(subscription, cancellationToken);
        await NotifySafelyAsync(new AppNotificationCommand(
            subscription.OwnerUserId,
            "Invitación rechazada",
            $"@{user.Username} rechazó la invitación al grupo.",
            "Invitation",
            "GroupInvitationRejected",
            null,
            invitation.PublicInvitationId,
            "FamilyInvitation",
            "/app/family",
            $"family-invitation-rejected:{invitation.PublicInvitationId}:recipient:{subscription.OwnerUserId}"),
            cancellationToken);
    }

    public async Task RevokeInvitationAsync(
        Guid ownerUserId,
        string publicInvitationId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await RequireOwnedActiveSubscriptionAsync(
            ownerUserId,
            cancellationToken);
        var invitation = subscription.Invitations.FirstOrDefault(value =>
            value.PublicInvitationId == publicInvitationId
            && value.Status == FamilyInvitationStatus.Pending)
            ?? throw new NotFoundException("Invitación pendiente no encontrada.");

        invitation.Status = FamilyInvitationStatus.Revoked;
        invitation.RespondedAtUtc = DateTime.UtcNow;
        invitation.CodeHash = string.Empty;
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await _familyRepository.UpdateAsync(subscription, cancellationToken);

        if (invitation.TargetUserId.HasValue)
        {
            var owner = await GetUserAsync(ownerUserId);
            await NotifySafelyAsync(new AppNotificationCommand(
                invitation.TargetUserId.Value,
                "Invitación revocada",
                $"@{owner.Username} retiró la invitación para unirte a su grupo.",
                "Invitation",
                "GroupInvitationRevoked",
                null,
                invitation.PublicInvitationId,
                "FamilyInvitation",
                "/app/family",
                $"family-invitation-revoked:{invitation.PublicInvitationId}:recipient:{invitation.TargetUserId.Value}"),
                cancellationToken);
        }
    }

    public async Task RemoveMemberAsync(
        Guid ownerUserId,
        string publicMembershipId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await RequireOwnedActiveSubscriptionAsync(
            ownerUserId,
            cancellationToken);
        var membership = subscription.Memberships.FirstOrDefault(value =>
            value.PublicMembershipId == publicMembershipId
            && value.Role == FamilyMembershipRole.Member
            && value.Status == FamilyMembershipStatus.Active)
            ?? throw new NotFoundException("Membresía no encontrada.");

        membership.Status = FamilyMembershipStatus.Removed;
        membership.EndedAtUtc = DateTime.UtcNow;
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await TryApplyPendingPlanAsync(subscription, cancellationToken);
        await _familyRepository.UpdateAsync(subscription, cancellationToken);

        RemoveAccessPoliciesForUser(subscription, membership.UserId);
        await _familyRepository.UpdateAsync(subscription, cancellationToken);
        await EnsurePersonalFreePlanAsync(membership.UserId, cancellationToken);
        await NotifySafelyAsync(new AppNotificationCommand(
            membership.UserId,
            "Saliste del grupo",
            "El titular te eliminó del grupo. Tu plan Gratuito personal fue reactivado.",
            "Subscription",
            "GroupMemberRemoved",
            null,
            membership.PublicMembershipId,
            "FamilyMembership",
            "/app/family",
            $"family-member-removed:{membership.PublicMembershipId}:recipient:{membership.UserId}"),
            cancellationToken);
    }

    public async Task LeaveAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await RequireMembershipAsync(userId, cancellationToken);
        if (subscription.OwnerUserId == userId)
        {
            throw new ConflictException("El propietario debe cancelar la suscripción; no puede abandonarla.");
        }

        var membership = subscription.Memberships.First(value =>
            value.UserId == userId
            && value.Status == FamilyMembershipStatus.Active);
        membership.Status = FamilyMembershipStatus.Left;
        membership.EndedAtUtc = DateTime.UtcNow;
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await TryApplyPendingPlanAsync(subscription, cancellationToken);
        await _familyRepository.UpdateAsync(subscription, cancellationToken);

        RemoveAccessPoliciesForUser(subscription, userId);
        await _familyRepository.UpdateAsync(subscription, cancellationToken);
        await EnsurePersonalFreePlanAsync(userId, cancellationToken);
        var user = await GetUserAsync(userId);
        await NotifySafelyAsync(new AppNotificationCommand(
            subscription.OwnerUserId,
            "Un integrante abandonó el grupo",
            $"@{user.Username} abandonó el grupo.",
            "Subscription",
            "GroupMemberLeft",
            null,
            membership.PublicMembershipId,
            "FamilyMembership",
            "/app/family",
            $"family-member-left:{membership.PublicMembershipId}:recipient:{subscription.OwnerUserId}"),
            cancellationToken);
        await NotifySafelyAsync(new AppNotificationCommand(
            userId,
            "Regresaste al plan Gratuito",
            "Abandonaste el grupo y tu plan Gratuito personal fue reactivado. Conservas tu cuenta y tus datos.",
            "Subscription",
            "PersonalFreeReactivated",
            null,
            membership.PublicMembershipId,
            "FamilyMembership",
            "/app/family",
            $"personal-free-reactivated:{membership.PublicMembershipId}:recipient:{userId}"),
            cancellationToken);
    }

    public async Task<string> GetEffectivePlanNameAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var family = await _familyRepository.GetActiveByUserAsync(userId, cancellationToken);
        if (family is not null)
        {
            await ApplyLifecycleAsync(family, DateTime.UtcNow, cancellationToken);
            if (family.Status != FamilySubscriptionStatus.Expired)
                return PlanNamePolicy.ToPublicName(family.PlanName);
        }

        var user = await GetUserAsync(userId);
        return PlanNamePolicy.ToPublicName(
            string.IsNullOrWhiteSpace(user.PlanActivo) ? PlanNamePolicy.Free : user.PlanActivo);
    }

    public async Task<IReadOnlyList<MonitoringRelationshipDto>> GetUnifiedRelationshipsAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _familyRepository.GetActiveByUserAsync(userId, cancellationToken);
        if (subscription is null)
            return [];
        await ApplyLifecycleAsync(subscription, DateTime.UtcNow, cancellationToken);
        if (subscription.Status == FamilySubscriptionStatus.Expired)
            return [];
        await EnsureUnifiedAccessPoliciesAsync(subscription, cancellationToken);
        var result = new List<MonitoringRelationshipDto>();
        foreach (var policy in subscription.AccessPolicies.Where(value =>
                     value.SubjectUserId == userId || value.ViewerUserId == userId))
        {
            result.Add(await MapUnifiedRelationshipAsync(policy));
        }

        return result.OrderBy(value => value.MonitoredName).ThenBy(value => value.MonitorName).ToList();
    }

    public async Task<MonitoringRelationshipDto?> TryGetUnifiedRelationshipAsync(
        Guid participantUserId,
        string publicRelationshipId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _familyRepository.GetActiveByUserAsync(participantUserId, cancellationToken);
        if (subscription is null)
            return null;
        await EnsureUnifiedAccessPoliciesAsync(subscription, cancellationToken);
        var policy = subscription.AccessPolicies.FirstOrDefault(value =>
            value.PublicRelationshipId == publicRelationshipId
            && (value.SubjectUserId == participantUserId || value.ViewerUserId == participantUserId));
        return policy is null ? null : await MapUnifiedRelationshipAsync(policy);
    }

    public async Task<MonitoringRelationshipDto?> TryUpdateUnifiedPermissionsAsync(
        Guid subjectUserId,
        string publicRelationshipId,
        UpdateMonitoringPermissionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _familyRepository.GetActiveByUserAsync(subjectUserId, cancellationToken);
        if (subscription is null)
            return null;
        await EnsureUnifiedAccessPoliciesAsync(subscription, cancellationToken);
        var policy = subscription.AccessPolicies.FirstOrDefault(value =>
            value.PublicRelationshipId == publicRelationshipId
            && value.SubjectUserId == subjectUserId);
        if (policy is null)
            return null;
        ApplyPermissions(policy, request);
        await _familyRepository.UpdateAsync(subscription, cancellationToken);
        return await MapUnifiedRelationshipAsync(policy);
    }

    public async Task<Guid?> TryResolveUnifiedAuthorizedUserIdAsync(
        Guid viewerUserId,
        string publicRelationshipId,
        MonitoringResourcePermission permission,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _familyRepository.GetActiveByUserAsync(viewerUserId, cancellationToken);
        if (subscription is null)
            return null;
        await EnsureUnifiedAccessPoliciesAsync(subscription, cancellationToken);
        var policy = subscription.AccessPolicies.FirstOrDefault(value =>
            value.PublicRelationshipId == publicRelationshipId
            && value.ViewerUserId == viewerUserId);
        if (policy is null)
            return null;
        if (!HasPermission(policy, permission))
            throw new ForbiddenException("El integrante no autorizó consultar este recurso.");
        return policy.SubjectUserId;
    }

    public async Task<bool> CanUnifiedMembersMessageAsync(
        Guid senderUserId,
        Guid recipientUserId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _familyRepository.GetActiveByUserAsync(senderUserId, cancellationToken);
        if (subscription is null)
            return false;
        await EnsureUnifiedAccessPoliciesAsync(subscription, cancellationToken);
        return subscription.AccessPolicies.Any(value =>
            value.SubjectUserId == recipientUserId
            && value.ViewerUserId == senderUserId
            && value.Permissions.SendMessages);
    }

    public async Task<MonitoringRelationshipDto?> TryGetUnifiedRelationshipBetweenAsync(
        Guid firstUserId,
        Guid secondUserId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _familyRepository.GetActiveByUserAsync(firstUserId, cancellationToken);
        if (subscription is null)
            return null;
        await EnsureUnifiedAccessPoliciesAsync(subscription, cancellationToken);
        var policy = subscription.AccessPolicies.FirstOrDefault(value =>
            value.SubjectUserId == secondUserId && value.ViewerUserId == firstUserId);
        return policy is null ? null : await MapUnifiedRelationshipAsync(policy);
    }

    public async Task<IReadOnlyList<FamilyMemberAccessDto>> GetMemberAccessAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await RequireMembershipAsync(userId, cancellationToken);
        await EnsureUnifiedAccessPoliciesAsync(subscription, cancellationToken);
        var result = new List<FamilyMemberAccessDto>();
        foreach (var policy in subscription.AccessPolicies.Where(value => value.SubjectUserId == userId))
            result.Add(await MapAccessAsync(subscription, policy));
        return result.OrderBy(value => value.SosPriority ?? int.MaxValue).ThenBy(value => value.ViewerName).ToList();
    }

    public async Task<FamilyMemberAccessDto> UpdateMemberAccessAsync(
        Guid userId,
        string targetPublicProfileId,
        UpdateFamilyMemberAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var subscription = await RequireMembershipAsync(userId, cancellationToken);
        await EnsureUnifiedAccessPoliciesAsync(subscription, cancellationToken);
        var target = await _usuarioRepository.GetByPublicProfileIdAsync(targetPublicProfileId.Trim())
            ?? throw new NotFoundException("Integrante no encontrado.");
        var policy = subscription.AccessPolicies.FirstOrDefault(value =>
            value.SubjectUserId == userId && value.ViewerUserId == target.Id)
            ?? throw new NotFoundException("El usuario no pertenece a tu grupo activo.");
        var maxSos = GetSosContactLimit(subscription.PlanName);
        if (request.SosPriority is < 1 || request.SosPriority > maxSos)
            throw new BadRequestException($"La prioridad SOS debe estar entre 1 y {maxSos}.");
        if (request.ViewMedicalProfile && !request.ConfirmMedicalConsent)
            throw new BadRequestException("Se requiere consentimiento médico explícito.");
        var displacedSosPolicies = new List<(FamilyMemberAccessPolicy Policy, int PreviousPriority)>();
        if (request.SosPriority.HasValue)
        {
            foreach (var other in subscription.AccessPolicies.Where(value =>
                         value.SubjectUserId == userId
                         && value.Id != policy.Id
                         && value.SosPriority == request.SosPriority))
            {
                displacedSosPolicies.Add((other, other.SosPriority!.Value));
                other.SosPriority = null;
                other.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
        var previousSosPriority = policy.SosPriority;
        policy.Permissions ??= new MonitoringPermissions();
        policy.Permissions.ViewRoutes = request.ViewRoutes;
        policy.Permissions.ViewLocation = request.ViewLocation;
        policy.Permissions.ViewEmergencyLocation = request.ViewEmergencyLocation;
        policy.Permissions.ViewIncidents = request.ViewIncidents;
        policy.Permissions.ReceiveCriticalAlerts = request.ReceiveCriticalAlerts;
        policy.Permissions.ViewMedicalProfile = request.ViewMedicalProfile;
        policy.Permissions.SendMessages = request.SendMessages;
        policy.Permissions.ViewTelemetry = request.ViewTelemetry;
        policy.Permissions.ReceiveNotifications = request.ReceiveNotifications;
        policy.MedicalConsentGrantedAtUtc = request.ViewMedicalProfile ? DateTime.UtcNow : null;
        policy.SosPriority = request.SosPriority;
        policy.UpdatedAtUtc = DateTime.UtcNow;
        await _familyRepository.UpdateAsync(subscription, cancellationToken);

        var actor = await GetUserAsync(userId);
        var notification = BuildAccessUpdateNotification(
            actor,
            target,
            subscription,
            policy,
            previousSosPriority);
        await NotifySafelyAsync(notification, cancellationToken);
        foreach (var displaced in displacedSosPolicies)
        {
            var displacedTarget = await GetUserAsync(displaced.Policy.ViewerUserId);
            await NotifySafelyAsync(
                BuildAccessUpdateNotification(
                    actor,
                    displacedTarget,
                    subscription,
                    displaced.Policy,
                    displaced.PreviousPriority),
                cancellationToken);
        }
        return await MapAccessAsync(subscription, policy);
    }

    private static AppNotificationCommand BuildAccessUpdateNotification(
        Usuario actor,
        Usuario target,
        FamilySubscription subscription,
        FamilyMemberAccessPolicy policy,
        int? previousSosPriority)
    {
        var (title, message, type, eventName) = (previousSosPriority, policy.SosPriority) switch
        {
            (null, not null) => (
                "Designación como contacto SOS",
                $"@{actor.Username} te eligió como contacto SOS con prioridad {policy.SosPriority}.",
                "SOS",
                "SosContactDesignated"),
            (not null, null) => (
                "Designación SOS revocada",
                $"@{actor.Username} retiró tu designación como contacto SOS.",
                "SOS",
                "SosContactRevoked"),
            (not null, not null) when previousSosPriority != policy.SosPriority => (
                "Prioridad SOS actualizada",
                $"@{actor.Username} cambió tu prioridad SOS de {previousSosPriority} a {policy.SosPriority}.",
                "SOS",
                "SosPriorityChanged"),
            _ => (
                "Permisos del grupo actualizados",
                $"@{actor.Username} actualizó los permisos que comparte contigo.",
                "Subscription",
                "GroupPermissionsUpdated")
        };

        return new AppNotificationCommand(
            target.Id,
            title,
            message,
            type,
            eventName,
            policy.PublicRelationshipId,
            subscription.PublicSubscriptionId,
            "FamilyGroup",
            "/app/contacts",
            $"group-access:{eventName}:{policy.PublicRelationshipId}:{policy.UpdatedAtUtc:O}");
    }

    public Task<int> ProcessLifecycleAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return _familyRepository.ProcessLifecycleAsync(
            utcNow,
            (subscription, ct) => ApplyLifecycleAsync(subscription, utcNow, ct),
            cancellationToken);
    }

    private async Task AcceptInvitationCoreAsync(
        FamilySubscription subscription,
        FamilyInvitation invitation,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId);
        ValidateInvitationTarget(invitation, user);
        EnsureInvitationPending(invitation);
        if (subscription.Status != FamilySubscriptionStatus.Active)
        {
            throw new ConflictException("La suscripción familiar no está activa.");
        }

        if (subscription.PendingAdjustment)
        {
            throw new ConflictException(
                "La suscripción debe ajustar sus integrantes antes de aceptar nuevas membresías.");
        }

        var existingFamily = await _familyRepository.GetActiveByUserAsync(userId, cancellationToken);
        if (existingFamily is not null && existingFamily.Id != subscription.Id)
        {
            if (!IsSuspendablePersonalFree(existingFamily, userId))
            {
                throw new ConflictException(
                    "Ya perteneces a otro grupo activo o administras integrantes propios.");
            }

            await SuspendPersonalFreeAsync(
                existingFamily,
                subscription.PublicSubscriptionId,
                cancellationToken);
        }

        var memberLimit = GetMemberLimit(subscription.PlanName);
        if (CountAcceptedInvitedMembers(subscription) >= memberLimit)
        {
            throw new ConflictException("El plan ya alcanzó su límite de integrantes aceptados.");
        }

        var existingMembership = subscription.Memberships.FirstOrDefault(value =>
            value.UserId == userId && value.Role == FamilyMembershipRole.Member);
        var now = DateTime.UtcNow;
        if (existingMembership is null)
        {
            subscription.Memberships.Add(new FamilyMembership
            {
                Id = Guid.NewGuid(),
                PublicMembershipId = FamilyPublicIdGenerator.GenerateMembershipId(),
                UserId = userId,
                Role = FamilyMembershipRole.Member,
                Status = FamilyMembershipStatus.Active,
                InvitedAtUtc = invitation.CreatedAtUtc,
                AcceptedAtUtc = now,
                PublicProfileIdSnapshot = user.PublicProfileId,
                UsernameSnapshot = user.Username,
                DisplayNameSnapshot = user.Nombre
            });
        }
        else
        {
            existingMembership.Status = FamilyMembershipStatus.Active;
            existingMembership.AcceptedAtUtc = now;
            existingMembership.EndedAtUtc = null;
        }

        invitation.TargetUserId = user.Id;
        invitation.TargetEmailNormalized = NormalizeUserEmail(user);
        invitation.TargetPublicProfileId = user.PublicProfileId;
        invitation.TargetUsername = user.Username;
        invitation.Status = FamilyInvitationStatus.Accepted;
        invitation.RespondedAtUtc = now;
        invitation.ConsumedAtUtc = now;
        invitation.CodeHash = string.Empty;
        subscription.UpdatedAtUtc = now;

        await EnsureUnifiedAccessPoliciesAsync(subscription, cancellationToken);
        await SetUserPlanAsync(user, subscription.PlanName);
        var owner = await GetUserAsync(subscription.OwnerUserId);
        await NotifySafelyAsync(new AppNotificationCommand(
            subscription.OwnerUserId,
            "Nuevo integrante en el grupo",
            $"@{user.Username} aceptó tu invitación.",
            "Invitation",
            "GroupInvitationAccepted",
            null,
            invitation.PublicInvitationId,
            "FamilyInvitation",
            "/app/family",
            $"family-invitation-accepted:{invitation.PublicInvitationId}:recipient:{subscription.OwnerUserId}"),
            cancellationToken);
        await NotifySafelyAsync(new AppNotificationCommand(
            user.Id,
            "Ya perteneces al grupo",
            $"Ahora compartes el plan {PlanNamePolicy.ToPublicName(subscription.PlanName)} de @{owner.Username}.",
            "Subscription",
            "GroupMembershipActivated",
            null,
            invitation.PublicInvitationId,
            "FamilyMembership",
            "/app/family",
            $"family-membership-activated:{subscription.PublicSubscriptionId}:recipient:{user.Id}"),
            cancellationToken);
    }

    private async Task EnsureUnifiedAccessPoliciesAsync(
        FamilySubscription subscription,
        CancellationToken cancellationToken)
    {
        var activeIds = subscription.Memberships
            .Where(value => value.Status == FamilyMembershipStatus.Active)
            .Select(value => value.UserId)
            .Append(subscription.OwnerUserId)
            .Distinct()
            .ToArray();
        var activeSet = activeIds.ToHashSet();
        var changed = subscription.AccessPolicies.RemoveAll(value =>
            value.SubjectUserId == value.ViewerUserId
            || !activeSet.Contains(value.SubjectUserId)
            || !activeSet.Contains(value.ViewerUserId)) > 0;
        foreach (var subject in activeIds)
            foreach (var viewer in activeIds.Where(value => value != subject))
            {
                if (subscription.AccessPolicies.Any(value =>
                        value.SubjectUserId == subject && value.ViewerUserId == viewer))
                    continue;
                subscription.AccessPolicies.Add(new FamilyMemberAccessPolicy
                {
                    Id = Guid.NewGuid(),
                    PublicRelationshipId = MonitoringPublicIdGenerator.GenerateRelationshipId(),
                    SubjectUserId = subject,
                    ViewerUserId = viewer,
                    Permissions = CreateDefaultUnifiedPermissions(),
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                });
                changed = true;
            }
        if (changed)
        {
            subscription.UpdatedAtUtc = DateTime.UtcNow;
            await _familyRepository.UpdateAsync(subscription, cancellationToken);
        }
    }

    private static MonitoringPermissions CreateDefaultUnifiedPermissions() => new()
    {
        ViewRoutes = false,
        ViewLocation = false,
        ViewEmergencyLocation = true,
        ViewIncidents = true,
        ReceiveCriticalAlerts = true,
        ViewMedicalProfile = false,
        SendMessages = true,
        ViewTelemetry = false,
        ReceiveNotifications = true
    };

    private async Task<MonitoringRelationshipDto> MapUnifiedRelationshipAsync(
        FamilyMemberAccessPolicy policy)
    {
        var subject = await GetUserAsync(policy.SubjectUserId);
        var viewer = await GetUserAsync(policy.ViewerUserId);
        return new MonitoringRelationshipDto
        {
            PublicRelationshipId = policy.PublicRelationshipId,
            Status = MonitoringRelationshipStatus.Accepted,
            Direction = MonitoringRequestDirection.MonitoredRequestsMonitor,
            MonitorPublicProfileId = viewer.PublicProfileId,
            MonitorUsername = viewer.Username,
            MonitorName = viewer.Nombre,
            MonitoredPublicProfileId = subject.PublicProfileId,
            MonitoredUsername = subject.Username,
            MonitoredName = subject.Nombre,
            Permissions = MapPermissions(policy.Permissions),
            RequestedAtUtc = policy.CreatedAtUtc,
            ExpiresAtUtc = DateTime.MaxValue,
            AcceptedAtUtc = policy.CreatedAtUtc
        };
    }

    private async Task<FamilyMemberAccessDto> MapAccessAsync(
        FamilySubscription subscription,
        FamilyMemberAccessPolicy policy)
    {
        var subject = await GetUserAsync(policy.SubjectUserId);
        var viewer = await GetUserAsync(policy.ViewerUserId);
        return new FamilyMemberAccessDto
        {
            PublicRelationshipId = policy.PublicRelationshipId,
            PublicSubscriptionId = subscription.PublicSubscriptionId,
            SubjectPublicProfileId = subject.PublicProfileId,
            SubjectUsername = subject.Username,
            SubjectName = subject.Nombre,
            ViewerPublicProfileId = viewer.PublicProfileId,
            ViewerUsername = viewer.Username,
            ViewerName = viewer.Nombre,
            Permissions = MapPermissions(policy.Permissions),
            MedicalConsentGranted = policy.MedicalConsentGrantedAtUtc.HasValue,
            SosPriority = policy.SosPriority,
            UpdatedAtUtc = policy.UpdatedAtUtc
        };
    }

    private static MonitoringPermissionsDto MapPermissions(MonitoringPermissions value) => new()
    {
        ViewRoutes = value.ViewRoutes,
        ViewLocation = value.ViewLocation,
        ViewEmergencyLocation = value.ViewEmergencyLocation,
        ViewIncidents = value.ViewIncidents,
        ReceiveCriticalAlerts = value.ReceiveCriticalAlerts,
        ViewMedicalProfile = value.ViewMedicalProfile,
        SendMessages = value.SendMessages,
        ViewTelemetry = value.ViewTelemetry,
        ReceiveNotifications = value.ReceiveNotifications
    };

    private static void ApplyPermissions(
        FamilyMemberAccessPolicy policy,
        UpdateMonitoringPermissionsRequest request)
    {
        if (request.ViewMedicalProfile && !request.ConfirmMedicalConsent)
            throw new BadRequestException("Se requiere consentimiento médico explícito.");
        policy.Permissions ??= new MonitoringPermissions();
        policy.Permissions.ViewRoutes = request.ViewRoutes;
        policy.Permissions.ViewLocation = request.ViewLocation;
        policy.Permissions.ViewEmergencyLocation = request.ViewEmergencyLocation;
        policy.Permissions.ViewIncidents = request.ViewIncidents;
        policy.Permissions.ReceiveCriticalAlerts = request.ReceiveCriticalAlerts;
        policy.Permissions.ViewMedicalProfile = request.ViewMedicalProfile;
        policy.Permissions.SendMessages = request.SendMessages;
        policy.Permissions.ViewTelemetry = request.ViewTelemetry;
        policy.Permissions.ReceiveNotifications = request.ReceiveNotifications;
        policy.MedicalConsentGrantedAtUtc = request.ViewMedicalProfile ? DateTime.UtcNow : null;
        policy.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static bool HasPermission(
        FamilyMemberAccessPolicy policy,
        MonitoringResourcePermission permission) => permission switch
        {
            MonitoringResourcePermission.Incidents => policy.Permissions.ViewIncidents,
            MonitoringResourcePermission.CriticalAlerts => policy.Permissions.ReceiveCriticalAlerts,
            MonitoringResourcePermission.Routes => policy.Permissions.ViewRoutes,
            MonitoringResourcePermission.Telemetry => policy.Permissions.ViewTelemetry,
            MonitoringResourcePermission.Location => policy.Permissions.ViewLocation,
            _ => false
        };

    private static bool IsSuspendablePersonalFree(FamilySubscription subscription, Guid userId)
    {
        return subscription.OwnerUserId == userId
            && PlanNamePolicy.ToPublicName(subscription.PlanName) == PlanNamePolicy.Free
            && CountAcceptedInvitedMembers(subscription) == 0
            && subscription.Invitations.All(value =>
                value.Status != FamilyInvitationStatus.Pending || value.ExpiresAtUtc <= DateTime.UtcNow);
    }

    private async Task SuspendPersonalFreeAsync(
        FamilySubscription subscription,
        string destinationPublicSubscriptionId,
        CancellationToken cancellationToken)
    {
        subscription.Status = FamilySubscriptionStatus.Suspended;
        subscription.SuspendedForPublicSubscriptionId = destinationPublicSubscriptionId;
        subscription.SuspendedAtUtc = DateTime.UtcNow;
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await _familyRepository.UpdateAsync(subscription, cancellationToken);
    }

    private async Task EnsurePersonalFreePlanAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await GetUserAsync(userId);
        var owned = await _familyRepository.GetByOwnerAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;
        if (owned is not null && owned.Status == FamilySubscriptionStatus.Suspended)
        {
            owned.Status = FamilySubscriptionStatus.Active;
            owned.PlanName = PlanNamePolicy.Free;
            owned.SuspendedForPublicSubscriptionId = null;
            owned.SuspendedAtUtc = null;
            owned.ReactivatedAtUtc = now;
            owned.PeriodStartUtc = now;
            owned.PeriodEndUtc = now.AddMonths(1);
            owned.NextBillingAtUtc = owned.PeriodEndUtc;
            owned.UpdatedAtUtc = now;
            var ownerMembership = owned.Memberships.FirstOrDefault(value =>
                value.UserId == userId && value.Role == FamilyMembershipRole.Owner);
            if (ownerMembership is null)
            {
                owned.Memberships.Add(CreateOwnerMembership(user, now));
            }
            else
            {
                ownerMembership.Status = FamilyMembershipStatus.Active;
                ownerMembership.EndedAtUtc = null;
            }
            RemoveAccessPoliciesForUser(owned, userId, keepOwnGroup: true);
            await _familyRepository.UpdateAsync(owned, cancellationToken);
        }
        else
        {
            var active = await _familyRepository.GetActiveByUserAsync(userId, cancellationToken);
            if (active is null || active.OwnerUserId != userId)
            {
                var freePlan = await ResolvePlanAsync(PlanNamePolicy.Free);
                var fresh = new FamilySubscription
                {
                    Id = Guid.NewGuid(),
                    PublicSubscriptionId = FamilyPublicIdGenerator.GenerateSubscriptionId(),
                    OwnerUserId = userId,
                    PlanName = freePlan.Nombre,
                    Status = FamilySubscriptionStatus.Active,
                    PeriodStartUtc = now,
                    PeriodEndUtc = now.AddMonths(1),
                    NextBillingAtUtc = now.AddMonths(1),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    Memberships = [CreateOwnerMembership(user, now)],
                    Payments = [CreatePayment(freePlan, now)]
                };
                await _familyRepository.AddAsync(fresh, cancellationToken);
            }
        }
        await SetUserPlanAsync(user, PlanNamePolicy.Free);
    }

    private static FamilyMembership CreateOwnerMembership(Usuario user, DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        PublicMembershipId = FamilyPublicIdGenerator.GenerateMembershipId(),
        UserId = user.Id,
        Role = FamilyMembershipRole.Owner,
        Status = FamilyMembershipStatus.Active,
        AcceptedAtUtc = now,
        PublicProfileIdSnapshot = user.PublicProfileId,
        UsernameSnapshot = user.Username,
        DisplayNameSnapshot = user.Nombre
    };

    private static void RemoveAccessPoliciesForUser(
        FamilySubscription subscription,
        Guid userId,
        bool keepOwnGroup = false)
    {
        subscription.AccessPolicies.RemoveAll(value =>
            value.SubjectUserId == userId || value.ViewerUserId == userId);
        if (!keepOwnGroup)
            subscription.UpdatedAtUtc = DateTime.UtcNow;
    }

    private async Task NotifySafelyAsync(
        AppNotificationCommand command,
        CancellationToken cancellationToken)
    {
        if (_notificationService is null)
            return;
        try
        {
            await _notificationService.CreateAndDispatchAsync(command, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // Las notificaciones no deben revertir la operación principal.
        }
    }

    private async Task NotifyUsersSafelyAsync(
        IEnumerable<Guid> recipientUserIds,
        string title,
        string message,
        string type,
        string eventName,
        string entityId,
        string referenceType,
        string deepLink,
        string idempotencySeed,
        CancellationToken cancellationToken)
    {
        foreach (var recipientUserId in recipientUserIds.Distinct())
        {
            await NotifySafelyAsync(new AppNotificationCommand(
                recipientUserId,
                title,
                message,
                type,
                eventName,
                null,
                entityId,
                referenceType,
                deepLink,
                $"{idempotencySeed}:recipient:{recipientUserId}"),
                cancellationToken);
        }
    }

    private static IReadOnlyList<Guid> GetActiveParticipantIds(FamilySubscription subscription)
    {
        return subscription.Memberships
            .Where(value => value.Status == FamilyMembershipStatus.Active)
            .Select(value => value.UserId)
            .Append(subscription.OwnerUserId)
            .Distinct()
            .ToArray();
    }

    private async Task<FamilySubscription> RequireOwnedActiveSubscriptionAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var subscription = await _familyRepository.GetByOwnerAsync(
            ownerUserId,
            cancellationToken)
            ?? throw new NotFoundException("Suscripción familiar no encontrada.");
        await ApplyLifecycleAsync(subscription, DateTime.UtcNow, cancellationToken);
        if (subscription.Status != FamilySubscriptionStatus.Active)
        {
            throw new ConflictException("La suscripción familiar no está activa.");
        }

        return subscription;
    }

    private async Task<FamilySubscription> RequireOwnedCurrentSubscriptionAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        var subscription = await _familyRepository.GetByOwnerAsync(ownerUserId, cancellationToken)
            ?? throw new NotFoundException("Suscripción familiar no encontrada.");
        await ApplyLifecycleAsync(subscription, DateTime.UtcNow, cancellationToken);
        if (subscription.Status is not (FamilySubscriptionStatus.Active or FamilySubscriptionStatus.PastDue))
            throw new ConflictException("La suscripción familiar ya no puede administrarse.");
        return subscription;
    }

    private async Task<FamilySubscription> RequireMembershipAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var subscription = await _familyRepository.GetActiveByUserAsync(userId, cancellationToken)
            ?? throw new NotFoundException("Suscripción familiar no encontrada.");
        await ApplyLifecycleAsync(subscription, DateTime.UtcNow, cancellationToken);
        if (subscription.Status == FamilySubscriptionStatus.Expired)
            throw new NotFoundException("Suscripción familiar no encontrada.");
        return subscription;
    }

    private async Task<Plan> ResolvePlanAsync(string requestedPlan)
    {
        var canonical = NormalizePlanName(requestedPlan);
        return await _planRepository.GetByNameAsync(canonical)
            ?? throw new BadRequestException("Plan no encontrado.");
    }

    private async Task<Usuario> GetUserAsync(Guid userId)
    {
        return await _usuarioRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("Usuario no encontrado.");
    }

    private async Task ApplyPlanAsync(
        FamilySubscription subscription,
        Plan plan,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        subscription.PlanName = plan.Nombre;
        subscription.PendingAdjustment = false;
        subscription.PendingPlanName = null;
        subscription.UpdatedAtUtc = now;
        subscription.Payments.Add(CreatePayment(plan, now));
        await _familyRepository.UpdateAsync(subscription, cancellationToken);

        foreach (var membership in subscription.Memberships.Where(value =>
                     value.Status == FamilyMembershipStatus.Active))
        {
            var user = await _usuarioRepository.GetByIdAsync(membership.UserId);
            if (user is not null)
            {
                await SetUserPlanAsync(user, plan.Nombre);
            }
        }
    }

    private async Task TryApplyPendingPlanAsync(
        FamilySubscription subscription,
        CancellationToken cancellationToken)
    {
        if (!subscription.PendingAdjustment || string.IsNullOrWhiteSpace(subscription.PendingPlanName))
        {
            return;
        }

        if (CountAcceptedInvitedMembers(subscription) > GetMemberLimit(subscription.PendingPlanName))
        {
            return;
        }

        var plan = await ResolvePlanAsync(subscription.PendingPlanName);
        var now = DateTime.UtcNow;
        subscription.PlanName = plan.Nombre;
        subscription.PendingAdjustment = false;
        subscription.PendingPlanName = null;
        subscription.UpdatedAtUtc = now;
        subscription.Payments.Add(CreatePayment(plan, now));

        foreach (var membership in subscription.Memberships.Where(value =>
                     value.Status == FamilyMembershipStatus.Active))
        {
            var user = await _usuarioRepository.GetByIdAsync(membership.UserId);
            if (user is not null)
            {
                await SetUserPlanAsync(user, plan.Nombre);
            }
        }
    }

    private async Task ResetUserPlansAsync(
        IEnumerable<Guid> userIds,
        CancellationToken cancellationToken)
    {
        foreach (var userId in userIds.Distinct())
            await EnsurePersonalFreePlanAsync(userId, cancellationToken);
    }

    private async Task SetUserPlanAsync(Usuario user, string planName)
    {
        user.PlanActivo = PlanNamePolicy.ToPublicName(planName);
        await _usuarioRepository.UpdateAsync(user);
    }

    private async Task<FamilySubscriptionSummaryDto> MapSummaryAsync(
        FamilySubscription subscription,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var owner = await GetUserAsync(subscription.OwnerUserId);
        var membership = subscription.Memberships.FirstOrDefault(value =>
            value.UserId == currentUserId
            && value.Status == FamilyMembershipStatus.Active);
        var role = subscription.OwnerUserId == currentUserId
            ? FamilyMembershipRole.Owner
            : membership?.Role ?? throw new NotFoundException("Membresía no encontrada.");
        var accepted = CountAcceptedInvitedMembers(subscription);
        var pending = subscription.Invitations.Count(value =>
            value.Status == FamilyInvitationStatus.Pending
            && value.ExpiresAtUtc > DateTime.UtcNow);
        var limit = GetMemberLimit(subscription.PlanName);

        return new FamilySubscriptionSummaryDto
        {
            PublicSubscriptionId = subscription.PublicSubscriptionId,
            PlanName = PlanNamePolicy.ToPublicName(subscription.PlanName),
            Status = subscription.Status,
            CurrentUserRole = role,
            OwnerPublicProfileId = owner.PublicProfileId,
            OwnerUsername = owner.Username,
            OwnerName = owner.Nombre,
            AcceptedMembers = accepted,
            InvitedMemberLimit = limit,
            TotalActivePeople = accepted + 1,
            TotalPeopleLimit = limit + 1,
            PendingInvitationCount = pending,
            AvailableMemberSlots = Math.Max(0, limit - accepted - pending),
            VehicleLimitPerUser = GetVehicleLimit(subscription.PlanName),
            PendingAdjustment = subscription.PendingAdjustment,
            PendingPlanName = string.IsNullOrWhiteSpace(subscription.PendingPlanName)
                ? null
                : PlanNamePolicy.ToPublicName(subscription.PendingPlanName),
            PeriodStartUtc = subscription.PeriodStartUtc,
            PeriodEndUtc = subscription.PeriodEndUtc,
            NextBillingAtUtc = subscription.NextBillingAtUtc,
            GraceEndsAtUtc = subscription.GraceEndsAtUtc,
            AutoRenew = subscription.AutoRenew,
            CanManagePlan = role == FamilyMembershipRole.Owner,
            CanInviteMembers = role == FamilyMembershipRole.Owner,
            CanLeaveGroup = role == FamilyMembershipRole.Member,
            SosContactLimit = GetSosContactLimit(subscription.PlanName),
            LatestPayment = subscription.Payments
                .OrderByDescending(value => value.OccurredAtUtc)
                .Select(MapPayment)
                .FirstOrDefault()
        };
    }

    private async Task ReconcileAcceptedMembershipsAsync(
        FamilySubscription subscription,
        CancellationToken cancellationToken)
    {
        var changed = false;
        var owner = await GetUserAsync(subscription.OwnerUserId);
        var ownerMembership = subscription.Memberships.FirstOrDefault(value =>
            value.UserId == subscription.OwnerUserId
            && value.Role == FamilyMembershipRole.Owner);

        if (ownerMembership is null)
        {
            subscription.Memberships.Add(new FamilyMembership
            {
                Id = Guid.NewGuid(),
                PublicMembershipId = FamilyPublicIdGenerator.GenerateMembershipId(),
                UserId = owner.Id,
                Role = FamilyMembershipRole.Owner,
                Status = FamilyMembershipStatus.Active,
                AcceptedAtUtc = subscription.CreatedAtUtc,
                PublicProfileIdSnapshot = owner.PublicProfileId,
                UsernameSnapshot = owner.Username,
                DisplayNameSnapshot = owner.Nombre
            });
            changed = true;
        }
        else if (ownerMembership.Status != FamilyMembershipStatus.Active)
        {
            ownerMembership.Status = FamilyMembershipStatus.Active;
            ownerMembership.EndedAtUtc = null;
            ownerMembership.AcceptedAtUtc ??= subscription.CreatedAtUtc;
            changed = true;
        }

        foreach (var invitation in subscription.Invitations.Where(value =>
                     value.Status is FamilyInvitationStatus.Accepted
                         or FamilyInvitationStatus.Consumed))
        {
            var invitedUser = await ResolveAcceptedInvitationUserAsync(invitation);
            if (invitedUser is null || invitedUser.Id == subscription.OwnerUserId)
            {
                continue;
            }

            if (!invitation.TargetUserId.HasValue)
            {
                invitation.TargetUserId = invitedUser.Id;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(invitation.TargetEmailNormalized))
            {
                invitation.TargetEmailNormalized = NormalizeUserEmail(invitedUser);
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(invitation.TargetPublicProfileId))
            {
                invitation.TargetPublicProfileId = invitedUser.PublicProfileId;
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(invitation.TargetUsername))
            {
                invitation.TargetUsername = invitedUser.Username;
                changed = true;
            }

            var membership = subscription.Memberships.FirstOrDefault(value =>
                value.UserId == invitedUser.Id
                && value.Role == FamilyMembershipRole.Member);
            if (membership is null)
            {
                subscription.Memberships.Add(new FamilyMembership
                {
                    Id = Guid.NewGuid(),
                    PublicMembershipId = FamilyPublicIdGenerator.GenerateMembershipId(),
                    UserId = invitedUser.Id,
                    Role = FamilyMembershipRole.Member,
                    Status = FamilyMembershipStatus.Active,
                    InvitedAtUtc = invitation.CreatedAtUtc,
                    AcceptedAtUtc = invitation.ConsumedAtUtc
                        ?? invitation.RespondedAtUtc
                        ?? invitation.CreatedAtUtc,
                    PublicProfileIdSnapshot = invitedUser.PublicProfileId,
                    UsernameSnapshot = invitedUser.Username,
                    DisplayNameSnapshot = invitedUser.Nombre
                });
                changed = true;
                continue;
            }

            if (membership.Status == FamilyMembershipStatus.Pending)
            {
                membership.Status = FamilyMembershipStatus.Active;
                membership.EndedAtUtc = null;
                membership.AcceptedAtUtc ??= invitation.ConsumedAtUtc
                    ?? invitation.RespondedAtUtc
                    ?? invitation.CreatedAtUtc;
                changed = true;
            }
        }

        if (changed)
        {
            subscription.UpdatedAtUtc = DateTime.UtcNow;
            await _familyRepository.UpdateAsync(subscription, cancellationToken);
        }
    }

    private async Task<Usuario?> ResolveAcceptedInvitationUserAsync(
        FamilyInvitation invitation)
    {
        if (invitation.TargetUserId.HasValue)
        {
            return await _usuarioRepository.GetByIdAsync(invitation.TargetUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(invitation.TargetPublicProfileId))
        {
            return await _usuarioRepository.GetByPublicProfileIdAsync(
                invitation.TargetPublicProfileId);
        }

        if (!string.IsNullOrWhiteSpace(invitation.TargetUsername))
        {
            return await _usuarioRepository.GetByUsernameAsync(invitation.TargetUsername);
        }

        return string.IsNullOrWhiteSpace(invitation.TargetEmailNormalized)
            ? null
            : await _usuarioRepository.GetByCorreoAsync(invitation.TargetEmailNormalized);
    }

    private async Task ApplyLifecycleAsync(
        FamilySubscription subscription,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (subscription.Status == FamilySubscriptionStatus.Active
            && subscription.PeriodEndUtc <= utcNow)
        {
            subscription.Status = FamilySubscriptionStatus.PastDue;
            subscription.GraceEndsAtUtc = subscription.PeriodEndUtc.Add(GracePeriod);
            subscription.NextBillingAtUtc = subscription.PeriodEndUtc;
            subscription.UpdatedAtUtc = utcNow;
            await _familyRepository.UpdateAsync(subscription, cancellationToken);
            return;
        }

        if (subscription.Status == FamilySubscriptionStatus.PastDue
            && subscription.GraceEndsAtUtc is not null
            && subscription.GraceEndsAtUtc <= utcNow)
        {
            var affectedUserIds = subscription.Memberships
                .Where(value => value.Status == FamilyMembershipStatus.Active)
                .Select(value => value.UserId)
                .Append(subscription.OwnerUserId)
                .Distinct()
                .ToArray();

            subscription.Status = FamilySubscriptionStatus.Expired;
            subscription.NextBillingAtUtc = null;
            subscription.AutoRenew = false;
            subscription.UpdatedAtUtc = utcNow;
            foreach (var membership in subscription.Memberships.Where(value =>
                         value.Role == FamilyMembershipRole.Member
                         && value.Status == FamilyMembershipStatus.Active))
            {
                membership.Status = FamilyMembershipStatus.Expired;
                membership.EndedAtUtc = utcNow;
            }
            foreach (var invitation in subscription.Invitations.Where(value =>
                         value.Status == FamilyInvitationStatus.Pending))
            {
                invitation.Status = FamilyInvitationStatus.Expired;
                invitation.RespondedAtUtc = utcNow;
                invitation.CodeHash = string.Empty;
            }

            subscription.AccessPolicies.Clear();
            await _familyRepository.UpdateAsync(subscription, cancellationToken);
            await ResetUserPlansAsync(affectedUserIds, cancellationToken);
            await NotifyUsersSafelyAsync(
                affectedUserIds,
                "Grupo vencido",
                "La suscripción venció. Tu plan Gratuito personal está activo.",
                "Subscription",
                "GroupPlanExpired",
                subscription.PublicSubscriptionId,
                "FamilySubscription",
                "/app/family",
                $"group-expired:{subscription.PublicSubscriptionId}:{utcNow:O}",
                cancellationToken);
        }
    }

    private static void EnsureInvitationPending(FamilyInvitation invitation)
    {
        if (invitation.Status != FamilyInvitationStatus.Pending
            || invitation.ConsumedAtUtc is not null
            || invitation.ExpiresAtUtc <= DateTime.UtcNow)
        {
            throw new ConflictException("La invitación ya fue procesada o expiró.");
        }
    }

    private static void ValidateInvitationTarget(FamilyInvitation invitation, Usuario user)
    {
        var email = NormalizeUserEmail(user);
        if (!InvitationTargetsUser(invitation, user, email))
        {
            throw new ForbiddenException("Esta invitación no está dirigida a este usuario.");
        }
    }

    private static bool InvitationTargetsUser(
        FamilyInvitation invitation,
        Usuario user,
        string email)
    {
        if (invitation.TargetUserId == user.Id)
        {
            return true;
        }

        if (MatchesTarget(
                invitation.TargetEmailNormalized,
                email,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (MatchesTarget(
                invitation.TargetPublicProfileId,
                user.PublicProfileId,
                StringComparison.Ordinal))
        {
            return true;
        }

        return MatchesTarget(
            invitation.TargetUsername,
            user.Username,
            StringComparison.OrdinalIgnoreCase);
    }

    private static void ExpireInvitations(FamilySubscription subscription)
    {
        var now = DateTime.UtcNow;
        foreach (var invitation in subscription.Invitations.Where(value =>
                     value.Status == FamilyInvitationStatus.Pending
                     && value.ExpiresAtUtc <= now))
        {
            invitation.Status = FamilyInvitationStatus.Expired;
            invitation.RespondedAtUtc = now;
            invitation.CodeHash = string.Empty;
        }
    }

    private static void EnsureNoDuplicateInvitation(
        FamilySubscription subscription,
        ResolvedInvitationTarget target)
    {
        var duplicateMember = target.User is not null && subscription.Memberships.Any(value =>
            value.UserId == target.User.Id
            && value.Status == FamilyMembershipStatus.Active);
        var targetEmail = target.User is null
            ? target.EmailNormalized
            : NormalizeUserEmail(target.User);
        var duplicateInvitation = subscription.Invitations.Any(value =>
            IsPendingInvitationForTarget(value, target, targetEmail));

        if (duplicateMember || duplicateInvitation)
        {
            throw new ConflictException("Ya existe una membresía o invitación pendiente para esta persona.");
        }
    }


    private static bool IsPendingInvitationForTarget(
        FamilyInvitation invitation,
        ResolvedInvitationTarget target,
        string? targetEmail)
    {
        if (invitation.Status != FamilyInvitationStatus.Pending)
        {
            return false;
        }

        if (target.User is not null && invitation.TargetUserId == target.User.Id)
        {
            return true;
        }

        if (MatchesTarget(
                invitation.TargetEmailNormalized,
                targetEmail,
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (target.User is null)
        {
            return false;
        }

        if (MatchesTarget(
                invitation.TargetPublicProfileId,
                target.User.PublicProfileId,
                StringComparison.Ordinal))
        {
            return true;
        }

        return MatchesTarget(
            invitation.TargetUsername,
            target.User.Username,
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

    private async Task<ResolvedInvitationTarget> ResolveTargetAsync(
        CreateFamilyInvitationRequest request)
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
        string? emailNormalized = null;
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
            emailNormalized = EmailNormalizer.Normalize(request.Email!);
            user = await _usuarioRepository.GetByCorreoAsync(request.Email!);
            if (user is not null)
            {
                emailNormalized = user.CorreoNormalizado;
            }
        }

        return new ResolvedInvitationTarget(user, emailNormalized);
    }

    private static string NormalizeUserEmail(Usuario user)
    {
        return string.IsNullOrWhiteSpace(user.CorreoNormalizado)
            ? EmailNormalizer.Normalize(user.Correo)
            : user.CorreoNormalizado;
    }

    private static SimulatedPaymentRecord CreatePayment(Plan plan, DateTime now)
    {
        return new SimulatedPaymentRecord
        {
            Id = Guid.NewGuid(),
            PublicPaymentId = FamilyPublicIdGenerator.GeneratePaymentId(),
            Result = "Approved",
            PlanName = plan.Nombre,
            Amount = plan.PrecioMensual,
            Currency = "MXN",
            OccurredAtUtc = now
        };
    }

    private static FamilyMemberDto MapMember(FamilyMembership membership)
    {
        return new FamilyMemberDto
        {
            PublicMembershipId = membership.PublicMembershipId,
            PublicProfileId = membership.PublicProfileIdSnapshot,
            Username = membership.UsernameSnapshot,
            DisplayName = membership.DisplayNameSnapshot,
            Role = membership.Role,
            Status = membership.Status,
            AcceptedAtUtc = membership.AcceptedAtUtc
        };
    }

    private static IncomingFamilyInvitationDto MapIncomingInvitation(
        FamilySubscription subscription,
        FamilyInvitation invitation,
        Usuario owner)
    {
        return new IncomingFamilyInvitationDto
        {
            PublicInvitationId = invitation.PublicInvitationId,
            TargetUsername = invitation.TargetUsername,
            TargetPublicProfileId = invitation.TargetPublicProfileId,
            TargetEmail = invitation.TargetEmailNormalized,
            Status = invitation.Status,
            CreatedAtUtc = invitation.CreatedAtUtc,
            ExpiresAtUtc = invitation.ExpiresAtUtc,
            OwnerPublicProfileId = owner.PublicProfileId,
            OwnerUsername = owner.Username,
            OwnerName = owner.Nombre,
            PlanName = PlanNamePolicy.ToPublicName(subscription.PlanName)
        };
    }

    private static FamilyInvitationDto MapInvitation(FamilyInvitation invitation)
    {
        return new FamilyInvitationDto
        {
            PublicInvitationId = invitation.PublicInvitationId,
            TargetUsername = invitation.TargetUsername,
            TargetPublicProfileId = invitation.TargetPublicProfileId,
            TargetEmail = invitation.TargetEmailNormalized,
            Status = invitation.Status,
            CreatedAtUtc = invitation.CreatedAtUtc,
            ExpiresAtUtc = invitation.ExpiresAtUtc
        };
    }

    private static SimulatedPaymentDto MapPayment(SimulatedPaymentRecord payment)
    {
        return new SimulatedPaymentDto
        {
            PublicPaymentId = payment.PublicPaymentId,
            Result = payment.Result,
            PlanName = PlanNamePolicy.ToPublicName(payment.PlanName),
            Amount = payment.Amount,
            Currency = payment.Currency,
            OccurredAtUtc = payment.OccurredAtUtc
        };
    }

    private static int CountAcceptedInvitedMembers(FamilySubscription subscription)
    {
        return subscription.Memberships.Count(value =>
            value.Role == FamilyMembershipRole.Member
            && value.Status == FamilyMembershipStatus.Active);
    }

    public static int GetMemberLimit(string planName)
    {
        return NormalizePlanName(planName) switch
        {
            "Free" => 1,
            "Basic" => 2,
            "Premium" => 5,
            _ => 1
        };
    }

    public static int GetMonitoringLimit(string planName)
    {
        return NormalizePlanName(planName) switch
        {
            "Free" => 1,
            "Basic" => 3,
            "Premium" => 6,
            _ => 1
        };
    }

    public static int GetSosContactLimit(string planName)
    {
        return GetMemberLimit(planName);
    }

    public static int GetVehicleLimit(string planName)
    {
        return NormalizePlanName(planName) switch
        {
            "Free" => 1,
            "Basic" => 3,
            "Premium" => int.MaxValue,
            _ => 1
        };
    }

    private static string NormalizePlanName(string? planName)
    {
        var normalized = (planName ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            "free" or "gratuito" or "trial" => "Free",
            "basic" or "standard" or "estandar" or "estándar" => "Basic",
            "premium" or "enterprise" => "Premium",
            _ => planName?.Trim() ?? string.Empty
        };
    }

    private sealed record ResolvedInvitationTarget(Usuario? User, string? EmailNormalized);
}
