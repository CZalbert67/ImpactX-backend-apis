namespace ImpactX.Core.Domain;

public class Suscripcion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public Guid PlanId { get; set; }
    public string Estado { get; set; } = "Trial";
    public DateTime Inicio { get; set; } = DateTime.UtcNow;
    public DateTime? Fin { get; set; }
    public DateTime? TrialFin { get; set; }
    public DateTime? CanceladaEn { get; set; }
    public string? MotivoCancelacion { get; set; }
    public string BillingCycle { get; set; } = "Monthly";
    public bool AutoRenew { get; set; } = true;
    public DateTime? GraceEndsAtUtc { get; set; }
    public DateTime? NextBillingAtUtc { get; set; }
    public Guid? LastPaymentId { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
