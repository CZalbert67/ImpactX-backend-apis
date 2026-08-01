namespace ImpactX.Core.Domain;

public class FichaMedica
{
    public string? TipoSangre { get; set; }
    public string? Alergias { get; set; }
    public string? Condiciones { get; set; }
    public string? Medicamentos { get; set; }
    public string? Nota { get; set; }
    public string? TienePadecimiento { get; set; }
    public bool CompartirFichaMedica { get; set; }
    public bool PermitirUbicacion { get; set; }
    public bool PermitirAprendizajeIA { get; set; }
}
