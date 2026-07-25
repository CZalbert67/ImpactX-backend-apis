# Auditoría de Línea Base — ImpactX Backend APIs

## Datos de la auditoría

| Campo | Valor |
|---|---|
| Rama auditada | `feat/backend-platform-foundation` |
| Último commit | `1f5cb83` — feat: add 4 new DevSecOps & banking-grade security pipelines |
| .NET SDK | 10.0.110 |
| Framework objetivo | `net10.0` |
| Solución | `ImpactX.slnx` |
| Proyecto API | `ImpactXv1/ImpactX.Api.csproj` |
| Proyecto de pruebas | `ImpactX.Tests/ImpactX.Tests.csproj` |
| Build Release | Correcto |
| Pruebas | **306 de 306** correctas (14 unit + 16 integration) |

---

## Requisitos que cumple

| # | Requisito | Verificación |
|---|---|---|
| 1 | .NET 10 | Ambos `.csproj` usan `net10.0`. Paquetes NuGet en versiones `10.0.9` |
| 2 | UUID como identificadores | Todos los Id de dominio y DTOs son `Guid`. `Guid.NewGuid()` como valor por defecto |
| 3 | JSON camelCase | System.Text.Json serializa en camelCase por defecto. No se registró `AddNewtonsoftJson()` |
| 4 | Fechas en UTC | Todos los valores por defecto usan `DateTime.UtcNow`. No hay `DateTime.Now` en el código |
| 5 | Bearer JWT | HMAC-SHA256. `ConfigureJwtAuthentication()` en pipeline. `ClockSkew = TimeSpan.Zero` |
| 6 | 306 pruebas | 14 unit (Moq) + 16 integration (`WebApplicationFactory<Program>`) |
| 7 | 15 controladores | Auth, Users, Plans, Subscription, Wearable, Permissions, Contacts, Monitors, Routes, Trips, Alertas, Incidentes, Notificaciones, Analytics, Settings |
| 8 | 22 entidades de dominio | Cobertura completa de todos los modelos del plan original |
| 9 | 23 DTOs | Todos los request/response mapeados |
| 10 | 14 repositorios + 2 implementaciones | EF InMemory y Cosmos DB por cada interfaz |
| 11 | 14 servicios con interfaz | Capa de negocio completa |
| 12 | Middleware completo | ExceptionHandling, RequestLogging, SecurityHeaders |
| 13 | CORS configurado | Policy `AllowLocalhost` |
| 14 | Docker multi-stage | `mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0`, puerto 8080 |
| 15 | Health checks | `GET /health`, `GET /health/live`, `GET /health/ready` con ResponseWriter unificado |
| 16 | OpenAPI | `/openapi/v1.json` + Scalar UI + Swagger UI (`/swagger`) solo en Development |
| 17 | 6 pipelines CI/CD | .NET CI, Azure deploy, OWASP audit, Gitleaks, CodeQL, Roslyn format |

---

## Requisitos pendientes

| # | Requisito | Estado actual | Impacto |
|---|---|---|---|
| 1 | Versionado de API (`/api/v1/`) | Rutas como `api/auth`, `api/users` | Dificulta evolución sin romper clientes |
| 2 | `X-Correlation-Id` | No implementado | Sin trazabilidad de extremo a extremo |
| 3 | `traceparent` / OpenTelemetry | ASP.NET Core reconoce `traceparent` internamente, pero no hay integración explícita de trazabilidad distribuida ni OpenTelemetry | Sin visibilidad de tracing entre servicios |
| 4 | Problem Details (RFC 7807) | Usa `ErrorResponse` custom | No sigue estándar `application/problem+json` |
| 5 | Fechas como `DateTimeOffset` | Todas las fechas son `DateTime` | Se pierde información de zona horaria |
| 6 | Rate limiting | No implementado | Riesgo en endpoints públicos (login/register) |
| 7 | CSRF | No implementado. Depende del método de almacenamiento y transporte del JWT | Evaluar según la estrategia de almacenamiento del token |
| 8 | `IEmailService` real | Solo existe `StubEmailService` | Sin envío real de correos |

---

## Riesgos prioritarios

| ID | Riesgo | Severidad | Detalle |
|---|---|---|---|
| R01 | JWT secret hardcodeado como fallback activo | 🔴 Crítico | `ServiceCollectionExtensions.cs:84` y `JwtTokenService.cs:26` tienen `"ImpactX_Super_Secret_JWT_Key_2026_Executive_Key_V12!"` como fallback si la config no tiene clave. Cualquier persona puede firmar JWTs |
| R02 | CORS permisivo con AllowCredentials | 🔴 Crítico | `SetIsOriginAllowed(_ => true)` + `AllowCredentials()` permite cualquier origen con credenciales |
| R03 | Cosmos key placeholder en 3 appsettings | 🔴 Crítico | `Key: "YOUR_AZURE_COSMOS_KEY"`. El **endpoint** (`https://impactx-db-west-final.documents.azure.com:443/`) no es una credencial pero debe venir de configuración por ambiente. La combinación endpoint + key expuesta en el repositorio es el riesgo |
| R04 | `test_output.txt` eliminado del repositorio | 🟠 Importante | Archivo de 241KB versionado. Ya fue eliminado del sistema de archivos y agregado al `.gitignore` |
| R05 | Production JWT secret vacío | 🟠 Importante | `appsettings.Production.json` tiene `"Secret": ""`. El fallback hardcodeado (R01) lo cubre, lo que agrava el riesgo |
| R06 | Sin analizadores Roslyn | 🟠 Importante | No hay `Directory.Build.props` ni `<AnalysisMode>`. Solo se ejecuta `dotnet format --verify-no-changes` en CI |

---

## Orden inicial de implementación

| Paso | Cambio | Dependencias |
|---|---|---|
| 1 | Eliminar `test_output.txt` del repositorio y registrar la eliminación con git add -A | ✅ Hecho |
| 2 | Restringir CORS a orígenes conocidos o eliminar `AllowCredentials` | Ninguna |
| 3 | Eliminar fallback hardcodeado del JWT secret. Exigir clave por config/env/user-secrets | Ninguna |
| 4 | Mover Cosmos endpoint+key a user secrets / env vars. Reemplazar en appsettings por placeholders inertes | Ninguna |
| 5 | Agregar `AddApiVersioning()` y prefijo `api/v1/` en rutas | Paso 9 (tests) |
| 6 | Middleware `X-Correlation-Id` | Ninguna |
| 7 | Migrar error handling a `ProblemDetails` (RFC 7807) | Paso 9 (tests) |
| 8 | `/health/live` + `/health/ready` | ✅ Hecho |
| 9 | Actualizar tests de integración para reflejar cambios en rutas y formato de errores | Pasos 5, 7 |
| 10 | Swagger UI (`/swagger`) en Development | ✅ Hecho |

---

## Estrategia para conservar las 306 pruebas

### Principio general
Ejecutar `dotnet test ImpactX.slnx` después de **cada cambio individual**, no después de lotes grandes.

### Clasificación de pruebas

**14 unit tests (Moq):**
- Validan lógica de servicios con interfaces mockeadas
- Se rompen solo si cambian firmas de interfaces o constructores
- Bajo riesgo en refactors

**16 integration tests (WebApplicationFactory):**
- Validan rutas HTTP, status codes, estructura del response body
- Alto riesgo en cambios de rutas, formato de errores, nombres de propiedades JSON

### Mitigaciones por tipo de cambio

| Cambio planeado | Riesgo | Mitigación |
|---|---|---|
| Migrar a `/api/v1/` | Alto | Mantener rutas actuales como default sin versión hasta migrar tests. O actualizar las 16 rutas en tests simultáneamente |
| Migrar a ProblemDetails | Alto | Mantener `ErrorResponse` como DTO de respuesta hasta que los tests se actualicen. O migrar tests primero para validar `ProblemDetails` |
| Cambiar fechas a `DateTimeOffset` | Bajo | Ejecutar tests, ajustar asserts si fallan por formato |
| Renombrar namespaces | Bajo | Tests ya usan `ImpactX.*`. Cambios cosméticos no afectan lógica |
| Separar en múltiples proyectos | Alto | `WebApplicationFactory<Program>` necesita un proyecto único como host. Mantener `ImpactX.Api` como host o crear `ImpactX.Host` |
| Eliminar `CosmosTest.csproj` | Ninguno | No está en solución, no referenciado por tests |
| Eliminar `Prueba1.Tests/` | Ninguno | Contiene solo artefactos locales (bin/obj). No está versionado en git. Puede borrarse localmente después de comprobar |

### Recomendación
Cada paso de implementación debe ir acompañado de:
1. `dotnet build ImpactX.slnx` para verificar compilación
2. `dotnet test ImpactX.slnx` para verificar que las 306 pruebas sigan pasando
3. Si fallan pruebas, corregir asserts antes de continuar al siguiente paso

---

## Archivos versionados generados o innecesarios

| Archivo | Estado | Acción |
|---|---|---|
| `test_output.txt` | ✅ Eliminado del sistema de archivos. Agregado al `.gitignore` | El archivo ya fue eliminado del sistema de archivos. Su eliminación se registrará en Git al ejecutar git add -A |
| `Prueba1.Tests/` | No versionado. Solo bin/obj local | Puede borrarse localmente |

## Modificaciones realizadas en esta auditoría

### Ronda 1 — Platform Foundation

| Archivo | Acción |
|---|---|
| `docs/BACKEND_AUDIT.md` | ✅ Creado |
| `AGENTS.md` | ✅ Modificado (regla de ramas actualizada, health checks, Swagger) |
| `.gitignore` | ✅ Modificado (test_output*.txt agregado) |
| `test_output.txt` | ✅ Eliminado del sistema de archivos |
| `ImpactXv1/Program.cs` | ✅ Modificado (health checks live/ready, SwaggerUI, ResponseWriter) |
| `ImpactXv1/ImpactX.Api.csproj` | ✅ Modificado (Swashbuckle.AspNetCore agregado) |
| `ImpactX.Tests/Integration/PlatformFoundationTests.cs` | ✅ Creado (9 pruebas: health + Swagger + OpenAPI) |

La implementación elevó el total de pruebas de **306 a 315** (9 nuevas).

### Ronda 2 — Normalización + OpenAPI global

| Archivo | Acción |
|---|---|
| `.gitattributes` | ✅ Creado (`* text=auto`, `*.cs text diff=csharp`) |
| `ImpactXv1/Program.cs` | ✅ Modificado (`app.MapOpenApi()` fuera del bloque Development) |
| `ImpactX.Tests/Integration/PlatformFoundationTests.cs` | ✅ Modificado (prueba `OpenApiV1Json_Returns200_InProduction` agregada) |
| `AGENTS.md` | ✅ Modificado (317 tests, OpenAPI global, Scalar+Swagger solo Dev) |
| `docs/BACKEND_AUDIT.md` | ✅ Modificado (documentación de Ronda 2) |

- Se configuró `core.whitespace cr-at-eol` en el repositorio.
- Se corrigieron los finales de línea de `Program.cs` y `PlatformFoundationTests.cs` a LF (consistentes con el resto del repositorio). `.gitattributes` declara `*.cs text` para que Git normalice a LF en el índice.
- `app.MapOpenApi()` ahora se ejecuta en todos los ambientes; solo Scalar y Swagger UI permanecen en Development.
- Total de pruebas: **317** (306 base + 11 nuevas: 9 de Ronda 1 + 2 de Ronda 2).

## Historial de versiones del documento

| Versión | Fecha | Cambios |
|---|---|---|
| 1.0 | — | Creación inicial del documento con auditoría de línea base |
| 1.1 | — | Ronda 1: health checks, Swagger UI, 315 pruebas |
| 1.2 | — | Ronda 2: `.gitattributes`, OpenAPI global, 317 pruebas |
