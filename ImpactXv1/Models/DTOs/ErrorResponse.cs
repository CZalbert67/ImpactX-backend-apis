namespace ImpactX.Models.DTOs;

public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public string? Detalle { get; set; }
    public string? TraceId { get; set; }
}