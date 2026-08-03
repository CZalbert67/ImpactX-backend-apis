using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Identity;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Security;
using ImpactX.Models.DTOs.FamilySubscriptions;

namespace ImpactX.Services;

public class FamilySubscriptionService : IFamilySubscriptionService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(3);
    private const int MaxPendingInvitations = 20;

    private readonly IFamilySubscriptionRepository _familyRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IPlanRepository _planRepository;
    private readonly IMonitoringRelationshipRepository? _monitoringRepository;

    public FamilySubscriptionService(
        IFamilySubscriptionRepository familyRepository,
        IUsuarioRepository usuarioRepository,
        IPlanRepository planRepository,
        IMonitoringRelationshipRepository? monitoringRepository = null)
    {
        _familyRepository = familyRepository;
        _usuarioRepository = usuarioRepository;
        _planRepository = planRepository;
        _monitoringRepository = monitoringRepository;
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
        return subscription.Status == FamilySubscriptionStatus.Expired
            ? null
            : await MapSummaryAsync(subscription, userId, cancellationToken);
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

        await ApplyPlanAsync(subscription, plan, cancellationToken);
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

        await _familyRepository.UpdateAsync(subscription, cancellationToken);
        await ResetUserPlansAsync(affectedUserIds);
    }

    public async Task<IReadOnlyList<FamilyMemberDto>> GetMembersAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await RequireMembershipAsync(userId, cancellationToken);
        return subscription.Memberships
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
            if (activeFamily is not null)
            {
                throw new ConflictException("El usuario ya pertenece a una suscripción familiar activa.");
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
            CreateMonitoringRelationship = request.CreateMonitoringRelationship
        };

        subscription.Invitations.Add(invitation);
        subscription.UpdatedAtUtc = now;
        await _familyRepository.UpdateAsync(subscription, cancellationToken);

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

        var member = await _usuarioRepository.GetByIdAsync(membership.UserId);
        if (member is not null)
        {
            await SetUserPlanAsync(member, "Free");
        }
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

        var user = await GetUserAsync(userId);
        await SetUserPlanAsync(user, "Free");
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
            throw new ConflictException("Ya perteneces a otra suscripción familiar activa.");
        }

        var memberLimit = GetMemberLimit(subscription.PlanName);
        if (CountAcceptedInvitedMembers(subscription) >= memberLimit)
        {
            throw new ConflictException("El plan ya alcanzó su límite de integrantes aceptados.");
        }

        await EnsureMonitoringCapacityIfRequestedAsync(
            subscription,
            invitation,
            userId,
            cancellationToken);

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

        invitation.Status = FamilyInvitationStatus.Accepted;
        invitation.RespondedAtUtc = now;
        invitation.ConsumedAtUtc = now;
        invitation.CodeHash = string.Empty;
        subscription.UpdatedAtUtc = now;

        await _familyRepository.UpdateAsync(subscription, cancellationToken);
        await SetUserPlanAsync(user, subscription.PlanName);
        await CreateAcceptedMonitoringRelationshipIfRequestedAsync(
            subscription,
            invitation,
            user,
            cancellationToken);
    }

    private async Task EnsureMonitoringCapacityIfRequestedAsync(
        FamilySubscription subscription,
        FamilyInvitation invitation,
        Guid monitoredUserId,
        CancellationToken cancellationToken)
    {
        if (!invitation.CreateMonitoringRelationship || _monitoringRepository is null)
        {
            return;
        }

        if (await _monitoringRepository.ExistsBlockedAsync(
                subscription.OwnerUserId,
                monitoredUserId,
                cancellationToken))
        {
            throw new ForbiddenException(
                "No se puede crear la relación de monitoreo asociada a esta membresía.");
        }

        if (await _monitoringRepository.ExistsActiveOrPendingAsync(
                subscription.OwnerUserId,
                monitoredUserId,
                cancellationToken))
        {
            return;
        }

        var acceptedRelationships = await _monitoringRepository.CountAcceptedByMonitorAsync(
            subscription.OwnerUserId,
            cancellationToken);
        if (acceptedRelationships >= GetMemberLimit(subscription.PlanName))
        {
            throw new ConflictException(
                "La invitación también crea una relación de monitoreo y la red ya alcanzó el límite del plan.");
        }
    }

    private async Task CreateAcceptedMonitoringRelationshipIfRequestedAsync(
        FamilySubscription subscription,
        FamilyInvitation invitation,
        Usuario monitoredUser,
        CancellationToken cancellationToken)
    {
        if (!invitation.CreateMonitoringRelationship || _monitoringRepository is null)
        {
            return;
        }

        if (await _monitoringRepository.ExistsActiveOrPendingAsync(
                subscription.OwnerUserId,
                monitoredUser.Id,
                cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        await _monitoringRepository.AddAsync(new MonitoringRelationship
        {
            Id = Guid.NewGuid(),
            PublicRelationshipId = MonitoringPublicIdGenerator.GenerateRelationshipId(),
            MonitorUserId = subscription.OwnerUserId,
            MonitoredUserId = monitoredUser.Id,
            InitiatedByUserId = subscription.OwnerUserId,
            Direction = MonitoringRequestDirection.MonitorInvitesMonitored,
            Status = MonitoringRelationshipStatus.Accepted,
            TargetEmailNormalized = monitoredUser.CorreoNormalizado,
            TargetPublicProfileId = monitoredUser.PublicProfileId,
            TargetUsername = monitoredUser.Username,
            InvitationCodeHash = string.Empty,
            Permissions = new MonitoringPermissions
            {
                ViewRoutes = true,
                ViewLocation = true,
                ViewEmergencyLocation = true,
                ViewIncidents = true,
                ReceiveCriticalAlerts = true,
                ViewMedicalProfile = false,
                SendMessages = true,
                ViewTelemetry = true,
                ReceiveNotifications = true
            },
            RequestedAtUtc = invitation.CreatedAtUtc,
            ExpiresAtUtc = invitation.ExpiresAtUtc,
            AcceptedAtUtc = now,
            UpdatedAtUtc = now
        }, cancellationToken);
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

    private async Task ResetUserPlansAsync(IEnumerable<Guid> userIds)
    {
        foreach (var userId in userIds.Distinct())
        {
            var user = await _usuarioRepository.GetByIdAsync(userId);
            if (user is not null)
            {
                await SetUserPlanAsync(user, "Free");
            }
        }
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
            AvailableMemberSlots = Math.Max(0, limit - accepted),
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
            LatestPayment = subscription.Payments
                .OrderByDescending(value => value.OccurredAtUtc)
                .Select(MapPayment)
                .FirstOrDefault()
        };
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

            await _familyRepository.UpdateAsync(subscription, cancellationToken);
            await ResetUserPlansAsync(affectedUserIds);
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
            "Basic" => 3,
            "Premium" => 6,
            _ => 1
        };
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
