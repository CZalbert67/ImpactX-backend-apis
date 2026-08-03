# Plan Integral — ImpactX + Azure Cosmos DB

## Estado del Proyecto

| Componente | Estado |
|---|---|
| **Tarea 0** (Setup) | ✅ Parcial — estructura de capas, JWT, middlewares |
| **Tarea 1** (Auth) | ✅ Completo — register, login, logout, recover/reset password, sessions, account export/delete |
| **Tareas 2–14** | ❌ No implementadas |
| **BD Producción** | ❌ Cosmos DB vacío (`ImpactX-Data`) |
| **BD Pruebas** | ❌ Cosmos DB vacío (`TestDatabase`) |
| **BD Desarrollo** | ✅ EF Core InMemory funcional |

---

## Contenedores Cosmos DB — Versión Final

| # | Contenedor | Partition Key | TTL |
|---|---|---|---|
| 1 | **Usuarios** | `/id` | — |
| 2 | **RefreshTokens** | `/usuarioId` | 7d |
| 3 | **PasswordResetTokens** | `/usuarioId` | 1h |
| 4 | **Planes** | `/id` | — |
| 5 | **Suscripciones** | `/usuarioId` | — |
| 6 | **Pagos** | `/usuarioId` | — |
| 7 | **Monitores** | `/usuarioId` | — |
| 8 | **ContactosEmergencia** | `/usuarioId` | — |
| 9 | **Rutas** | `/usuarioId` | — |
| 10 | **Viajes** | `/usuarioId` | 90d |
| 11 | **TelemetriaViaje** | `/viajeId` | 90d |
| 12 | **Alertas** | `/usuarioId` | 365d |
| 13 | **Notificaciones** | `/usuarioId` | 30d |
| 14 | **Wearables** | `/usuarioId` | — |
| 15 | **AppInvites** | `/usuarioId` | 30d |
| 16 | **ChatThreads** | `/usuarioId` | — |

---

## Modelos de Datos

### Usuario (con subdocumentos embebidos)

```csharp
public class Usuario
{
    public Guid Id { get; set; }
    public string Username { get; set; }
    public string AppId { get; set; }
    public string InviteCode { get; set; }
    public string Nombre { get; set; }
    public string Correo { get; set; }
    public string Telefono { get; set; }
    public string PasswordHash { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public bool EmailConfirmed { get; set; }
    public PerfilConduccion? PerfilConduccion { get; set; }
    public FichaMedica? FichaMedica { get; set; }
    public PreferenciasUsuario? Preferencias { get; set; }
    public PermisosApp? Permisos { get; set; }
    public SettingsUsuario? Settings { get; set; }
}
```

### PerfilConduccion

```csharp
public class PerfilConduccion
{
    public string? TipoVehiculo { get; set; }    // Validado contra el catálogo de TipoVehiculo (case-insensitive)
    public string? Marca { get; set; }
    public string? Modelo { get; set; }
    public int? Anio { get; set; }
    public string? Color { get; set; }
    public string? Placa { get; set; }
    public string? Uso { get; set; }              // Validado contra el catálogo de UsoPrincipalVehiculo (case-insensitive)
    public string? VelocidadPromedioLabel { get; set; } // "65 km/h"
}
```

### FichaMedica

```csharp
public class FichaMedica
{
    public string? TipoSangre { get; set; }
    public string? Alergias { get; set; }
    public string? Condiciones { get; set; }
    public string? Medicamentos { get; set; }
    public string? Nota { get; set; }
}
```

---

## Arquitectura

```
Controllers → Services → Repositories (Interfaces)
                         ├── EF Core InMemory (dev)
                         └── Cosmos DB (prod/test)

CosmosDbContext (Singleton) — CosmosClient + Containers
```

## Configuración por Environment

| Environment | UseCosmosDb | DatabaseName |
|---|---|---|
| Development | true | TestDatabase |
| Production | true | ImpactX-Data |

---

## Fases de Implementación

| Fase | Tareas | Duración estimada |
|---|---|---|
| **F0** | Setup Cosmos SDK + CosmosDbContext + appsettings | ~2h |
| **F1** | Refactor Auth a Cosmos DB (repos) | ~3h |
| **F2** | Perfil + Preferencias + Permisos + Settings | ~4h |
| **F3** | Planes + Suscripciones + Pagos | ~6h |
| **F4** | Wearable + Telemetría | ~5h |
| **F5** | Contactos + Monitores | ~6h |
| **F6** | Rutas + Viajes | ~8h |
| **F7** | SOS + Incidentes | ~6h |
| **F8** | Notificaciones + Analytics | ~4h |
| **F9** | QA + Documentación + DevSecOps | ~6h |
