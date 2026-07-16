namespace Prueba1.Core.Domain;

public class Pago
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public Guid SuscripcionId { get; set; }
    public decimal Monto { get; set; }
    public string Moneda { get; set; } = "MXN";
    public string MetodoPago { get; set; } = string.Empty;
    public string Estado { get; set; } = "Pendiente";
    public DateTime FechaPago { get; set; } = DateTime.UtcNow;
    public string? Referencia { get; set; }
    public string? ComprobanteUrl { get; set; }
}
