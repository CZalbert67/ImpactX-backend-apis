using ImpactX.Core.Domain;
using ImpactX.Core.Domain.Enums;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Identity;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Core.Pagination;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class PlanService : IPlanService
{
    private static readonly TimeSpan GracePeriod = TimeSpan.FromDays(3);
    private readonly IPlanRepository _planRepository;
    private readonly ISuscripcionRepository _suscripcionRepository;
    private readonly IPagoRepository _pagoRepository;
    private readonly IUsuarioRepository _usuarioRepository;
    private readonly IFamilySubscriptionRepository? _familyRepository;

    private static readonly Dictionary<string, int> PlanOrder = new(StringComparer.OrdinalIgnoreCase)
    {
        [PlanNamePolicy.Free] = 0,
        ["Trial"] = 0,
        [PlanNamePolicy.LegacyBasic] = 1,
        [PlanNamePolicy.Standard] = 1,
        [PlanNamePolicy.Premium] = 2,
        ["Enterprise"] = 3,
    };

    public PlanService(
        IPlanRepository planRepository,
        ISuscripcionRepository suscripcionRepository,
        IPagoRepository pagoRepository,
        IUsuarioRepository usuarioRepository)
        : this(planRepository, suscripcionRepository, pagoRepository, usuarioRepository, null)
    {
    }

    public PlanService(
        IPlanRepository planRepository,
        ISuscripcionRepository suscripcionRepository,
        IPagoRepository pagoRepository,
        IUsuarioRepository usuarioRepository,
        IFamilySubscriptionRepository? familyRepository)
    {
        _planRepository = planRepository;
        _suscripcionRepository = suscripcionRepository;
        _pagoRepository = pagoRepository;
        _usuarioRepository = usuarioRepository;
        _familyRepository = familyRepository;
    }

    public async Task<List<PlanDto>> GetAllPlansAsync()
    {
        var plans = await _planRepository.GetAllAsync();
        return plans.Select(MapToPlanDto).ToList();
    }

    public async Task<SuscripcionDto?> GetCurrentSubscriptionAsync(Guid usuarioId)
    {
        var subscription = await GetCurrentAsync(usuarioId);
        if (subscription is null)
            return null;

        if (await ApplyLifecycleAsync(subscription, DateTime.UtcNow))
        {
            subscription = await GetCurrentAsync(usuarioId);
            if (subscription is null)
                return null;
        }

        var plan = await _planRepository.GetByIdAsync(subscription.PlanId);
        return MapToSuscripcionDto(subscription, plan);
    }

    public async Task<EffectiveSubscriptionDto> GetEffectiveSubscriptionAsync(
        Guid usuarioId,
        CancellationToken cancellationToken = default)
    {
        if (_familyRepository is not null)
        {
            var family = await _familyRepository.GetActiveByUserAsync(usuarioId, cancellationToken);
            if (family is not null && TryResolveFamilyEntitlement(family, DateTime.UtcNow, out var familyState, out var graceEndsAtUtc))
            {
                var publicName = PlanNamePolicy.ToPublicName(family.PlanName);
                return BuildEffective(
                    publicName,
                    family.OwnerUserId == usuarioId ? "FamilyOwner" : "FamilyMember",
                    familyState,
                    family.OwnerUserId == usuarioId,
                    family.PeriodEndUtc,
                    graceEndsAtUtc);
            }
        }

        var current = await GetCurrentSubscriptionAsync(usuarioId);
        if (current is not null)
        {
            return BuildEffective(
                current.PlanNombre,
                "Individual",
                current.Estado,
                true,
                current.Fin,
                current.GraceEndsAtUtc);
        }

        return BuildEffective(PlanNamePolicy.Free, "Free", "Activa", true, null, null);
    }

    public async Task<List<SuscripcionDto>> GetSubscriptionHistoryAsync(Guid usuarioId)
    {
        var history = await _suscripcionRepository.GetHistoryByUserAsync(usuarioId);
        var dtos = new List<SuscripcionDto>();
        foreach (var subscription in history)
        {
            var plan = await _planRepository.GetByIdAsync(subscription.PlanId);
            dtos.Add(MapToSuscripcionDto(subscription, plan));
        }
        return dtos;
    }

    public async Task<PagedResult<SuscripcionDto>> GetSubscriptionHistoryPagedAsync(
        Guid usuarioId,
        int? pageSize,
        string? continuationToken)
    {
        var size = PaginationValidator.Resolve(pageSize, continuationToken);
        var page = await _suscripcionRepository.GetHistoryByUserPagedAsync(usuarioId, size, continuationToken);
        var dtos = new List<SuscripcionDto>();
        foreach (var subscription in page.Items)
        {
            var plan = await _planRepository.GetByIdAsync(subscription.PlanId);
            dtos.Add(MapToSuscripcionDto(subscription, plan));
        }

        return new PagedResult<SuscripcionDto>
        {
            Items = dtos,
            ContinuationToken = page.ContinuationToken,
            HasMoreResults = page.HasMoreResults,
            PageSize = page.PageSize,
        };
    }

    public async Task<SuscripcionDto> ChangePlanAsync(Guid usuarioId, ChangePlanRequest request)
    {
        var result = await ActivateAsync(usuarioId, request);
        return result.Subscription;
    }

    public async Task<SubscriptionPaymentResultDto> ActivateAsync(
        Guid usuarioId,
        ChangePlanRequest request)
    {
        if (_familyRepository is not null)
        {
            var family = await _familyRepository.GetActiveByUserAsync(usuarioId);
            if (family is not null
                && TryResolveFamilyEntitlement(family, DateTime.UtcNow, out _, out _))
            {
                throw new ConflictException(
                    "Ya recibes beneficios mediante una suscripción familiar; no necesitas activar un plan individual.");
            }
        }

        var storageName = PlanNamePolicy.ToStorageName(request.PlanNombre);
        if (storageName == PlanNamePolicy.Free)
            throw new BadRequestException("El plan Free no requiere activación ni pago.");

        var plan = await _planRepository.GetByNameAsync(storageName)
            ?? throw new BadRequestException("Plan no encontrado.");
        var billingCycle = NormalizeBillingCycle(request.BillingCycle);
        var current = await GetCurrentAsync(usuarioId);
        if (current is not null)
        {
            var currentPlan = await _planRepository.GetByIdAsync(current.PlanId);
            var currentOrder = PlanOrder.GetValueOrDefault(currentPlan?.Nombre ?? PlanNamePolicy.Free, 0);
            var newOrder = PlanOrder.GetValueOrDefault(plan.Nombre, 0);
            if (newOrder <= currentOrder && current.Estado is "Activa" or "Grace")
                throw new ConflictException("Solo puedes activar un plan superior; usa renovación para conservar el actual.");
        }

        var now = DateTime.UtcNow;
        if (current is not null)
        {
            current.Estado = "Reemplazada";
            current.CanceladaEn = now;
            current.MotivoCancelacion = $"Cambio a {PlanNamePolicy.ToPublicName(plan.Nombre)}";
            current.AutoRenew = false;
            current.GraceEndsAtUtc = null;
            current.UpdatedAtUtc = now;
            await _suscripcionRepository.UpdateAsync(current);
        }

        var subscription = new Suscripcion
        {
            UsuarioId = usuarioId,
            PlanId = plan.Id,
            Estado = "Activa",
            Inicio = now,
            Fin = AddBillingPeriod(now, billingCycle),
            NextBillingAtUtc = AddBillingPeriod(now, billingCycle),
            BillingCycle = billingCycle,
            AutoRenew = true,
            UpdatedAtUtc = now
        };
        await _suscripcionRepository.AddAsync(subscription);

        var payment = CreatePayment(usuarioId, subscription, plan, billingCycle, request.MetodoPago, now);
        await _pagoRepository.AddAsync(payment);
        subscription.LastPaymentId = payment.Id;
        await _suscripcionRepository.UpdateAsync(subscription);
        await SetUserPlanAsync(usuarioId, plan.Nombre);

        return new SubscriptionPaymentResultDto
        {
            Subscription = MapToSuscripcionDto(subscription, plan),
            Payment = MapToPagoDto(payment)
        };
    }

    public async Task<SubscriptionPaymentResultDto> RenewAsync(
        Guid usuarioId,
        RenewSubscriptionRequest request)
    {
        var subscription = await GetCurrentAsync(usuarioId)
            ?? throw new ConflictException("No tienes una suscripción renovable.");
        var plan = await _planRepository.GetByIdAsync(subscription.PlanId)
            ?? throw new BadRequestException("El plan asociado ya no existe.");
        if (PlanNamePolicy.ToStorageName(plan.Nombre) == PlanNamePolicy.Free)
            throw new ConflictException("El plan Free no requiere renovación.");

        var now = DateTime.UtcNow;
        var billingCycle = NormalizeBillingCycle(subscription.BillingCycle);
        var baseDate = subscription.Fin is not null && subscription.Fin > now
            ? subscription.Fin.Value
            : now;
        subscription.Estado = "Activa";
        subscription.Fin = AddBillingPeriod(baseDate, billingCycle);
        subscription.NextBillingAtUtc = subscription.Fin;
        subscription.GraceEndsAtUtc = null;
        subscription.CanceladaEn = null;
        subscription.MotivoCancelacion = null;
        subscription.UpdatedAtUtc = now;

        var payment = CreatePayment(usuarioId, subscription, plan, billingCycle, request.MetodoPago, now);
        await _pagoRepository.AddAsync(payment);
        subscription.LastPaymentId = payment.Id;
        await _suscripcionRepository.UpdateAsync(subscription);
        await SetUserPlanAsync(usuarioId, plan.Nombre);

        return new SubscriptionPaymentResultDto
        {
            Subscription = MapToSuscripcionDto(subscription, plan),
            Payment = MapToPagoDto(payment)
        };
    }

    public async Task<SuscripcionDto> CancelSubscriptionAsync(
        Guid usuarioId,
        CancelSubscriptionRequest? request)
    {
        var subscription = await GetCurrentAsync(usuarioId)
            ?? throw new ConflictException("No tienes una suscripción activa.");
        var plan = await _planRepository.GetByIdAsync(subscription.PlanId);
        if (PlanNamePolicy.ToStorageName(plan?.Nombre) == PlanNamePolicy.Free)
            throw new ConflictException("El plan Free no se puede cancelar.");

        subscription.Estado = "Cancelada";
        subscription.CanceladaEn = DateTime.UtcNow;
        subscription.MotivoCancelacion = request?.Motivo;
        subscription.AutoRenew = false;
        subscription.GraceEndsAtUtc = null;
        subscription.UpdatedAtUtc = DateTime.UtcNow;
        await _suscripcionRepository.UpdateAsync(subscription);
        await SetUserPlanAsync(usuarioId, PlanNamePolicy.Free);
        return MapToSuscripcionDto(subscription, plan);
    }

    public async Task<List<PagoDto>> GetPaymentsAsync(Guid usuarioId)
    {
        var payments = await _pagoRepository.GetByUserAsync(usuarioId);
        return payments.Select(MapToPagoDto).ToList();
    }

    public async Task<PagedResult<PagoDto>> GetPaymentsPagedAsync(
        Guid usuarioId,
        int? pageSize,
        string? continuationToken)
    {
        var size = PaginationValidator.Resolve(pageSize, continuationToken);
        var page = await _pagoRepository.GetByUserPagedAsync(usuarioId, size, continuationToken);
        return new PagedResult<PagoDto>
        {
            Items = page.Items.Select(MapToPagoDto).ToList(),
            ContinuationToken = page.ContinuationToken,
            HasMoreResults = page.HasMoreResults,
            PageSize = page.PageSize,
        };
    }

    public async Task<PagoDto?> GetPaymentReceiptAsync(Guid id, Guid usuarioId)
    {
        var payment = await _pagoRepository.GetByIdAsync(usuarioId, id);
        return payment is null || payment.UsuarioId != usuarioId ? null : MapToPagoDto(payment);
    }

    public Task<int> ExpireSubscriptionsAsync()
        => ProcessLifecycleAsync(DateTime.UtcNow);

    public async Task<int> ProcessLifecycleAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await _suscripcionRepository.ProcessLifecycleAsync(
            utcNow,
            async (subscription, ct) =>
            {
                await ApplyLifecycleAsync(subscription, utcNow);
            },
            cancellationToken);
    }

    private async Task<Suscripcion?> GetCurrentAsync(Guid userId)
    {
        return await _suscripcionRepository.GetCurrentByUserAsync(userId)
            ?? await _suscripcionRepository.GetActiveByUserAsync(userId);
    }

    private async Task<bool> ApplyLifecycleAsync(Suscripcion subscription, DateTime utcNow)
    {
        if (subscription.Estado is "Activa" or "Trial"
            && subscription.Fin is not null
            && subscription.Fin <= utcNow)
        {
            var plan = await _planRepository.GetByIdAsync(subscription.PlanId);
            if (PlanNamePolicy.ToStorageName(plan?.Nombre) == PlanNamePolicy.Free)
                return false;

            subscription.Estado = "Grace";
            subscription.GraceEndsAtUtc = subscription.Fin.Value.Add(GracePeriod);
            subscription.UpdatedAtUtc = utcNow;
            await _suscripcionRepository.UpdateAsync(subscription);
            return true;
        }

        if (subscription.Estado == "Grace"
            && subscription.GraceEndsAtUtc is not null
            && subscription.GraceEndsAtUtc <= utcNow)
        {
            subscription.Estado = "Expirada";
            subscription.UpdatedAtUtc = utcNow;
            await _suscripcionRepository.UpdateAsync(subscription);
            await SetUserPlanAsync(subscription.UsuarioId, PlanNamePolicy.Free);
            return true;
        }

        return false;
    }

    private async Task SetUserPlanAsync(Guid userId, string planName)
    {
        var user = await _usuarioRepository.GetByIdAsync(userId);
        if (user is null)
            return;
        user.PlanActivo = PlanNamePolicy.ToPublicName(planName);
        await _usuarioRepository.UpdateAsync(user);
    }

    private static Pago CreatePayment(
        Guid userId,
        Suscripcion subscription,
        Plan plan,
        string billingCycle,
        string? paymentMethod,
        DateTime now)
    {
        return new Pago
        {
            UsuarioId = userId,
            SuscripcionId = subscription.Id,
            Monto = billingCycle == "Annual" ? plan.PrecioAnual : plan.PrecioMensual,
            Moneda = "MXN",
            MetodoPago = string.IsNullOrWhiteSpace(paymentMethod) ? "Simulated" : paymentMethod.Trim(),
            Estado = "Completado",
            FechaPago = now,
            Referencia = $"SIM-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..32]
        };
    }

    private static string NormalizeBillingCycle(string? value)
    {
        if (string.Equals(value, "Annual", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Anual", StringComparison.OrdinalIgnoreCase))
            return "Annual";
        if (string.IsNullOrWhiteSpace(value)
            || string.Equals(value, "Monthly", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "Mensual", StringComparison.OrdinalIgnoreCase))
            return "Monthly";
        throw new BadRequestException("BillingCycle debe ser Monthly o Annual.");
    }

    private static DateTime AddBillingPeriod(DateTime value, string billingCycle)
        => billingCycle == "Annual" ? value.AddYears(1) : value.AddMonths(1);

    private static bool TryResolveFamilyEntitlement(
        FamilySubscription family,
        DateTime utcNow,
        out string state,
        out DateTime? graceEndsAtUtc)
    {
        state = "Activa";
        graceEndsAtUtc = family.GraceEndsAtUtc;

        if (family.Status == FamilySubscriptionStatus.Active)
        {
            if (family.PeriodEndUtc > utcNow)
                return true;

            graceEndsAtUtc = family.PeriodEndUtc.Add(GracePeriod);
            if (graceEndsAtUtc > utcNow)
            {
                state = "Grace";
                return true;
            }

            return false;
        }

        if (family.Status == FamilySubscriptionStatus.PastDue
            && family.GraceEndsAtUtc is not null
            && family.GraceEndsAtUtc > utcNow)
        {
            state = "Grace";
            return true;
        }

        return false;
    }

    private static EffectiveSubscriptionDto BuildEffective(
        string planName,
        string source,
        string state,
        bool isOwner,
        DateTime? validUntil,
        DateTime? graceEnds)
    {
        var publicName = PlanNamePolicy.ToPublicName(planName);
        return new EffectiveSubscriptionDto
        {
            PlanNombre = publicName,
            Source = source,
            Estado = state,
            IsOwner = isOwner,
            ValidUntilUtc = validUntil,
            GraceEndsAtUtc = graceEnds,
            VehicleLimit = publicName switch
            {
                PlanNamePolicy.Standard => 3,
                PlanNamePolicy.Premium => -1,
                _ => 1
            },
            InvitedMemberLimit = publicName switch
            {
                PlanNamePolicy.Standard => 2,
                PlanNamePolicy.Premium => 5,
                _ => 1
            },
            MonitoringLimit = publicName switch
            {
                PlanNamePolicy.Standard => 3,
                PlanNamePolicy.Premium => 6,
                _ => 1
            },
            MapHistoryEnabled = publicName == PlanNamePolicy.Premium,
            ExportEnabled = publicName == PlanNamePolicy.Premium
        };
    }

    private static PlanDto MapToPlanDto(Plan plan) => new()
    {
        Id = plan.Id,
        Nombre = PlanNamePolicy.ToPublicName(plan.Nombre),
        PrecioMensual = plan.PrecioMensual,
        PrecioAnual = plan.PrecioAnual,
        MaxContactos = plan.MaxContactos,
        MaxMonitores = plan.MaxMonitores,
        HistorialMapa = plan.HistorialMapa,
        ExportacionDatos = plan.ExportacionDatos,
        SoportePrioritario = plan.SoportePrioritario,
        DuracionTrialDias = plan.DuracionTrialDias,
    };

    private static SuscripcionDto MapToSuscripcionDto(Suscripcion subscription, Plan? plan) => new()
    {
        Id = subscription.Id,
        PlanId = subscription.PlanId,
        PlanNombre = PlanNamePolicy.ToPublicName(plan?.Nombre),
        Estado = subscription.Estado,
        Inicio = subscription.Inicio,
        Fin = subscription.Fin,
        TrialFin = subscription.TrialFin,
        GraceEndsAtUtc = subscription.GraceEndsAtUtc,
        NextBillingAtUtc = subscription.NextBillingAtUtc,
        BillingCycle = subscription.BillingCycle,
        AutoRenew = subscription.AutoRenew,
        LastPaymentId = subscription.LastPaymentId,
        CanceladaEn = subscription.CanceladaEn,
        MotivoCancelacion = subscription.MotivoCancelacion,
    };

    private static PagoDto MapToPagoDto(Pago payment) => new()
    {
        Id = payment.Id,
        SuscripcionId = payment.SuscripcionId,
        Monto = payment.Monto,
        Moneda = payment.Moneda,
        MetodoPago = payment.MetodoPago,
        Estado = payment.Estado,
        FechaPago = payment.FechaPago,
        Referencia = payment.Referencia,
        ComprobanteUrl = payment.ComprobanteUrl,
    };
}
