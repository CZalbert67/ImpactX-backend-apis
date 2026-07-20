namespace ImpactX.Models.DTOs;

public class DashboardDto
{
    public int TotalIncidentes { get; set; }
    public int ContactosActivos { get; set; }
    public int ViajesRegistrados { get; set; }
    public int DiasDePlan { get; set; }
    public string PlanActual { get; set; } = string.Empty;
    public string? CanalMasUsado { get; set; }
}

public class IncidentTrendPointDto
{
    public string Periodo { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Bumps { get; set; }
    public int Crashes { get; set; }
    public int Severe { get; set; }
}

public class TripSummaryDto
{
    public int TotalViajes { get; set; }
    public double DistanciaTotalKm { get; set; }
    public double VelocidadPromedio { get; set; }
    public double VelocidadMaxima { get; set; }
    public string? RiesgoMasFrecuente { get; set; }
    public int DuracionTotalMinutos { get; set; }
}

public class TrendFilterRequest
{
    public string Agrupacion { get; set; } = "week";
    public int Meses { get; set; } = 6;
}
