# AGENTS.md — ImpactX Backend APIs

## Project identity
- .NET 10.0 ASP.NET Core Web API, target `net10.0`
- Namespace: `ImpactX.*` (ImpactX.Api, ImpactX.Services, ImpactX.Infrastructure.*, ImpactX.Core.*, ImpactX.Models.*, ImpactX.Extensions, ImpactX.Middleware)
- Solution: `ImpactX.slnx`
- Two projects: Web API (`ImpactXv1/ImpactX.Api.csproj`) + tests (`ImpactX.Tests/ImpactX.Tests.csproj`)
- `ImpactX.Api.csproj` exposes `InternalsVisibleTo` to `ImpactX.Tests`

## Commands
```
dotnet restore ImpactX.slnx
dotnet build ImpactX.slnx
dotnet test ImpactX.slnx [--filter "..."] [--configuration Release]
dotnet test ImpactX.slnx --configuration Release --verbosity normal --collect "XPlat Code Coverage"
dotnet run --project ImpactXv1/ImpactX.Api.csproj
dotnet add package <PackageName>
```

## Architecture
- **Controllers → Services → Repositories** (interface + 2 impls per entity)
- Two repository families: **EF Core InMemory** and **Cosmos DB**
- DI wiring in `Extensions/ServiceCollectionExtensions.cs::RegisterApplicationServices()`
- DB mode driven by `UseCosmosDb` + `UseInMemoryDatabase` (both bool)
  - `UseCosmosDb=true` → `CosmosDbContext` (singleton) + Cosmos repos
  - `UseInMemoryDatabase=true` → EF Core InMemory + EF repos
  - Both false → EF Core InMemory (falls through to same else branch)
  - **No SQL Server path** — no `ConnectionStrings:DefaultConnection` exists
- Startup seeding via `Extensions/WebApplicationExtensions.cs::SeedDatabaseAsync()` — calls `PlanSeeder` for both modes
- CORS policy `AllowLocalhost` (all origins, all headers/methods, credentials)

## Implemented features (15 controllers, 15+ services)
- **Auth** (register, login, logout, recover/reset password, sessions, account export/delete, **refresh token**)
- **Users** (profile CRUD, driver profile, medical profile, preferences, permissions, settings, **FCM token** PUT/DELETE)
- **Plans + Subscriptions + Payments**
- **Contacts**: legacy `/api/contacts` (nombre/teléfono, no operativo) + V1 `/api/v1/contacts` como relación interna aceptada con preinvitación, código hash, bloqueo y revocación lógica
- **Monitors** (invite, accept, reject, revoke, restore, **Premium allows 6**)
- **Routes** (Rutas)
- **Trips** (Viajes + telemetry: GET paginado, PATCH legacy por evento, **POST ingesta por lotes con idempotencia por EventId**)
- **Wearables**
- **Alerts** (Alertas + **monitor notification integration**)
- **Incidents** (Incidentes)
- **Notifications** (Notificaciones + **alert dispatch**, **idempotency**, **push history**)
- **Analytics**
- **Settings**

## Domain layer
- `Core/Domain/` — 22 entity classes (Usuario, Viaje, ViajeTelemetry, Alerta, Incidente, Notificacion, Wearable, ContactoEmergencia, Monitor, Ruta, Plan, Suscripcion, Pago, RefreshToken, PasswordResetToken, FichaMedica, PerfilConduccion, PreferenciasUsuario, PermisosApp, SettingsUsuario, AppInvite, ChatThread)
- `Core/Domain/Enums/` — PlanType enum
- `Core/Exceptions/` — BadRequestException, ConflictException, ForbiddenException, NotFoundException
- `Core/Interfaces/Repositories/` — 14 repository interfaces
- `Core/Interfaces/Services/` — IEmailService, IEncryptionService, ITokenService
- `Models/DTOs/` — 23 DTO classes

## Infrastructure
- `Infrastructure/Data/` — `ApplicationDbContext` (EF Core, 14 DbSets with full Fluent API config), `CosmosDbContext` (singleton, wraps `CosmosClient` + 18 container references), `CosmosContainerCatalog` (catálogo central), `CosmosDatabaseOptions` (Options pattern), `CosmosSchemaValidationException`, `CosmosPartitionKeys`, `PlanSeeder`, `DatabaseInitializationOptions`, `DatabaseInitializationState`, `CosmosInitializationService`
- `Infrastructure/Data/Repositories/EF/` — 14 EF repository implementations
- `Infrastructure/Data/Repositories/Cosmos/` — 14 Cosmos repository implementations + `IncidenteQueryBuilder` (SQL parametrizado testeable)
- `Infrastructure/Security/` — `EncryptionService`, `JwtTokenService`, `StubEmailService`

## Cosmos DB specifics
- `CosmosDbContext` is a **singleton** — wraps `CosmosClient` + 18 container references from `CosmosContainerCatalog`
- **`CosmosContainerCatalog`** (única fuente de verdad): 18 definiciones (Name, PartitionKeyPath, DefaultTimeToLive, Entity, CompositeIndexes) validadas al cargar (sin duplicados, PK `/x` de segmento único, TTL −1 o > 0). Sin throughput dedicado: todos comparten las 400 RU/s de la base `ImpactX-Data`
- **`CosmosDatabaseOptions`** (sección `AzureCosmosDb`, Options pattern + `ValidateOnStart`): Endpoint (URI absoluto, nunca en logs), Key (placeholder `YOUR_AZURE_COSMOS_KEY` validado por readiness, no por opciones), DatabaseName (default `ImpactX-Data`), SharedThroughput (default 400, entero positivo ≤ 1000), RequestTimeoutSeconds (30), MaxRetryAttemptsOnRateLimitedRequests (3), MaxRetryWaitTimeSeconds (30) — reintentos 429 limitados, nunca infinitos
- `EnsureContainersAsync()` crea la base con throughput manual compartido (fallback sin throughput solo ante 400 BadRequest), crea solo contenedores faltantes **sin throughput**, re-lee y valida; idempotente; nunca borra/recrea; **mismatch de partition key → `CosmosSchemaValidationException`** (inicialización Failed, readiness Unhealthy, migración controlada requerida)
- 18 containers: Usuarios (`/id`), RefreshTokens (`/usuarioId`, TTL 604800), PasswordResetTokens (`/usuarioId`, TTL 3600), Dispositivos (`/usuarioId`), Planes (`/id`), Suscripciones (`/usuarioId`), Pagos (`/usuarioId`), Monitores (`/usuarioId`), ContactosEmergencia (`/usuarioId`), Rutas (`/usuarioId`), Viajes (`/usuarioId`, TTL 7776000), TelemetriaViaje (`/viajeId`, TTL 7776000), Alertas (`/usuarioId`, TTL 31536000), Notificaciones (`/usuarioId`, TTL 2592000), Wearables (`/usuarioId`), AppInvites (`/usuarioId`, TTL 2592000), ChatThreads (`/usuarioId`), Incidentes (`/usuarioId`)
- Política de índices por defecto de Cosmos; sin composite indexes (todas las `ORDER BY` son de partición única); el catálogo permite representarlos sin duplicar
- Repos: queries ligadas a usuario/viaje con `QueryRequestOptions.PartitionKey`; point-reads solo con id+PK correctas (`GetByIdAsync(id)` de contenedores `/usuarioId` usa `SELECT TOP 1 WHERE c.id = @id` — cross-partition justificada por contrato); `ReplaceItemAsync` en updates; `MaxItemCount` (1/50/100) + detención temprana; todo input por `QueryDefinition` (nunca concatenado)
- `PlanSeeder`: IDs determinísticos (`FreePlanId`/`BasicPlanId`/`PremiumPlanId` = GUIDs fijos `00000000-...-0001..3`), point-read + COUNT por nombre; sin `SELECT * FROM c`
- Cosmos key via `AzureCosmosDb:Key` or env var `COSMOS_KEY`
- Property naming: `CosmosPropertyNamingPolicy.CamelCase`
- All 3 appsettings files (`appsettings.json`, `Development.json`, `Production.json`) set `UseCosmosDb: true` with real endpoint but `Key: "YOUR_AZURE_COSMOS_KEY"` — must be replaced or overridden
- `appsettings.Development.json` loads additional `appsettings.Local.json` (gitignored)

## JWT auth
- HMAC-SHA256 symmetric signing via `JwtTokenService` (`ITokenService`)
- Config section `Jwt:{Secret,Issuer,Audience,ExpirationMinutes}`
- Single source: `JwtSecurityConfiguration.GetRequiredSecret(IConfiguration)` reads only `Jwt:Secret`
- **No fallback** — `InvalidOperationException` if missing, empty, or <32 bytes UTF-8
- Default issuer: `"ImpactXApi"`, audience: `"ImpactXClients"`
- `ClockSkew = TimeSpan.Zero`
- User ID from `ClaimTypes.NameIdentifier`

## Middleware pipeline (order matters in Program.cs)
1. `CorrelationIdMiddleware` — lee/genera `X-Correlation-Id` (≤100 chars, sin CR/LF), lo devuelve en el header de respuesta, lo guarda en `HttpContext.Items` y `TraceIdentifier`, y crea un logging scope con `CorrelationId` para toda la solicitud
2. `RequestLoggingMiddleware` — log estructurado `HTTP {Method} {Path} ... status {StatusCode} ... {ElapsedMs}ms ... correlationId {CorrelationId}` (sin body, sin query string, sin Authorization/cookies/tokens; CR/LF sanitizados)
3. `ProblemDetailsMiddleware` — excepciones → RFC 7807; siempre incluye `traceId` + `correlationId`
4. `SecurityHeadersMiddleware`
5. `app.UseCors("ApiCors")`
6. `UseAuthentication()` / `UseAuthorization()`
7. `MapControllers()`
8. `MapHealthChecks` — tres endpoints: `/health`, `/health/live`, `/health/ready`

## OpenAPI (all environments) + Scalar + Swagger UI (Development only)
```csharp
app.MapOpenApi();

if (app.Environment.IsDevelopment())
{
    app.MapScalarApiReference();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "ImpactX API v1");
    });
}
```
- OpenAPI spec at `/openapi/v1.json` — **available in all environments**
- Scalar UI at Scalar default route (package `Scalar.AspNetCore`) — Development only
- Swagger UI at `/swagger` and `/swagger/index.html` (package `Swashbuckle.AspNetCore`) — Development only

## Health checks
- `builder.Services.AddHealthChecks()` — checks: `live` (trivial, tag `live`), `config` (`ConfigurationReadinessCheck`, tag `ready`), `database` (`DatabaseReadinessCheck`, tag `ready`, solo cuando `UseCosmosDb=true`)
- **`GET /health/live`** — solo proceso vivo + pipeline HTTP. No contacta Cosmos, no crea contenedores, no ejecuta seeding. 200 si vivo. Nunca depende de servicios externos.
- **`GET /health/ready`** — lista para tráfico: `config` (JWT secret válido vía `JwtSecurityConfiguration.GetRequiredSecret`; con Cosmos: endpoint + databaseName + key presentes y key ≠ placeholder `YOUR_AZURE_COSMOS_KEY`) + `database` (estado de inicialización si `DatabaseInitialization:Enabled` y `Readiness:InitializationRequired`; acceso read-only `CosmosDbContext.IsAccessibleAsync` = `Database.ReadAsync`, timeout `Readiness:CosmosAccessTimeoutSeconds`). 200 lista; **503** si una dependencia crítica no está lista. Comprobación barata y de solo lectura.
- **`GET /health`** — agrega todos los checks. JSON: `status`, `service`, `environment`, `timestamp` (UTC ISO-8601 "o"), `correlationId`, `checks[]` con `name`, `status`, `duration` (ms) y `description` segura cuando aplica. Sin exceptions internas, sin claves, sin connection strings, sin stack traces.
- Healthy→200, Degraded→200, Unhealthy→503 (mapeo por defecto de `MapHealthChecks`). `AllowCachingResponses=false` en los tres endpoints.
- Response writer en `Program.cs::WriteHealthCheckResponse` — safe, no internals expuestos
- No authentication, no authorization on any health endpoint

## Tests
- **Unit tests** (files in `ImpactX.Tests/Unit/`): pure Moq per service
- **Integration tests** (files in `ImpactX.Tests/Integration/`): `CustomWebApplicationFactory<Program>` forces `UseCosmosDb=false` + `UseInMemoryDatabase=true`, injects test JWT secret
- API project exposes `partial class Program` for `WebApplicationFactory<Program>`
- **Category=Security**: pruebas de autenticación, refresh token, autorización, FCM, JWT config, recover/reset password, revocación de sesiones, 401. Se ejecutan filtradas en CI como regression de seguridad.

## CI/CD — 7 GitHub Actions workflows (se conservan)
1. **dotnet-ci.yml** — Pipeline principal de CI (fortalecido con security regression):
   - `push` a `main`, `leo-desarrollo`, `feat/**`
   - `pull_request` hacia `main`
   - `workflow_dispatch`
   - **Job `build-and-test`** (15 min): checkout → setup-dotnet 10.0.x → **Run hardcoded secrets policy tests** → **Check hardcoded secrets policy** → restore con NuGet audit → build Release → test (TRX + XPlat Code Coverage) → **Run security regression tests** (filtro `Category=Security`, validación TRX con Python, fallo si 0 pruebas o fallos) → publica TRX, cobertura, **hardcoded-secrets-policy-report** y **security-regression-results** (14 días)
   - **Job `smoke-test`** (5 min, depende de build-and-test): genera Jwt__Secret efímero con Python secrets → checkout → setup-dotnet → restore API → publish Release → ejecución directa de `ImpactX.Api.dll` con `dotnet`, usando `UseCosmosDb=false`, `UseInMemoryDatabase=true`, `ASPNETCORE_ENVIRONMENT=CI`, `ASPNETCORE_URLS=http://127.0.0.1:5055` → valida 4 endpoints (`/health`, `/health/live`, `/health/ready`, `/openapi/v1.json`) con Python 3 (status, service, environment, timestamp, schema) → verifica Swagger 404 → publica log de arranque
   - **Sin Docker**: el smoke test ejecuta `dotnet publish` + `ImpactX.Api.dll` directamente
2. **main_impactx-api-backend.yml** — push to `main` only + `workflow_dispatch`: build (15 min) → publish → deploy (20 min, oidc) to Azure Web App `impactx-api-backend`. Permissions: `build: contents: read`, `deploy: id-token: write + contents: read`. **Concurrency** `deploy-impactx-api-main` (cancel-in-progress=false). **Post-deploy verification**: wait 20s → curl `--fail --silent --show-error --connect-timeout 5 --max-time 20` a `/health/live`, `/health/ready`, `/openapi/v1.json` (host vía env `APP_BASE_URL` = `vars.APP_BASE_URL || 'https://impactx-api-backend.azurewebsites.net'`), 12 intentos × 15s, muestra endpoint/intento/código, falla con `exit 1` si no llegan a 200, body de error solo en archivo temporal (nunca impreso), detecta `Site Disabled` (WP stop requests F1) y `quota` con warnings de diagnóstico (`az webapp show --query state`, `az webapp list-usages`). Sin restart ni redeploy automáticos. Validado por `test_deploy_workflow_contract.py`.
3. **api-security-audit.yml** — OWASP API security scan. Triggers: push + pull_request (main, leo-desarrollo) + `workflow_dispatch`. Timeout: 15 min. Permissions: `contents: read`.
4. **secret-scanning.yml** — Gitleaks credential scan + hardcoded secrets policy tests + policy scanner. Triggers: push + pull_request (main, leo-desarrollo) + `workflow_dispatch`. Timeout: 10 min. Permissions: `contents: read`.
5. **codeql-analysis.yml** — CodeQL SAST (C#, security-extended). Triggers: push + pull_request (main, leo-desarrollo) + `workflow_dispatch`. Timeout: 15 min. Permissions: `contents: read, security-events: write`.
6. **code-quality-roslyn.yml** — Roslyn format estricto solo sobre archivos C# modificados. Triggers: push + pull_request (main, leo-desarrollo) + `workflow_dispatch`. Timeout: 15 min. Permissions: `contents: read`. No oculta fallos (sin `|| echo`, `|| true` ni `continue-on-error`). Si no hay C# modificados, termina correctamente sin ejecutar `dotnet format`. La deuda histórica de 14.673 errores ENDOFLINE/FINALNEWLINE en archivos no modificados no afecta este check.
7. **infra-validation.yml** — Bicep Infrastructure Validation. Triggers: `pull_request` a `main` solo cuando cambian `infra/**` o el propio workflow, y `workflow_dispatch`. Permissions: `contents: read` (sin OIDC, sin Azure login). Concurrency con cancelación automática. Bicep 0.45.15 descargado temporalmente con SHA-256 fijado y verificado durante auditoría. Sin Azure login. Sin despliegue. Validaciones: compilación estricta de `infra/main.bicep` + `dev/test/prod.bicepparam` (sin errores ni warnings), presencia exacta del placeholder `DONT_DEPLOY_UNTIL_RUNTIME_IS_VERIFIED`, ausencia de ARM JSON (solo `bicepconfig.json`), prohibición de secretos en archivos Bicep, revisión de nombres de outputs, y `git diff --check` con rango completo.

## Dev server
- HTTP: `http://localhost:5000` / `http://localhost:5161`
- HTTPS: `https://localhost:7278`
- Launch profiles: `http` and `https` (both set `ASPNETCORE_ENVIRONMENT=Development`)
- API routes: `api/auth/*`, `api/users/*`, `api/plans/*`, `api/subscription/*`, `api/wearable/*`, `api/permissions/*`, `api/contacts/*`, `api/monitors/*`, `api/routes/*`, `api/trips/*`, `api/alertas/*`, `api/incidentes/*`, `api/notificaciones/*`, `api/analytics/*`, `api/settings/*`
- Health endpoints: `GET /health`, `GET /health/live`, `GET /health/ready`
- Swagger UI at `/swagger` (Development only)

## Configuration & secrets
- `UserSecretsId`: `45e9cf43-58e6-44b4-9a33-3a4988095b2d` — use `dotnet user-secrets set ...`
- Cosmos key: must be set via `AzureCosmosDb:Key` in config, env var `COSMOS_KEY`, or user secrets
- JWT secret: must be set externally via `Jwt__Secret` env var, `dotnet user-secrets`, or Azure Key Vault. Minimum 32 bytes UTF-8.
- Dev ignores `appsettings.Local.json` (gitignored)
- Production JWT secret must be set via `Jwt__Secret` env var or Azure Key Vault

## Risks
- **JWT secret externalized** — no longer hardcoded. Required via `Jwt__Secret` env var, user secrets, or Azure Key Vault. `JwtSecurityConfiguration.GetRequiredSecret()` enforces fail-fast with 32-byte minimum.
- **Cosmos key placeholder** `"YOUR_AZURE_COSMOS_KEY"` in 3 appsettings files — real endpoint exposed
- **CORS policy** allows all origins with credentials (`SetIsOriginAllowed(_ => true)`)
- No rate limiting, no CSRF protection
- `StubEmailService` — no real email sending
- `Prueba1.Tests/` directory leftover (only bin/obj, no source)
- `CosmosTest.csproj` standalone project at root (not in solution)

## Azure IaC — Bicep foundation (`infra/`)
- **Scope**: `targetScope = 'subscription'` — each deployment creates its own Resource Group
- **Modules**: `modules/monitoring.bicep` (Log Analytics + Application Insights workspace-based), `modules/app-service.bicep` (Linux App Service Plan + Web App + System-Assigned Managed Identity, preconfigured with App Service managed auto-instrumentation via `APPLICATIONINSIGHTS_CONNECTION_STRING`, `ApplicationInsightsAgent_EXTENSION_VERSION=~3`, `XDT_MicrosoftApplicationInsights_Mode=recommended`)
- **Environments**: `infra/environments/{dev,test,prod}.bicepparam` — parameter files using `../main.bicep`
- **No secrets**: No JWT, Cosmos DB keys, connection strings, or credentials are defined in `infra/`. The Application Insights connection string is passed internally between modules but is never exposed as a `main.bicep` output.
- **No deployment**: Resources have not been created. No `what-if` has been run. No GitHub Actions workflows were modified.
- **Validation** (requires Bicep CLI, not `az`):
  ```
  bicep build infra/main.bicep
  bicep build-params infra/environments/{dev,test,prod}.bicepparam
  ```
- **Before first deploy**: Install Azure CLI, authenticate, verify .NET runtime (`az webapp list-runtimes --os linux | grep DOTNET`), replace `DONT_DEPLOY_UNTIL_RUNTIME_IS_VERIFIED`, review SKU/region, run what-if, get approval.
- **Allowed modifications**: Only files under `infra/`, `AGENTS.md`, `docs/BACKEND_AUDIT.md`. No C#, no workflows, no appsettings.

## R0.2 — PR 1A API Contracts V1 (implementado)
- **Branch**: `feat/backend-api-contracts-v1`, base `27e17857da216e0d80482a12b7759551785f4020`
- **Problem Details (RFC 7807)**: Middleware propio `ProblemDetailsMiddleware` reemplaza `ExceptionHandlingMiddleware`. Mapeo de excepciones: BadRequestException→400, ArgumentException→400 (segura), UnauthorizedAccessException→401, ForbiddenException→403, NotFoundException→404, KeyNotFoundException→404, ConflictException→409, excepción inesperada→500. **No** mapea InvalidOperationException a 409.
- **CORS**: Configurable por `Cors:AllowedOrigins` (formato jerárquico `Cors:AllowedOrigins:0`). Producción con lista vacía cierra CORS. No usa `AllowAnyOrigin` ni `SetIsOriginAllowed`.
- **Rate Limiting**: 13 políticas nombradas (auth-register, auth-login, auth-refresh, auth-recover, auth-reset, monitor-invite-details, monitor-invitation-action, monitor-invite-create, fcm-token, telemetry-ingestion, incident-create, alert-detect, alert-sos). Límites por configuración. `RejectionStatusCode=429`.
- **Rutas V1**: Todos los controladores tienen rutas V1 (15 controllers). Auth usa `[Route("api/v1/auth")]` en minúsculas (no `[controller]`). Users y Subscription usan rutas absolutas. No hay `api/v1/vehicles` (domain no existe).
- **Deprecation**: Middleware `LegacyDeprecationMiddleware` agrega headers `Deprecation: true`, `Warning`, `Link` a rutas `/api/` (no V1), excepto health/openapi/swagger.
- **Correlation ID**: Middleware `CorrelationIdMiddleware` lee/genera `X-Correlation-Id`, sanitiza CR/LF, limita longitud.
- **OpenAPI V1**: Filtro `ShouldInclude` incluye solo paths que empiezan con `api/v1/`. Document transformer agrega `securitySchemes.Bearer` y `ProblemDetails` schema (type, title, status, detail, instance). Response metadata transformer agrega respuestas 4xx/5xx declaradas vía `[ProducesResponseType]` en endpoints, más reglas automáticas para 400/401/403/404/409/429/500. Operation transformer agrega `security` requirement Bearer en operaciones protegidas.
- **409 Conflict en OpenAPI**: 24 endpoints V1 ahora declaran `[ProducesResponseType(StatusCodes.Status409Conflict)]` — cada uno con `ConflictException` real en el servicio (auth/register, subscriptions, monitors, contacts, trips, alerts, settings, **PUT /api/v1/devices/fcm-token**). El response metadata transformer (`OpenApiV1ResponseMetadataTransformer.cs`) los mapea a respuestas `application/problem+json` → `$ref ProblemDetails` en OpenAPI spec.
- **Legacy**: Contratos legacy conservados. Registro duplicado V1 retorna 409 ProblemDetails; legacy retorna 409 ConflictObjectResult.
- **Contrato de pruebas**: 75 pruebas de contrato (ApiContractV1: 41 + Cors: 4 + Security: 5 + ProblemDetailsContract: 18 + RateLimitingContract: 5 + Error500: 2). Incluye 3 nuevas OpenAPI 409 tests. Fábricas aisladas para rate limiting y CORS.
- **Total real**: 490 pruebas, 115 Category=Security, 75 pruebas de contrato, 19 Python, scanner 0 violaciones, NuGet 0 vulnerables, actionlint limpio, git diff --check limpio. Build y tests passes en Debug y Release.
- **Históricos**: 415/104 → 474/114/58 → 487/115/70 → **490/115/75**.
- **Pendiente**: PR 1B y PR 1C no implementados. Vehículos no existe. Telemetría ligada a TripId. Límites de rate limiting pendientes de calibrar. Resultados GitHub pendientes.

## R0.3 — Identity and Device Hardening / PR 1B (implementado)
- **Branch**: `feat/backend-identity-device-hardening`, base `main` en `85c25ae487f4a4f809582791d92b9c38207a02b0`
- **Reset token con hash**: `PasswordResetToken.TokenHash` almacena solo SHA-256 (base64) del token. `PasswordResetTokenHasher.Hash()` en `Core/Security/`. El token crudo (32 bytes RNG base64url) viaja solo a `IEmailService`. Búsqueda por hash (`GetByTokenHashAsync`), expiración UTC 1h, uso único (`UsedAt`), `InvalidateAllByUsuarioIdAsync` invalida tokens previos al crear uno nuevo. Respuesta genérica indistinta. Sin token/hash/correo/contraseña en logs.
- **AuthService catch**: asignación del plan Free captura solo `CosmosException` (Conflict/429) y `DbUpdateException` con `ILogger` (sin `Exception.Message`, sin `Console.WriteLine`); propaga `OperationCanceledException` y errores inesperados a `ProblemDetailsMiddleware`.
- **FCM multidispositivo**: nueva entidad `Dispositivo` (Id, UsuarioId, DeviceId, Platform Android/WearOS/Web, TokenFcm, Nombre, Activo, CreadoEn, ActualizadoEn, UltimoUsoEn). Repos EF + Cosmos (`IDispositivoRepository`), contenedor Cosmos `Dispositivos` (`/usuarioId`, sin TTL). DeviceId único por usuario. **Token FCM globalmente único**: antes de crear o actualizar cualquier dispositivo, `GetByTokenFcmAsync(string)` busca el token globalmente (EF: query global; Cosmos: cross-partition solo para esta resolución); si es de otro dispositivo del mismo usuario se desactiva la asociación anterior y se elimina su TokenFcm; si es de otro usuario → `ConflictException` (409) con mensaje genérico sin revelar el propietario. El token nunca se registra en logs. Plataforma normalizada por switch case-insensitive con `Trim()` → exactamente `Android`/`WearOS`/`Web` (nunca "Wearos"); plataforma null/vacía/espacios o inválida → 400.
- **Endpoints V1** (`DevicesController`, todos `[Authorize]`): `GET /api/v1/devices`, `PUT /api/v1/devices/fcm-token` (`UpsertDeviceRequest`: deviceId, platform, token, name opcional), `DELETE /api/v1/devices/{id:guid}` (404 para ID inexistente o ajeno — sin revelar existencia), `DELETE /api/v1/devices` + `DELETE /api/v1/devices/fcm-token` (revocación total, compat). Rate limiting `fcm-token` en PUT/DELETE.
- **Autorización horizontal**: usuarioId siempre del JWT (`ClaimTypes.NameIdentifier`); el body no tiene campo UsuarioId. Listar/modificar/eliminar solo dispositivos propios. `GetByIdAsync(Guid usuarioId, Guid id)` filtra por usuario en EF (doble condición) y en Cosmos (lectura puntual con `PartitionKey(usuarioId)`, 404→null).
- **Nunca se devuelve TokenFcm** (`DeviceDto` sin token) ni se registra en logs.
- **NotificationService**: envía a todos los dispositivos activos; fallo de un token no bloquea los demás (fallback a `Usuario.FcmToken` legacy si no hay dispositivos). Al menos un éxito → agregado "Enviado"; todos fallidos → "Fallido". Idempotencia de notificaciones intacta. Sin Firebase real.
- **Legacy**: `PUT/DELETE /api/users/me/fcm-token` conservados (escriben `Usuario.FcmToken`). Rutas V1 `/api/v1/devices/*` se movieron de UsersController a DevicesController (sin conflictos de ruta). **Migración del token legacy**: al registrar dispositivo V1, al eliminar el último dispositivo activo y al revocar todos se limpia `Usuario.FcmToken` (evita reenvíos con el token eliminado).
- **Cuenta eliminada**: `DeleteAccountAsync` revoca también todos los dispositivos (`DeleteAllByUsuarioIdAsync`) y limpia `Usuario.FcmToken`.
- **Cosmos hardening**: `EnsureContainersAsync` captura solo `BadRequest` (cuenta incompatible con throughput manual) en creación de BD reintentando sin throughput; `CreateContainerIfNotExistsAsync` ya maneja el 409 de contenedores (sin catch); 401/403/429/5xx propagan. `PlanSeeder` captura solo Conflict de Cosmos, propaga OCE, sin `Console.WriteLine` ni `ex.Message`.
- **Total real**: 558 pruebas, 144 Category=Security, scanner 0 violaciones, NuGet 0 vulnerables, actionlint limpio, Roslyn limpio en C# modificados, git diff --check limpio. Build y tests passes en Debug y Release.
- **Históricos**: 415/104 → 474/114/58 → 487/115/70 → 490/115/75 → 532/137 → 547/142 → **558/144**.
- **Pendiente**: PR 1C (deuda: ver BACKEND_AUDIT). StubEmailService sin reemplazo real. Cosmos real no contactado. Resultados GitHub pendientes. La comprobación global query-before-write del token FCM no garantiza atomicidad bajo concurrencia extrema en Cosmos (deuda documentada para PR 1C).

## R0.4 — Readiness, Observability and Production Hardening / PR 1C (implementado)
- **Branch**: `feat/backend-readiness-observability`, base `main` en `2c3c87f` (PR #25)
- **Semántica de health checks**: `/health/live` = solo proceso/pipeline HTTP (check trivial, nunca toca Cosmos, 200 si vivo). `/health/ready` = `config` (JWT secret válido vía `JwtSecurityConfiguration.GetRequiredSecret`; con Cosmos: endpoint + databaseName + key presentes y key ≠ placeholder `YOUR_AZURE_COSMOS_KEY`) + `database` (solo modo Cosmos; estado de inicialización si es requerida + `CosmosDbContext.IsAccessibleAsync` = `Database.ReadAsync` metadata read-only con timeout `Readiness:CosmosAccessTimeoutSeconds`=5s). 200 lista / 503 dependencia crítica no lista. `/health` agrega todos los checks; JSON: `status`, `service`, `environment`, `timestamp` (UTC "o"), `correlationId`, `checks[]` (name, status, duration ms, description segura). `AllowCachingResponses=false` en los tres endpoints. Sin excepciones internas, claves, connection strings ni stack traces en ninguna respuesta.
- **Inicialización Cosmos asíncrona**: `CosmosInitializationService` (BackgroundService, solo cuando `UseCosmosDb=true`) ejecuta `EnsureContainersAsync` + `PlanSeeder.SeedPlansAsync` fuera del arranque; estado singleton `DatabaseInitializationState` (NotStarted/Running/Succeeded/Failed, Attempts, FailureDescription seguro). `WebApplicationExtensions.SeedDatabaseAsync` ya no toca Cosmos (solo EF InMemory). **Nada bloquea `app.Run()`** → sin ciclos de caída/reinicio por fallos transitorios de Cosmos.
- **`DatabaseInitializationOptions`** (sección `DatabaseInitialization`): `Enabled`, `MaxAttempts` (3), `RetryDelaySeconds` (5), `TimeoutSeconds` (60 por intento, vía linked CTS). Default en código: `Enabled = !IsProduction` si la sección no la define. Producción: `Enabled=false` (inicialización solo si se habilita explícitamente). `ReadinessOptions` (sección `Readiness`): `InitializationRequired` (true) y `CosmosAccessTimeoutSeconds` (5).
- **Reintentos**: transitorios = 408, 429, 500, 502, 503, 504 y timeout de intento (OCE). **No se reintentan**: 401, 403, 400, 404, 409 y errores inesperados (catch genérico solo marca `Failed` con descripción segura + log de `ex.GetType().Name`; nunca `ex.Message`). Nunca reintento infinito. Sin catch vacío. `OperationCanceledException` por shutdown → catch superior que registra y termina con gracia.
- **Readiness durante inicialización**: con `Readiness:InitializationRequired=true` + init habilitada, `/health/ready` = Unhealthy (503) mientras el estado no sea Succeeded; Failed → Unhealthy con descripción segura.
- **Correlation ID**: `CorrelationIdMiddleware` reutilizado (sin duplicados) ahora crea logging scope (`BeginScope` con `CorrelationId`) para toda la solicitud. Acepta header válido (≤100 chars, sin CR/LF), sanitiza CR/LF, reemplaza inválidos con GUID N, devuelve el header de respuesta, `HttpContext.Items["CorrelationId"]` + `TraceIdentifier`.
- **RequestLoggingMiddleware**: log estructurado `HTTP {Method} {Path} completed with status {StatusCode} in {ElapsedMs}ms, correlationId {CorrelationId}`; method/path sanitizados (CR/LF→espacio). Sin body, sin query string, sin Authorization/cookies/tokens. Orden de pipeline: `CorrelationId → RequestLogging → ProblemDetails → SecurityHeaders → LegacyDeprecation` (RequestLogging fuera de ProblemDetails para registrar el status final incluso con errores). `appsettings.Development.json`: `Microsoft.AspNetCore.Hosting=Warning` (el framework no registra query strings).
- **Firebase**: inicialización movida tras `builder.Build()` con `app.Logger`; sin rutas de archivo ni `ex.Message` en logs (solo `ex.GetType().Name`); sin `Console.WriteLine`.
- **Post-deploy verification** (`main_impactx-api-backend.yml`): concurrency `deploy-impactx-api-main` (cancel-in-progress=false); espera 20s; curl `--fail --silent --show-error --connect-timeout 5 --max-time 20` a `/health/live`, `/health/ready`, `/openapi/v1.json` (host `APP_BASE_URL` = `vars.APP_BASE_URL || 'https://impactx-api-backend-h0eyf9c4fxd8dsbc.westus-01.azurewebsites.net'`); 12 intentos × 15s; falla con `exit 1`; body de error solo en archivo temporal (nunca impreso); detecta `Site Disabled` (WP stop requests F1) y `quota` con warnings de diagnóstico (`az webapp show --query state`, `az webapp list-usages`). Sin restart ni redeploy automáticos. Contrato estático: `scripts/security/tests/test_deploy_workflow_contract.py` (11 tests).
- **Total real**: 607 pruebas, 146 Category=Security, 76 contratos V1 conservados, 30 Python, scanner 0 violaciones (260 archivos), NuGet 0 vulnerables, actionlint limpio (7 workflows), Roslyn limpio en 20 C# modificados/nuevos, git diff --check limpio. Build y tests pasan en Debug y Release.
- **Históricos**: 415/104 → 474/114/58 → 487/115/70 → 490/115/75 → 532/137 → 547/142 → 558/144 → **607/146**.
- **Pendiente**: abrir el PR. Resultados GitHub pendientes. Azure/Cosmos real no contactados en esta rama (no afirmar validación). Deudas documentadas en BACKEND_AUDIT (unicidad global TokenFcm query-before-write no atómica en Cosmos, StubEmailService, Firebase real, OpenTelemetry, rate limiting sin calibrar).

## R0.5 — Cosmos Data Architecture and Persistence Hardening / PR 2A (implementado)
- **Branch**: `feat/backend-cosmos-data-architecture`
- **Catálogo central**: `CosmosContainerCatalog` + `CosmosContainerDefinition` (18 contenedores: Name, PartitionKeyPath, DefaultTimeToLive, Entity, CompositeIndexes) validados al cargar (sin duplicados, PK `/x` de segmento único, TTL −1 o > 0). Única fuente de verdad: `CosmosDbContext` crea/obtiene contenedores desde el catálogo (sin strings dispersos). Sin throughput dedicado (definiciones y creación no lo admiten). Partition keys conservadas: Usuarios/Planes `/id`, TelemetriaViaje `/viajeId`, resto `/usuarioId`. TTLs conservados.
- **`CosmosDatabaseOptions`** (sección `AzureCosmosDb`, Options pattern + `ValidateOnStart`): Endpoint (URI absoluto, nunca en logs), Key (placeholder `YOUR_AZURE_COSMOS_KEY` validado por readiness, no por opciones), DatabaseName (default `ImpactX-Data`), SharedThroughput (default 400, entero positivo ≤ 1000), RequestTimeoutSeconds (30), MaxRetryAttemptsOnRateLimitedRequests (3), MaxRetryWaitTimeSeconds (30). `CosmosClient` con reintentos 429 limitados (nunca infinitos).
- **`EnsureContainersAsync`** (virtuales granulares para tests): crea la base con throughput manual compartido (fallback sin throughput solo ante 400 BadRequest), crea solo contenedores faltantes sin throughput, re-lee y valida; idempotente; nunca borra/recrea. **Mismatch de partition key → `CosmosSchemaValidationException`**: no borra, no recrea, inicialización `Failed` con descripción segura (sin endpoint/key/nombre de contenedor), readiness Unhealthy, migración controlada requerida (procedimiento en docs). Carrera de creación (Conflict) tolerada con re-lectura validante. `CosmosInitializationService` captura la excepción de schema → Fail sin reintentos.
- **Repos Cosmos (15)**: queries ligadas a usuario/viaje con `QueryRequestOptions.PartitionKey`; point-reads solo con id+PK correctas (Usuarios/Planes `/id`, Dispositivos `(usuarioId, id)`); **`GetByIdAsync(id)` de contenedores `/usuarioId` usa `SELECT TOP 1 * WHERE c.id = @id`** (corrige bug: `ReadItemAsync(id, PartitionKey(id))` devolvía 404 siempre — cross-partition justificada por contrato); `ReplaceItemAsync` en updates; `MaxItemCount` (1/50/100) + detención temprana; todo input por `QueryDefinition` parametrizada (nunca concatenado); `CancellationToken` respetado. `IncidenteQueryBuilder` aislado y testeable (OFFSET/LIMIT enteros del dominio, Cosmos no permite parámetros ahí).
- **`PlanSeeder`**: IDs determinísticos (`FreePlanId`/`BasicPlanId`/`PremiumPlanId` = GUIDs fijos `00000000-0000-0000-0000-00000000000{1,2,3}`), point-read + COUNT parametrizado por nombre (cubre planes legacy con IDs aleatorios sin duplicar), `CreateItemAsync` con Conflict tolerado, 401/403/429/5xx y OCE propagan. **Sin `SELECT * FROM c`**.
- **Índices**: política por defecto de Cosmos; sin composite indexes (todas las `ORDER BY` son de partición única → no requeridos); el catálogo permite representarlos (vacíos) sin duplicar.
- **Total real**: 662 pruebas, 158 Category=Security, 76 contratos V1 conservados, 30 Python, scanner 0 violaciones, NuGet 0 vulnerables, actionlint limpio (7 workflows), Roslyn limpio en C# modificados/nuevos, git diff --check limpio. Build y tests pasan en Debug y Release.
- **Históricos**: 415/104 → 474/114/58 → 487/115/70 → 490/115/75 → 532/137 → 547/142 → 558/144 → 607/146 → **662/158**.
- **Documentación**: `docs/COSMOS_DATA_ARCHITECTURE.md` (nuevo) + AGENTS.md/BACKEND_AUDIT/DEVSECOPS_EVIDENCE actualizados.
- **Pendiente**: abrir el PR. Resultados GitHub pendientes. Azure/Cosmos real no contactados en esta rama (no afirmar validación). Deuda documentada: unicidad global TokenFcm query-before-write no atómica en Cosmos (sin falsa transacción entre contenedores; propuesta futura: contenedor de registro con id = hash del token), StubEmailService, Firebase real, OpenTelemetry, rate limiting sin calibrar.

## R0.6 — Pagination and Query Efficiency / PR 2B (implementado)
- **Branch**: `feat/backend-pagination-query-efficiency`, base `main` en `ab025d6` (PR #27)
- **Modelo de paginación** (`Core/Pagination/`): `PaginationDefaults` (pageSize 20 default, 1–100, token máx 2048 chars), `PagedResult<T>` (Items, ContinuationToken, HasMoreResults, PageSize), `PaginationValidator` (400 con `BadRequestException` para pageSize fuera de rango, token vacío/CR-LF/largo), `PagedResultHttp` (header `X-Continuation-Token`).
- **Contrato legacy conservado**: endpoints existentes (routes/frequent, routes/history, notifications, contacts, devices, monitors, subscription history/payments) siguen devolviendo `List<T>` en body; el siguiente token va en el header `X-Continuation-Token` (nunca URL/body; no se envía en la última página). Endpoints nuevos paginados (body `PagedResult<T>`): `GET /api/trips`, `GET /api/trips/{id}/telemetry`, `GET /api/alerts`, `GET /api/wearable/all`. `pageSize` y `continuationToken` opcionales en todos.
- **Cosmos**: `CosmosPageReader.ReadSinglePageAsync` — un único `ReadNextAsync`, `MaxItemCount = pageSize`, `QueryRequestOptions.PartitionKey`, token del SDK opaco (nunca descodificado); error 400 de Cosmos mapeado a `BadRequestException` genérica (token inválido/expirado, sin detalle). **EF**: `EfPageReader` + `OffsetContinuationToken` (base64 `offset:N`, rechazo de malformados) — consulta con probe `pageSize+1` para decidir `HasMoreResults`; la página final exacta o parcial **nunca** genera token.
- **Point-reads `GetByIdAsync(usuarioId, id)`** (11 repos): `ReadItemAsync` con `PartitionKey(usuarioId)` en Cosmos / doble condición en EF; servicios validan propiedad (404/403). Se conserva `GetByIdAsync(id)` (cross-partition TOP 1) solo donde no hay PK. Aplicado a Viaje (telemetría), Alerta, Incidente, Notificación, Contacto, Monitor, Pago, Ruta, Dispositivo, Wearable, Suscripción.
- **Procesos incrementales** (página→actuar→token, sin acumular en memoria): `RevokeAllByUsuarioIdAsync`, `InvalidateAllByUsuarioIdAsync`, `DeleteAllByUsuarioIdAsync` (dispositivos/notificaciones), `MarkAllAsReadAsync`, `ExpireAllAsync`, `ProcessTrialsEndingAsync`. `PlanService.ExpireSubscriptionsAsync` usa `ExpireAllAsync(process, ct)`; se retiraron `GetExpiredAsync`/`GetTrialsEndingAsync` del flujo de servicio.
- **Incidentes**: `Pagina ≥ 1` y `Tamano` 1–100 (default 20) validados en servicio → 400 (legacy OFFSET/LIMIT conservado, nunca mezclado con token).
- **CORS/OpenAPI**: `WithExposedHeaders("X-Continuation-Token", "X-Correlation-Id")` en `Program.cs` (el token viaja como query param, fuera de `WithHeaders`); operation transformer documenta `pageSize` (min 1, max 100, default 20) y `continuationToken` (token opaco del header); 400 ya cubierto por el response transformer.
- **Total real**: 731 pruebas (705 baseline + 26 nuevas: EfPageReader 11, CosmosPageReader 8, contrato paginación +7 con JSON legacy y 6 Category=Security), 164 Category=Security, 77 contratos V1, 30 Python, scanner 0 violaciones (282 archivos), NuGet 0 vulnerables, actionlint limpio, dotnet format limpio en C# modificados/nuevos, git diff --check limpio. Build y tests pasan en Debug y Release (731/731 ambos). OpenAPI verificado en vivo (`/openapi/v1.json`, 79 paths, min/max y descripciones presentes).
- **Históricos**: 415/104 → 474/114/58 → 487/115/70 → 490/115/75 → 532/137 → 547/142 → 558/144 → 607/146 → 662/158 → 705/158 → **731/164**.
- **Documentación**: `docs/COSMOS_PAGINATION.md` (nuevo) + AGENTS.md/BACKEND_AUDIT/DEVSECOPS_EVIDENCE actualizados.
- **Pendiente**: abrir el PR. Resultados GitHub pendientes. Azure/Cosmos real no contactados (no afirmar validación). Deudas previas vigentes (TokenFcm no atómico, StubEmailService, Firebase, OpenTelemetry, rate limiting sin calibrar).

## R0.7 — Wearable V1 Route Alias (implementado)
- **Branch**: `fix/wearable-v1-route-alias`
- **Alias V1 agregado**: `GET /api/v1/wearable/all` como segundo atributo de ruta sobre el mismo método `GetWearables` de `WearableController` (patrón de atributos apilados ya usado en `UsersController`/`SubscriptionController`). La ruta legacy `GET /api/wearable/all` se conserva exactamente: mismo método, misma autorización (`[Authorize]` de clase + `UsuarioId` del claim `ClaimTypes.NameIdentifier`), mismos parámetros opcionales (`pageSize`, `continuationToken`) y mismo body `PagedResult<T>`. Sin lógica duplicada, sin DTOs/paginación/repositorios/Cosmos modificados, sin ruta plural `api/v1/wearables/all`.
- **OpenAPI**: el alias aparece en `/openapi/v1.json` (filtro `ShouldInclude` de `api/v1/`); `pageSize` y `continuationToken` documentados automáticamente por `OpenApiV1OperationTransformer`.
- **Pruebas**: `WearableControllerTests` +6 — 401 sin JWT en ambas rutas (2, Category=Security), mismo status + estructura JSON en ambas rutas autenticadas (1), OpenAPI contiene `/api/v1/wearable/all` con `pageSize`/`continuationToken` (2), ausencia de la ruta plural (1).
- **Total real**: pendiente de la validación final de esta rama.
- **Pendiente**: abrir el PR. Resultados GitHub pendientes. Azure/Cosmos real no contactados en esta rama (no afirmar validación). Deudas previas vigentes (TokenFcm no atómico, StubEmailService, Firebase, OpenTelemetry, rate limiting sin calibrar).

## R0.8 — Telemetry Ingestion and Idempotency / PR 2C (implementado)
- **Branch**: `feat/backend-telemetry-ingestion-idempotency`
- **Endpoint**: `POST /api/v1/trips/{id}/telemetry` (rutas duales legacy/V1 sobre la misma acción en `TripsController`) — ingesta por lotes de telemetría con **idempotencia por `EventId`** (GUID del cliente = `Id` persistido en `TelemetriaViaje`, partition key `/viajeId`, sin upsert, sin consultas globales). `[Authorize]` de clase, `usuarioId` siempre del JWT. `[EnableRateLimiting("telemetry-ingestion")]` (misma política que el PATCH legacy) + `[RequestSizeLimit(32768)]` → 413 en Kestrel para payloads > 32 KB. El lote máximo válido (100 eventos con todos los campos) serializa **≈ 19 KB** y cabe en los 32 KB (verificado por prueba).
- **Contrato**: `TelemetryBatchRequest` (`eventos`: 1–100), `TelemetryEventRequest` (`eventId` GUID único en el lote, `timestamp` UTC obligatorio con sufijo `Z`/`+00:00` vía `UtcTimestampJsonConverter` — sin sufijo → 400; tolerancia de 5 min al futuro; rangos lat `[-90,90]`, lng `[-180,180]`, velocidad `[0,400]`, altitud `[-1000,10000]`, heading `[0,360)`; NaN/Infinity → 400) y `TelemetryIngestionResultDto` (`viajeId`, `recibidos`, `insertados`, `duplicados`, `primerEventoUtc`, `ultimoEventoUtc`). Límites y validación en `Core/Telemetry/` (`TelemetryIngestionLimits`, `TelemetryBatchValidator`, `TelemetryEventEquality`).
- **Idempotencia**: point-read por evento antes del batch; evento existente **idéntico** → duplicado (no se reinserta); **diferente** → `ConflictException` → 409 genérico (protege el registro original). Sin eventos nuevos el servicio **no crea ningún batch**. `RecibidoEn` (nuevo campo en `ViajeTelemetry`, UTC del servidor) **no** participa en la igualdad idempotente. **Semántica atómica (auditada)**: un batch fallido **nunca** cuenta inserciones — los estados individuales de operación (200/409/FailedDependency) no declaran escrituras dentro de un batch global fallido; la clasificación tras el fallo es por point-read (`id` + `PartitionKey(viajeId)`). Carrera → reintento acotado: batch reconstruido **solo con pendientes**, **máximo 1 inicial + 2 reintentos** (`CancellationToken` respetado); conflicto de contenido → 409 sin reintentar ni revelar EventId; todos idénticos → `Insertados=0`/`Duplicados=Recibidos` sin segundo batch; agotamiento → `CosmosException` segura sin afirmar inserciones. **Invariante 200**: `Recibidos == Insertados + Duplicados`.
- **Estados**: solo `Activo`/`Pausado` (misma regla legacy); otro estado → 409. Viaje inexistente → 404; viaje ajeno → `ForbiddenException` interno mapeado a **404** genérico por `ProblemDetailsMiddleware` (sin revelar existencia).
- **Repos**: `IViajeRepository` + `GetTelemetryByEventIdAsync` + `AddTelemetryBatchAsync`. Cosmos: `ReadItemAsync(id, PartitionKey(viajeId))` + `TransactionalBatch` + ctor `internal` para tests (sin upsert/replace; `TransactionalBatchResponse`/`ItemResponse<T>` mockeables: ctores protegidos + getters virtuales). EF: pre-check `ViajeId+Id` (InMemory no lanza en claves duplicadas — verificado) + `AddRange` + un `SaveChangesAsync` (sin simular TransactionalBatch).
- **Servicio**: `ViajeService.IngestTelemetryAsync` — valida → propiedad del viaje → estado → duplicados → batch → resultado; logs solo con conteos, nunca detalles del payload.
- **OpenAPI**: descripción del POST (límites, reintentos seguros, EventId, UTC) en `OpenApiV1OperationTransformer` + `[ProducesResponseType(typeof(TelemetryIngestionResultDto), 200)]` (el generador no infiere 200 para `IActionResult`).
- **Total real**: 837 pruebas (737 baseline + 100 de PR 2C: ingesta 84 + auditoría atómica 8 + igualdad exacta de CodeQL 8), 183 Category=Security (166 + 17), 77 contratos V1, 30 Python, scanner 0 violaciones (294 archivos), NuGet 0 vulnerables, actionlint limpio, dotnet format limpio en C# modificados/nuevos (CRLF), git diff --check limpio. Build y tests pasan en Release (837/837; dirigidas del filtro CodeQL 106/106, Security 183/183).
- **Históricos**: 415/104 → 474/114/58 → 487/115/70 → 490/115/75 → 532/137 → 547/142 → 558/144 → 607/146 → 662/158 → 705/158 → 731/164 → **737/166 → 829/183 → 837/183**.
- **Documentación**: `docs/TELEMETRY_INGESTION.md` (nuevo) + AGENTS.md/BACKEND_AUDIT/DEVSECOPS_EVIDENCE/COSMOS_DATA_ARCHITECTURE actualizados.
- **Pendiente**: abrir el PR. Resultados GitHub pendientes. Azure/Cosmos real no contactados en esta rama (no afirmar validación). Deudas previas vigentes (TokenFcm no atómico, StubEmailService, Firebase, OpenTelemetry, rate limiting sin calibrar).

## Rules
- **No commits or pushes without explicit authorization**
- **Trabajar siempre en una rama feature y utilizar Pull Request. No hacer push directo a main.**
- **No Docker** in dev tasks unless explicitly requested
- **Run `dotnet restore`, `dotnet build`, `dotnet test` after each batch of changes**
- **JWT secret must be set externally** — `Jwt__Secret` via env var or `dotnet user-secrets set "Jwt:Secret" "..."`. Minimum 32 bytes UTF-8.
- **No hardcoded secrets** — `scripts/security/check_hardcoded_secrets.py` scans for violations. Run `python3 scripts/security/check_hardcoded_secrets.py` before PR.

## ImpactX canonical monitoring contract (backend-complete-v1)
- `MonitoringRelationships` is the operational source for monitor authorization,
  quick messages and alert recipients.
- Do not use `Monitores` to authorize new behavior; it remains legacy-only until
  a controlled migration is approved.
- Alert dispatch requires an accepted relationship with both
  `ReceiveCriticalAlerts` and `ReceiveNotifications` enabled.
- `/api/v1/permissions/mobile` is mobile-only and
  `/api/v1/permissions/web` is web-only.
- Do not add free-text messaging. Quick messages always use approved templates.

## Checkpoint interno — Galaxy Watch 8 y telemetría V2
- Wearable objetivo validado por backend: Samsung Galaxy Watch 8 con WearOS.
- Vinculación/configuración/desvinculación: `client=mobile`; heartbeat, batería,
  diagnóstico y sync operativo: `client=wearable`.
- Telemetría V2: hasta 100 eventos / 256 KiB, `batchId`, `batchSequence`,
  `sequenceNumber`, procedencia del wearable, GPS, acelerómetro, giroscopio,
  biometría opcional, orientación, calidad y sincronización offline.
- La magnitud de movimiento se calcula en servidor cuando están presentes los
  tres ejes. Las etiquetas de reglas/ML son de solo servidor.
- Compatibilidad: esquema V1 y documentos históricos siguen siendo legibles.
- No se agregaron contenedores Cosmos ni cambios destructivos. La validación
  Release debe ejecutarse en Arch antes de commit, push o despliegue.


## Checkpoint interno — mobile sync + impact-rules-v1
- `GET /api/v1/mobile/sync/bootstrap` es exclusivo de `client=mobile` y solo
  entrega un snapshot de lectura; nunca habilita start/pause/resume/finish ni
  escritura de telemetría.
- `impact-rules-v1` corre en servidor sobre telemetría canonicalizada. Ningún
  cliente puede escribir etiquetas, versión de regla, puntaje o versión ML.
- Alertas moderadas tienen ventana de cancelación de 10 segundos; severas y
  críticas se envían inmediatamente a destinatarios internos autorizados.
- Idempotencia de alerta: `sourceTelemetryEventId`; no crear una segunda alerta
  por reintentar el mismo evento.
- No integrar 911, SMS, WhatsApp ni canales externos automáticos.

## Checkpoint interno — finalización funcional V8
- V8 concentra los bloques antes separados de sincronización offline,
  suscripciones, incidentes y ciclo de cuenta; no recorta funcionalidades.
- Mobile sync V2: bootstrap/changes/push/ack, máximo 50 operaciones, recibos
  idempotentes por `operationId`, y 200 recibos recientes embebidos en Usuario.
- Plan público: Free/Standard/Premium. `Basic` permanece solo como nombre legacy
  de almacenamiento para Standard.
- Vigencia: ciclo mensual simulado, anual opcional y gracia de tres días.
- Incidente: upsert único por alerta, gestión web/móvil y TTL por documento de
  365 días.
- Eliminación de cuenta: revocación de sesiones + anonimización inmediata; los
  registros de dominio siguen TTL, sin borrado masivo destructivo.
- No se agregan contenedores, no se cambian partition keys, throughput ni secretos.
- Tras validar V8, V9 será exclusivamente cierre: legacy/OpenAPI, seguridad,
  pruebas Cosmos/Firebase/Azure y congelamiento del contrato para frontend.

## Checkpoint final — Backend V9 y contrato congelado
- La API canónica queda congelada en `2026.08.04`; cambios incompatibles requieren
  nueva versión y no deben alterar silenciosamente `/api/v1`.
- Contrato runtime: `GET /api/v1/meta/contract`; capacidades públicas por cliente
  en `GET /api/v1/meta/clients/{web|mobile|wearable}`.
- Toda respuesta expone `X-ImpactX-Api-Version` y
  `X-ImpactX-Contract-Version`.
- Las rutas legacy se conservan temporalmente con `Deprecation`, `Sunset`,
  `Warning`, `Link` y `X-ImpactX-Legacy-Route`; el frontend nuevo no debe usarlas.
- V9 no agrega contenedores Cosmos, no cambia partition keys, throughput ni TTL.
- La validación externa debe ser de solo lectura para Cosmos y mediante flujos de
  prueba controlados para Firebase/Azure.
- El siguiente trabajo funcional es conectar y completar frontend, móvil y
  wearable sobre el contrato congelado; no agregar endpoints ad hoc para la UI.
