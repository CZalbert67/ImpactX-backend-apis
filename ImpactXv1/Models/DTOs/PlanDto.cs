namespace Prueba1.Models.DTOs;

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
    public DateTime? CanceladaEn { get; set; }
    public string? MotivoCancelacion { get; set; }
    public bool IsActive => Estado == "Trial" || Estado == "Activa";
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
    public string PlanNombre { get; set; } = string.Empty;
}

public class CancelSubscriptionRequest
{
    public string? Motivo { get; set; }
}
