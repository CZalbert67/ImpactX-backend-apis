using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public sealed class AccountService : IAccountService
{
    private readonly IUserService _userService;
    private readonly IPlanService _planService;
    private readonly IFamilySubscriptionService _familyService;
    private readonly IWearableService _wearableService;
    private readonly IVehicleService _vehicleService;
    private readonly IEmergencyContactService _emergencyContactService;
    private readonly IMonitoringRelationshipService _monitoringService;
    private readonly INotificationService _notificationService;
    private readonly IQuickMessageService _quickMessageService;
    private readonly IUsuarioRepository _userRepository;
    private readonly IViajeRepository _tripRepository;
    private readonly IAlertaRepository _alertRepository;
    private readonly IIncidenteRepository _incidentRepository;
    private readonly IEncryptionService _encryptionService;
    private readonly IAuthService _authService;

    public AccountService(
        IUserService userService,
        IPlanService planService,
        IFamilySubscriptionService familyService,
        IWearableService wearableService,
        IVehicleService vehicleService,
        IEmergencyContactService emergencyContactService,
        IMonitoringRelationshipService monitoringService,
        INotificationService notificationService,
        IQuickMessageService quickMessageService,
        IUsuarioRepository userRepository,
        IViajeRepository tripRepository,
        IAlertaRepository alertRepository,
        IIncidenteRepository incidentRepository,
        IEncryptionService encryptionService,
        IAuthService authService)
    {
        _userService = userService;
        _planService = planService;
        _familyService = familyService;
        _wearableService = wearableService;
        _vehicleService = vehicleService;
        _emergencyContactService = emergencyContactService;
        _monitoringService = monitoringService;
        _notificationService = notificationService;
        _quickMessageService = quickMessageService;
        _userRepository = userRepository;
        _tripRepository = tripRepository;
        _alertRepository = alertRepository;
        _incidentRepository = incidentRepository;
        _encryptionService = encryptionService;
        _authService = authService;
    }

    public async Task<AccountExportV2Dto> ExportAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var profile = await _userService.GetProfileAsync(userId);
        var effective = await _planService.GetEffectiveSubscriptionAsync(userId, cancellationToken);
        var subscriptions = await _planService.GetSubscriptionHistoryAsync(userId);
        var payments = await _planService.GetPaymentsAsync(userId);
        var family = await _familyService.GetCurrentAsync(userId, cancellationToken);
        var wearable = await _wearableService.GetWearableAsync(userId);
        var vehicles = await _vehicleService.GetVehiclesAsync(userId, cancellationToken);
        var emergency = await _emergencyContactService.GetSyncAsync(userId, cancellationToken);
        var monitoring = await _monitoringService.GetRelationshipsAsync(userId, cancellationToken);
        var notifications = await _notificationService.GetNotificationsAsync(userId);
        var quickMessages = await _quickMessageService.GetHistoryAsync(userId, null, cancellationToken);
        var trips = await _tripRepository.GetByUserAsync(userId);
        var telemetry = new List<ViajeTelemetry>();
        foreach (var trip in trips)
        {
            cancellationToken.ThrowIfCancellationRequested();
            telemetry.AddRange(await _tripRepository.GetTelemetryByViajeAsync(trip.Id));
        }

        return new AccountExportV2Dto
        {
            ContractVersion = 2,
            GeneratedAtUtc = DateTime.UtcNow,
            Profile = profile,
            EffectiveSubscription = effective,
            SubscriptionHistory = subscriptions,
            Payments = payments,
            FamilySubscription = family,
            Wearable = wearable,
            Vehicles = vehicles,
            EmergencyContacts = emergency,
            MonitoringRelationships = monitoring,
            Trips = trips,
            Telemetry = telemetry,
            Alerts = await _alertRepository.GetByUserAsync(userId),
            Incidents = await _incidentRepository.GetByUserAsync(userId),
            Notifications = notifications,
            QuickMessages = quickMessages
        };
    }

    public async Task<AccountRetentionDto> GetRetentionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("Usuario no encontrado.");
        return new AccountRetentionDto
        {
            TripsAndTelemetryDays = 90,
            AlertsAndIncidentsDays = 365,
            NotificationsDays = 30,
            AccountActive = user.IsActive,
            DeletedAtUtc = user.DeletedAtUtc,
            DataAnonymizedAtUtc = user.DataAnonymizedAtUtc,
            DeletionMode = "immediate-identity-anonymization;domain-records-follow-ttl"
        };
    }

    public async Task<UserProfileDto> RevokeConsentsAsync(
        Guid userId,
        RevokeConsentsRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("Usuario no encontrado.");
        user.Onboarding ??= new OnboardingProgress();
        if (request.RevokeLocationIncidentConsent)
            user.Onboarding.LocationIncidentConsent = false;
        if (request.RevokeDrivingPatternConsent)
            user.Onboarding.DrivingPatternConsent = false;
        if (request.RemoveMedicalProfile)
        {
            user.FichaMedica = null;
            user.Onboarding.MedicalProfileStatus = MedicalProfileOnboardingStatus.Skipped;
        }
        user.Onboarding.UpdatedAtUtc = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        return await _userService.GetProfileAsync(userId);
    }

    public async Task<DeleteAccountV2Response> DeleteAsync(
        Guid userId,
        DeleteAccountV2Request request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(request.Confirmation?.Trim(), "DELETE", StringComparison.Ordinal))
            throw new BadRequestException("Confirmation debe ser exactamente DELETE.");

        var user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("Usuario no encontrado.");
        if (!_encryptionService.VerifyPassword(request.Password, user.PasswordHash))
            throw new ForbiddenException("La contraseña actual no es válida.");

        var now = DateTime.UtcNow;
        await _authService.DeleteAccountAsync(userId);
        user = await _userRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("Usuario no encontrado.");
        var suffix = userId.ToString("N");
        user.Nombre = "Cuenta eliminada";
        user.Username = $"deleted_{suffix[..12]}";
        user.PublicProfileId = $"deleted-{suffix[..16]}";
        user.Correo = $"deleted+{suffix}@impactx.invalid";
        user.CorreoNormalizado = user.Correo;
        user.Telefono = string.Empty;
        user.AppId = string.Empty;
        user.InviteCode = string.Empty;
        user.UsernamesAnteriores = [];
        user.FcmToken = null;
        user.PerfilConduccion = null;
        user.FichaMedica = null;
        user.Preferencias = null;
        user.Permisos = null;
        user.Settings = null;
        user.Onboarding = null;
        user.MobileSyncReceipts = [];
        user.MobileSyncLastAcknowledgedCursor = null;
        user.MobileSyncClientInstanceId = null;
        user.PasswordHash = _encryptionService.HashPassword(Guid.NewGuid().ToString("N"));
        user.DeletedAtUtc = now;
        user.DataAnonymizedAtUtc = now;
        user.DeletionReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
        user.IsActive = false;
        await _userRepository.UpdateAsync(user);

        return new DeleteAccountV2Response
        {
            Deleted = true,
            DeletedAtUtc = now,
            IdentityAnonymized = true,
            RetentionSummary = "Identidad anonimizada inmediatamente. Viajes/telemetría: 90 días; alertas/incidentes: 365 días; notificaciones: 30 días."
        };
    }
}
