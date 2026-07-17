namespace ImpactX.Models.DTOs;

public class ContactoDto
{
    public Guid Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Parentesco { get; set; }
    public string? Username { get; set; }
    public string? AppUserId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public bool EsPrincipal { get; set; }
    public DateTime CreadoEn { get; set; }
}

public class CreateContactoRequest
{
    public string Nombre { get; set; } = string.Empty;
    public string Telefono { get; set; } = string.Empty;
    public string? Parentesco { get; set; }
    public string? Username { get; set; }
    public string? AppUserId { get; set; }
    public string Priority { get; set; } = "Secundario";
    public bool EsPrincipal { get; set; }
}

public class UpdateContactoRequest
{
    public string? Nombre { get; set; }
    public string? Telefono { get; set; }
    public string? Parentesco { get; set; }
    public string? Priority { get; set; }
}

public class MakePrimaryRequest
{
    public Guid ContactoId { get; set; }
}

public class SyncContactosResponse
{
    public List<ContactoDto> Contactos { get; set; } = [];
    public DateTime SincronizadoEn { get; set; } = DateTime.UtcNow;
}
