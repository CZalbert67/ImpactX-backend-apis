namespace Prueba1.Core.Domain;

public class ChatThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UsuarioId { get; set; }
    public Guid ContactId { get; set; }
    public string? ContactUsername { get; set; }
    public string? ContactName { get; set; }
    public string? UltimoMensaje { get; set; }
    public DateTime? UltimoMensajeEn { get; set; }
    public int NoLeidos { get; set; }
    public DateTime CreadoEn { get; set; } = DateTime.UtcNow;
}
