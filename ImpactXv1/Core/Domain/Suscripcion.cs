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
}
