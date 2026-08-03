using System.Text.Json.Serialization;

namespace ImpactX.Core.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum TipoVehiculo
{
    Automovil,
    Suv,
    Camioneta,
    Van,
    Camion,
    Autobus,
    Deportivo
}

[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(Newtonsoft.Json.Converters.StringEnumConverter))]
public enum UsoPrincipalVehiculo
{
    Ciudad,
    Carretera,
    Mixto,
    TodoTerreno,
    Comercial
}
