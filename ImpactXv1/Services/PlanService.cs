using ImpactX.Core.Domain;
using ImpactX.Core.Exceptions;
using ImpactX.Core.Interfaces.Repositories;
using ImpactX.Models.DTOs;

namespace ImpactX.Services;

public class PlanService : IPlanService
{
    private readonly IPlanRepository _planRepository;
    private readonly ISuscripcionRepository _suscripcionRepository;
    private readonly IPagoRepository _pagoRepository;
    private readonly IUsuarioRepository _usuarioRepository;

    private static readonly Dictionary<string, int> PlanOrder = new()
    {
        ["Free"] = 0,
        ["Trial"] = 0,
        ["Basic"] = 1,
        ["Premium"] = 2,
        ["Enterprise"] = 3,
    };

    public PlanService(
        IPlanRepository planRepository,
        ISuscripcionRepository suscripcionRepository,
        IPagoRepository pagoRepository,
        IUsuarioRepository usuarioRepository)
    {
        _planRepository = planRepository;
        _suscripcionRepository = suscripcionRepository;
        _pagoRepository = pagoRepository;
        _usuarioRepository = usuarioRepository;
    }

    public async Task<List<PlanDto>> GetAllPlansAsync()
    {
        var plans = await _planRepository.GetAllAsync();
        return plans.Select(MapToPlanDto).ToList();
    }

    public async Task<SuscripcionDto?> GetCurrentSubscriptionAsync(Guid usuarioId)
    {
        var suscripcion = await _suscripcionRepository.GetActiveByUserAsync(usuarioId);
        if (suscripcion is null) return null;

        var plan = await _planRepository.GetByIdAsync(suscripcion.PlanId);
        return MapToSuscripcionDto(suscripcion, plan);
    }

    public async Task<List<SuscripcionDto>> GetSubscriptionHistoryAsync(Guid usuarioId)
    {
        var historial = await _suscripcionRepository.GetHistoryByUserAsync(usuarioId);
        var dtos = new List<SuscripcionDto>();
        foreach (var s in historial)
        {
            var plan = await _planRepository.GetByIdAsync(s.PlanId);
            dtos.Add(MapToSuscripcionDto(s, plan));
        }
        return dtos;
    }

    public async Task<SuscripcionDto> ChangePlanAsync(Guid usuarioId, ChangePlanRequest request)
    {
        var plan = await _planRepository.GetByNameAsync(request.PlanNombre);
        if (plan is null)
            throw new BadRequestException("Plan no encontrado.");

        var current = await _suscripcionRepository.GetActiveByUserAsync(usuarioId);

        if (current is not null)
        {
            var currentPlan = await _planRepository.GetByIdAsync(current.PlanId);
            var currentOrder = currentPlan is not null
                ? PlanOrder.GetValueOrDefault(currentPlan.Nombre, 0) : 0;
            var newOrder = PlanOrder.GetValueOrDefault(plan.Nombre, 0);

            if (newOrder <= currentOrder)
                throw new ConflictException(
                    "Solo puedes cambiar a un plan superior.");
        }

        var now = DateTime.UtcNow;
        var suscripcion = new Suscripcion
        {
            UsuarioId = usuarioId,
            PlanId = plan.Id,
            Estado = "Activa",
            Inicio = now,
            Fin = now.AddMonths(1),
        };

        await _suscripcionRepository.AddAsync(suscripcion);

        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is not null)
        {
            usuario.PlanActivo = plan.Nombre;
            await _usuarioRepository.UpdateAsync(usuario);
        }

        return MapToSuscripcionDto(suscripcion, plan);
    }

    public async Task<SuscripcionDto> CancelSubscriptionAsync(
        Guid usuarioId, CancelSubscriptionRequest? request)
    {
        var suscripcion = await _suscripcionRepository.GetActiveByUserAsync(usuarioId);
        if (suscripcion is null)
            throw new ConflictException("No tienes una suscripción activa.");

        suscripcion.Estado = "Cancelada";
        suscripcion.CanceladaEn = DateTime.UtcNow;
        suscripcion.MotivoCancelacion = request?.Motivo;

        await _suscripcionRepository.UpdateAsync(suscripcion);

        var plan = await _planRepository.GetByIdAsync(suscripcion.PlanId);

        var usuario = await _usuarioRepository.GetByIdAsync(usuarioId);
        if (usuario is not null)
        {
            usuario.PlanActivo = "Free";
            await _usuarioRepository.UpdateAsync(usuario);
        }

        return MapToSuscripcionDto(suscripcion, plan);
    }

    public async Task<List<PagoDto>> GetPaymentsAsync(Guid usuarioId)
    {
        var pagos = await _pagoRepository.GetByUserAsync(usuarioId);
        return pagos.Select(MapToPagoDto).ToList();
    }

    public async Task<PagoDto?> GetPaymentReceiptAsync(Guid id, Guid usuarioId)
    {
        var pago = await _pagoRepository.GetByIdAsync(id);
        return pago is null || pago.UsuarioId != usuarioId ? null : MapToPagoDto(pago);
    }

    public async Task<int> ExpireSubscriptionsAsync()
    {
        var expired = await _suscripcionRepository.GetExpiredAsync();
        foreach (var s in expired)
        {
            s.Estado = "Expirada";
            await _suscripcionRepository.UpdateAsync(s);

            var usuario = await _usuarioRepository.GetByIdAsync(s.UsuarioId);
            if (usuario is not null)
            {
                usuario.PlanActivo = "Free";
                await _usuarioRepository.UpdateAsync(usuario);
            }
        }
        return expired.Count;
    }

    private static PlanDto MapToPlanDto(Plan p) => new()
    {
        Id = p.Id,
        Nombre = p.Nombre,
        PrecioMensual = p.PrecioMensual,
        PrecioAnual = p.PrecioAnual,
        MaxContactos = p.MaxContactos,
        MaxMonitores = p.MaxMonitores,
        HistorialMapa = p.HistorialMapa,
        ExportacionDatos = p.ExportacionDatos,
        SoportePrioritario = p.SoportePrioritario,
        DuracionTrialDias = p.DuracionTrialDias,
    };

    private static SuscripcionDto MapToSuscripcionDto(Suscripcion s, Plan? p) => new()
    {
        Id = s.Id,
        PlanId = s.PlanId,
        PlanNombre = p?.Nombre ?? "Desconocido",
        Estado = s.Estado,
        Inicio = s.Inicio,
        Fin = s.Fin,
        TrialFin = s.TrialFin,
        CanceladaEn = s.CanceladaEn,
        MotivoCancelacion = s.MotivoCancelacion,
    };

    private static PagoDto MapToPagoDto(Pago p) => new()
    {
        Id = p.Id,
        SuscripcionId = p.SuscripcionId,
        Monto = p.Monto,
        Moneda = p.Moneda,
        MetodoPago = p.MetodoPago,
        Estado = p.Estado,
        FechaPago = p.FechaPago,
        Referencia = p.Referencia,
        ComprobanteUrl = p.ComprobanteUrl,
    };
}
