using System.ComponentModel.DataAnnotations;
using ImpactX.Core.Domain.Enums;

namespace ImpactX.Models.DTOs.Vehicles;

public class CreateVehicleRequest
{
    [Required(ErrorMessage = "El tipo de vehículo es obligatorio.")]
    public TipoVehiculo TipoVehiculo { get; set; }

    [Required(ErrorMessage = "La marca es obligatoria.")]
    [MaxLength(100, ErrorMessage = "La marca no puede exceder los 100 caracteres.")]
    public string Marca { get; set; } = string.Empty;

    [Required(ErrorMessage = "El modelo es obligatorio.")]
    [MaxLength(100, ErrorMessage = "El modelo no puede exceder los 100 caracteres.")]
    public string Modelo { get; set; } = string.Empty;

    [Range(1886, 2100, ErrorMessage = "El año debe estar entre 1886 y 2100.")]
    public int Ano { get; set; }

    [Range(0.0, 300.0, ErrorMessage = "La velocidad promedio debe estar entre 0 y 300.")]
    public double VelocidadPromedio { get; set; }

    [Required(ErrorMessage = "El uso principal del vehículo es obligatorio.")]
    public UsoPrincipalVehiculo UsoPrincipalVehiculo { get; set; }

    public bool? EsPrincipal { get; set; }
}
