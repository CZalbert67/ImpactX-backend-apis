using System.Text.Json;
using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Interfaces.Services;
using ImpactX.Core.Sync;
using ImpactX.Core.Telemetry;
using ImpactX.Models.DTOs;
using ImpactX.Models.DTOs.QuickMessages;
using ImpactX.Models.DTOs.Vehicles;

namespace ImpactX.Services;

public sealed class MobileSyncService : IMobileSyncService
{
    private const int MaxReceipts = 200;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IUserService _userService;
    private readonly IPlanService _planService;
    private readonly IPermissionService _permissionService;
    private readonly IWearableService _wearableService;
    private readonly IViajeService _tripService;
    private readonly IVehicleService _vehicleService;
    private readonly IEmergencyContactService _emergencyContactService;
    private readonly IMonitoringRelationshipService _monitoringService;
    private readonly IQuickMessageService _quickMessageService;
    private readonly INotificationService _notificationService;
    private readonly IIncidentService _incidentService;
    private readonly IUsuarioRepository _usuarioRepository;

    public MobileSyncService(
        IUserService userService,
        IPlanService planService,
        IPermissionService permissionService,
        IWearableService wearableService,
        IViajeService tripService,
        IVehicleService vehicleService,
        IEmergencyContactService emergencyContactService,
        IMonitoringRelationshipService monitoringService,
        IQuickMessageService quickMessageService,
        INotificationService notificationService,
        IIncidentService incidentService,
        IUsuarioRepository usuarioRepository)
    {
        _userService = userService;
        _planService = planService;
        _permissionService = permissionService;
        _wearableService = wearableService;
        _tripService = tripService;
        _vehicleService = vehicleService;
        _emergencyContactService = emergencyContactService;
        _monitoringService = monitoringService;
        _quickMessageService = quickMessageService;
        _notificationService = notificationService;
        _incidentService = incidentService;
        _usuarioRepository = usuarioRepository;
    }

    public async Task<MobileSyncSnapshotDto> GetBootstrapAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Se ejecuta secuencialmente porque los repositorios EF de pruebas
        // comparten un DbContext scoped, que no admite operaciones paralelas.
        var profile = await _userService.GetProfileAsync(userId);
        var effectiveSubscription = await _planService.GetEffectiveSubscriptionAsync(userId, cancellationToken);
        var permissions = await _permissionService.GetPermissionsAsync(userId);
        var wearable = await _wearableService.GetWearableAsync(userId);
        var activeTrip = await _tripService.GetActiveAsync(userId);
        var vehicles = await _vehicleService.GetVehiclesAsync(userId, cancellationToken);
        var emergencyContacts = await _emergencyContactService.GetSyncAsync(userId, cancellationToken);
        var monitoringRelationships = await _monitoringService.GetRelationshipsAsync(userId, cancellationToken);
        var activeIncidents = await _incidentService.GetActiveIncidentsAsync(userId);
        var quickTemplates = await _quickMessageService.GetTemplatesAsync(userId, cancellationToken);
        var quickRecipients = await _quickMessageService.GetRecipientsAsync(userId, cancellationToken);
        var unreadNotifications = await _notificationService.GetUnreadCountAsync(userId);
        var unreadQuickMessages = await _quickMessageService.GetUnreadCountAsync(userId, cancellationToken);
        var now = DateTime.UtcNow;

        var snapshot = new MobileSyncSnapshotDto
        {
            ContractVersion = 2,
            SnapshotId = Guid.NewGuid(),
            GeneratedAtUtc = now,
            Profile = profile,
            EffectiveSubscription = effectiveSubscription,
            Permissions = permissions,
            Wearable = wearable,
            ActiveTrip = activeTrip,
            Vehicles = vehicles,
            EmergencyContacts = emergencyContacts,
            MonitoringRelationships = monitoringRelationships,
            ActiveIncidents = activeIncidents,
            QuickMessageTemplates = quickTemplates,
            QuickMessageRecipients = quickRecipients,
            UnreadNotifications = unreadNotifications,
            UnreadQuickMessages = unreadQuickMessages,
            OfflineContract = BuildOfflineContract()
        };
        snapshot.SyncCursor = MobileSyncCursor.Compute(snapshot);
        return snapshot;
    }

    public async Task<MobileSyncChangesDto> GetChangesAsync(
        Guid userId,
        string? cursor,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetBootstrapAsync(userId, cancellationToken);
        var hasChanges = !string.Equals(
            cursor?.Trim(),
            snapshot.SyncCursor,
            StringComparison.OrdinalIgnoreCase);

        return new MobileSyncChangesDto
        {
            Cursor = snapshot.SyncCursor,
            HasChanges = hasChanges,
            RequiresBootstrap = hasChanges,
            GeneratedAtUtc = snapshot.GeneratedAtUtc,
            Snapshot = hasChanges ? snapshot : null
        };
    }

    public async Task<MobileSyncPushResponse> PushAsync(
        Guid userId,
        MobileSyncPushRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePushRequest(request);
        var before = await GetBootstrapAsync(userId, cancellationToken);
        var user = await _usuarioRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("Usuario no encontrado.");
        user.MobileSyncReceipts ??= [];

        var receiptById = user.MobileSyncReceipts
            .GroupBy(value => value.OperationId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(value => value.AppliedAtUtc).First());
        var newReceipts = new List<MobileSyncOperationReceipt>();
        var results = new List<MobileSyncOperationResultDto>(request.Operations.Count);

        foreach (var operation in request.Operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateOperation(operation);

            if (receiptById.TryGetValue(operation.OperationId, out var existing))
            {
                results.Add(MapReceipt(existing, wasDuplicate: true));
                continue;
            }

            MobileSyncOperationReceipt receipt;
            try
            {
                await ApplyOperationAsync(userId, operation, cancellationToken);
                receipt = NewReceipt(operation, "applied", null);
            }
            catch (Exception ex) when (ex is BadRequestException
                or NotFoundException
                or ConflictException
                or ForbiddenException)
            {
                receipt = NewReceipt(operation, "rejected", ex.Message);
            }

            receiptById[receipt.OperationId] = receipt;
            newReceipts.Add(receipt);
            results.Add(MapReceipt(receipt, wasDuplicate: false));
        }

        if (newReceipts.Count > 0)
        {
            // Algunos servicios anteriores actualizan el mismo usuario. Se
            // relee antes de guardar los recibos para no pisar esos cambios.
            user = await _usuarioRepository.GetByIdAsync(userId)
                ?? throw new NotFoundException("Usuario no encontrado.");
            user.MobileSyncReceipts ??= [];
            user.MobileSyncReceipts.AddRange(newReceipts);
            user.MobileSyncReceipts = user.MobileSyncReceipts
                .GroupBy(value => value.OperationId)
                .Select(group => group.OrderByDescending(value => value.AppliedAtUtc).First())
                .OrderByDescending(value => value.AppliedAtUtc)
                .Take(MaxReceipts)
                .ToList();
            user.MobileSyncRevision++;
            user.MobileSyncClientInstanceId = request.ClientInstanceId.Trim();
            await _usuarioRepository.UpdateAsync(user);
        }

        var after = await GetBootstrapAsync(userId, cancellationToken);
        return new MobileSyncPushResponse
        {
            PreviousCursor = before.SyncCursor,
            Cursor = after.SyncCursor,
            RequiresPull = !string.IsNullOrWhiteSpace(request.BaseCursor)
                && !string.Equals(request.BaseCursor.Trim(), before.SyncCursor, StringComparison.OrdinalIgnoreCase),
            ServerRevision = user.MobileSyncRevision,
            Results = results
        };
    }

    public async Task<MobileSyncAckResponse> AcknowledgeAsync(
        Guid userId,
        MobileSyncAckRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ClientInstanceId)
            || string.IsNullOrWhiteSpace(request.Cursor))
        {
            throw new BadRequestException("ClientInstanceId y cursor son obligatorios.");
        }

        var current = await GetBootstrapAsync(userId, cancellationToken);
        if (!string.Equals(request.Cursor.Trim(), current.SyncCursor, StringComparison.OrdinalIgnoreCase))
        {
            throw new ConflictException("El cursor ya no representa el estado actual. Solicita /changes o /bootstrap.");
        }

        var user = await _usuarioRepository.GetByIdAsync(userId)
            ?? throw new NotFoundException("Usuario no encontrado.");
        var now = DateTime.UtcNow;
        user.MobileSyncLastAcknowledgedCursor = current.SyncCursor;
        user.MobileSyncLastAckAtUtc = now;
        user.MobileSyncClientInstanceId = request.ClientInstanceId.Trim();
        await _usuarioRepository.UpdateAsync(user);

        return new MobileSyncAckResponse
        {
            Cursor = current.SyncCursor,
            AcknowledgedAtUtc = now,
            ServerRevision = user.MobileSyncRevision
        };
    }

    private async Task ApplyOperationAsync(
        Guid userId,
        MobileSyncOperationDto operation,
        CancellationToken cancellationToken)
    {
        switch (operation.Type.Trim().ToLowerInvariant())
        {
            case "notification.mark-read":
            {
                var payload = Deserialize<NotificationReadPayload>(operation.Payload);
                if (payload.NotificationId == Guid.Empty)
                    throw new BadRequestException("notificationId es obligatorio.");
                await _notificationService.ToggleReadAsync(
                    userId,
                    payload.NotificationId,
                    new ToggleReadRequest { Leida = payload.Read });
                break;
            }
            case "notification.mark-all-read":
                await _notificationService.MarkAllAsReadAsync(userId);
                break;
            case "quick-message.mark-read":
            {
                var payload = Deserialize<QuickMessageReadPayload>(operation.Payload);
                if (string.IsNullOrWhiteSpace(payload.PublicMessageId))
                    throw new BadRequestException("publicMessageId es obligatorio.");
                await _quickMessageService.MarkReadAsync(
                    userId,
                    payload.PublicMessageId.Trim(),
                    cancellationToken);
                break;
            }
            case "permissions.update":
            {
                var payload = Deserialize<UpdatePermissionsRequest>(operation.Payload);
                await _permissionService.UpdateMobilePermissionsAsync(userId, payload);
                break;
            }
            case "fcm-token.upsert":
            {
                var payload = Deserialize<UpdateFcmTokenRequest>(operation.Payload);
                if (string.IsNullOrWhiteSpace(payload.Token))
                    throw new BadRequestException("El token FCM es obligatorio.");
                await _userService.UpdateFcmTokenAsync(userId, payload);
                break;
            }
            case "fcm-token.delete":
                await _userService.DeleteFcmTokenAsync(userId);
                break;
            case "profile.update":
            {
                var payload = Deserialize<UpdateUserProfileRequest>(operation.Payload);
                await _userService.UpdateProfileAsync(userId, payload);
                break;
            }
            case "preferences.update":
            {
                var payload = Deserialize<UpdateUserPreferencesRequest>(operation.Payload);
                await _userService.UpdatePreferencesAsync(userId, payload);
                break;
            }
            case "onboarding.update":
            {
                var payload = Deserialize<UpdateOnboardingRequest>(operation.Payload);
                await _userService.UpdateOnboardingAsync(userId, payload);
                break;
            }
            case "vehicle.create":
            {
                var payload = Deserialize<CreateVehicleRequest>(operation.Payload);
                await _vehicleService.CreateVehicleAsync(userId, payload, cancellationToken);
                break;
            }
            case "vehicle.update":
            {
                var payload = Deserialize<VehicleUpdatePayload>(operation.Payload);
                if (string.IsNullOrWhiteSpace(payload.PublicVehicleId) || payload.Vehicle is null)
                    throw new BadRequestException("publicVehicleId y vehicle son obligatorios.");
                await _vehicleService.UpdateVehicleAsync(
                    userId,
                    payload.PublicVehicleId.Trim(),
                    payload.Vehicle,
                    cancellationToken);
                break;
            }
            case "vehicle.delete":
            {
                var payload = Deserialize<VehicleIdPayload>(operation.Payload);
                if (string.IsNullOrWhiteSpace(payload.PublicVehicleId))
                    throw new BadRequestException("publicVehicleId es obligatorio.");
                await _vehicleService.DeleteVehicleAsync(
                    userId,
                    payload.PublicVehicleId.Trim(),
                    cancellationToken);
                break;
            }
            case "vehicle.set-primary":
            {
                var payload = Deserialize<VehicleIdPayload>(operation.Payload);
                if (string.IsNullOrWhiteSpace(payload.PublicVehicleId))
                    throw new BadRequestException("publicVehicleId es obligatorio.");
                await _vehicleService.SetPrimaryVehicleAsync(
                    userId,
                    payload.PublicVehicleId.Trim(),
                    cancellationToken);
                break;
            }
            case "quick-message.send":
            {
                var payload = Deserialize<SendQuickMessageRequest>(operation.Payload);
                await _quickMessageService.SendAsync(userId, payload, cancellationToken);
                break;
            }
            default:
                throw new BadRequestException($"Operación de sincronización no soportada: {operation.Type}.");
        }
    }

    private static T Deserialize<T>(JsonElement payload)
    {
        try
        {
            return payload.Deserialize<T>(JsonOptions)
                ?? throw new BadRequestException("El payload de la operación es inválido.");
        }
        catch (JsonException)
        {
            throw new BadRequestException("El payload de la operación es inválido.");
        }
    }

    private static void ValidatePushRequest(MobileSyncPushRequest request)
    {
        if (request is null)
            throw new BadRequestException("El cuerpo de sincronización es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.ClientInstanceId))
            throw new BadRequestException("ClientInstanceId es obligatorio.");
        if (request.ClientInstanceId.Trim().Length > 100)
            throw new BadRequestException("ClientInstanceId excede 100 caracteres.");
        if (request.Operations.Count is < 1 or > 50)
            throw new BadRequestException("Operations debe contener entre 1 y 50 elementos.");
        if (request.Operations.Select(value => value.OperationId).Distinct().Count() != request.Operations.Count)
            throw new BadRequestException("El lote contiene operationId duplicados.");
    }

    private static void ValidateOperation(MobileSyncOperationDto operation)
    {
        if (operation.OperationId == Guid.Empty)
            throw new BadRequestException("operationId es obligatorio.");
        if (string.IsNullOrWhiteSpace(operation.Type))
            throw new BadRequestException("type es obligatorio.");
        if (operation.Type.Trim().Length > 80)
            throw new BadRequestException("type excede 80 caracteres.");
    }

    private static MobileSyncOperationReceipt NewReceipt(
        MobileSyncOperationDto operation,
        string result,
        string? message)
    {
        return new MobileSyncOperationReceipt
        {
            OperationId = operation.OperationId,
            Type = operation.Type.Trim().ToLowerInvariant(),
            Result = result,
            Message = message,
            AppliedAtUtc = DateTime.UtcNow
        };
    }

    private static MobileSyncOperationResultDto MapReceipt(
        MobileSyncOperationReceipt receipt,
        bool wasDuplicate)
    {
        return new MobileSyncOperationResultDto
        {
            OperationId = receipt.OperationId,
            Type = receipt.Type,
            Result = receipt.Result,
            Message = receipt.Message,
            ProcessedAtUtc = receipt.AppliedAtUtc,
            WasDuplicate = wasDuplicate
        };
    }

    private static MobileOfflineContractDto BuildOfflineContract() => new()
    {
        ContractVersion = 2,
        TelemetrySchemaVersion = 2,
        MaxTelemetryEventsPerBatch = TelemetryIngestionLimits.MaxEventsPerBatch,
        MaxTelemetryBodyBytes = TelemetryIngestionLimits.MaxBodyBytes,
        MaxOperationsPerPush = 50,
        TelemetryWriter = "wearable",
        MobileMayControlTrip = false,
        MobileMayReadTripState = true,
        MobileMayRelayOfflineAlerts = true,
        IdempotencyKey = "operationId/eventId",
        DuplicateBehavior = "same-operation-is-idempotent;telemetry-different-content-is-409"
    };

    private sealed class NotificationReadPayload
    {
        public Guid NotificationId { get; set; }
        public bool Read { get; set; } = true;
    }

    private sealed class QuickMessageReadPayload
    {
        public string PublicMessageId { get; set; } = string.Empty;
    }

    private sealed class VehicleIdPayload
    {
        public string PublicVehicleId { get; set; } = string.Empty;
    }

    private sealed class VehicleUpdatePayload
    {
        public string PublicVehicleId { get; set; } = string.Empty;
        public UpdateVehicleRequest? Vehicle { get; set; }
    }
}
