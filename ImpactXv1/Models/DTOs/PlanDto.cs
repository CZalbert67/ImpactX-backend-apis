using System.ComponentModel.DataAnnotations;

namespace ImpactX.Models.DTOs;

public class PlanDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public decimal PrecioMensual { get; set; }
    public decimal PrecioAnual { get; set; }
    public int MaxContactos { get; set; }
    public int MaxMonitores { get; set; }
    public bool HistorialMapa { get; set; }
    public bool ExportacionDatos { get; set; }
    public bool SoportePrioritario { get; set; }
    public int DuracionTrialDias { get; set; }
}

public class SuscripcionDto
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public string PlanNombre { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime Inicio { get; set; }
    public DateTime? Fin { get; set; }
    public DateTime? TrialFin { get; set; }
    public DateTime? GraceEndsAtUtc { get; set; }
    public DateTime? NextBillingAtUtc { get; set; }
    public string BillingCycle { get; set; } = "Monthly";
    public bool AutoRenew { get; set; }
    public Guid? LastPaymentId { get; set; }
    public DateTime? CanceladaEn { get; set; }
    public string? MotivoCancelacion { get; set; }
    public bool IsActive => Estado is "Trial" or "Activa" or "Grace";
}

public sealed class EffectiveSubscriptionDto
{
    public string PlanNombre { get; set; } = "Free";
    public string Source { get; set; } = "Free";
    public string Estado { get; set; } = "Activa";
    public bool IsOwner { get; set; }
    public DateTime? ValidUntilUtc { get; set; }
    public DateTime? GraceEndsAtUtc { get; set; }
    public int VehicleLimit { get; set; } = 1;
    public int InvitedMemberLimit { get; set; } = 1;
    public int MonitoringLimit { get; set; } = 1;
    public bool MapHistoryEnabled { get; set; }
    public bool ExportEnabled { get; set; }
}

public class PagoDto
{
    public Guid Id { get; set; }
    public Guid SuscripcionId { get; set; }
    public decimal Monto { get; set; }
    public string Moneda { get; set; } = string.Empty;
    public string MetodoPago { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;
    public DateTime FechaPago { get; set; }
    public string? Referencia { get; set; }
    public string? ComprobanteUrl { get; set; }
}

public class ChangePlanRequest
{
    [Required]
    public string PlanNombre { get; set; } = string.Empty;
    public string BillingCycle { get; set; } = "Monthly";
    public string MetodoPago { get; set; } = "Simulated";
}

public sealed class RenewSubscriptionRequest
{
    public string MetodoPago { get; set; } = "Simulated";
}

public sealed class SubscriptionPaymentResultDto
{
    public SuscripcionDto Subscription { get; set; } = new();
    public PagoDto Payment { get; set; } = new();
}

public class CancelSubscriptionRequest
{
    public string? Motivo { get; set; }
}
