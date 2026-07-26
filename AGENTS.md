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

## Implemented features (15 controllers, 14+ services)
- **Auth** (register, login, logout, recover/reset password, sessions, account export/delete)
- **Users** (profile CRUD, driver profile, medical profile, preferences, permissions, settings)
- **Plans + Subscriptions + Payments**
- **Contacts** (emergency contacts CRUD)
- **Monitors**
- **Routes** (Rutas)
- **Trips** (Viajes + telemetry)
- **Wearables**
- **Alerts** (Alertas)
- **Incidents** (Incidentes)
- **Notifications** (Notificaciones)
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
- `Infrastructure/Data/` — `ApplicationDbContext` (EF Core, 14 DbSets with full Fluent API config), `CosmosDbContext` (singleton, 17 containers), `PlanSeeder`
- `Infrastructure/Data/Repositories/EF/` — 14 EF repository implementations
- `Infrastructure/Data/Repositories/Cosmos/` — 14 Cosmos repository implementations
- `Infrastructure/Security/` — `EncryptionService`, `JwtTokenService`, `StubEmailService`

## Cosmos DB specifics
- `CosmosDbContext` is a **singleton** — wraps `CosmosClient` + 17 container references
- `EnsureContainersAsync()` creates containers with partition keys and TTLs on startup
- 17 containers: Usuarios, RefreshTokens, PasswordResetTokens, Planes, Suscripciones, Pagos, Monitores, ContactosEmergencia, Rutas, Viajes, TelemetriaViaje, Alertas, Notificaciones, Wearables, AppInvites, ChatThreads, Incidentes
- Cosmos key via `AzureCosmosDb:Key` or env var `COSMOS_KEY`
- Property naming: `CosmosPropertyNamingPolicy.CamelCase`
- All 3 appsettings files (`appsettings.json`, `Development.json`, `Production.json`) set `UseCosmosDb: true` with real endpoint but `Key: "YOUR_AZURE_COSMOS_KEY"` — must be replaced or overridden
- `appsettings.Development.json` loads additional `appsettings.Local.json` (gitignored)

## JWT auth
- HMAC-SHA256 symmetric signing via `JwtTokenService` (`ITokenService`)
- Config section `Jwt:{Secret,Issuer,Audience}` — reads `Secret` then falls back to `SecretKey`
- **Hardcoded fallback** in code if config value missing or <16 chars: `"ImpactX_Super_Secret_JWT_Key_2026_Executive_Key_V12!"`
- Default issuer: `"ImpactXApi"`, audience: `"ImpactXClients"`
- `ClockSkew = TimeSpan.Zero`
- User ID from `ClaimTypes.NameIdentifier`

## Middleware pipeline (order matters in Program.cs)
1. `ExceptionHandlingMiddleware` — catches unhandled exceptions, returns `ErrorResponse`
2. `RequestLoggingMiddleware`
3. `SecurityHeadersMiddleware`
4. `app.UseCors("AllowLocalhost")`
5. `UseAuthentication()` / `UseAuthorization()`
6. `MapControllers()`
7. `MapHealthChecks` — three endpoints: `/health`, `/health/live`, `/health/ready`

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
- `builder.Services.AddHealthChecks()` — includes `live` and `ready` checks with tags
- Three endpoints with unified JSON response (`status`, `service`, `environment`, `timestamp` as `DateTimeOffset`)
- **`GET /health`** — aggregates all checks (no tag filter), same format
- **`GET /health/live`** — filtered by `"live"` tag (process alive)
- **`GET /health/ready`** — filtered by `"ready"` tag (app ready for requests)
- Response writer in `Program.cs::WriteHealthCheckResponse` — safe, no internals exposed
- No authentication, no authorization on any health endpoint

## Tests — 317 total (xUnit + Moq + WebApplicationFactory)
- **Unit tests** (14 files in `ImpactX.Tests/Unit/`): pure Moq per service
- **Integration tests** (16 files in `ImpactX.Tests/Integration/`): `CustomWebApplicationFactory<Program>` forces `UseCosmosDb=false` + `UseInMemoryDatabase=true`, injects test JWT secret
- API project exposes `partial class Program` for `WebApplicationFactory<Program>`

## CI/CD — 6 GitHub Actions workflows
1. **dotnet-ci.yml** — Pipeline principal de CI:
   - `push` a `main`, `leo-desarrollo`, `feat/**`
   - `pull_request` hacia `main`
   - `workflow_dispatch`
   - **Job `build-and-test`** (15 min): checkout → setup-dotnet 10.0.x → restore con NuGet audit → build Release → test (317, TRX + XPlat Code Coverage) → publica TRX + cobertura
   - **Job `smoke-test`** (5 min, depende de build-and-test): checkout → setup-dotnet → restore API → publish Release → ejecución directa de `ImpactX.Api.dll` con `dotnet`, usando `UseCosmosDb=false`, `UseInMemoryDatabase=true`, `ASPNETCORE_ENVIRONMENT=CI`, `ASPNETCORE_URLS=http://127.0.0.1:5055`, JWT de test → valida 4 endpoints (`/health`, `/health/live`, `/health/ready`, `/openapi/v1.json`) con Python 3 (status, service, environment, timestamp, schema) → verifica Swagger 404 → publica log de arranque
   - **Sin Docker**: el smoke test ejecuta `dotnet publish` + `ImpactX.Api.dll` directamente
2. **main_impactx-api-backend.yml** — push to `main` only + `workflow_dispatch`: build (15 min) → publish → deploy (20 min, oidc) to Azure Web App `impactx-api-backend`. Permissions: `build: contents: read`, `deploy: id-token: write + contents: read`
3. **api-security-audit.yml** — OWASP API security scan. Triggers: push + pull_request (main, leo-desarrollo) + `workflow_dispatch`. Timeout: 15 min. Permissions: `contents: read`.
4. **secret-scanning.yml** — Gitleaks credential scan. Triggers: push + pull_request (main, leo-desarrollo) + `workflow_dispatch`. Timeout: 10 min. Permissions: `contents: read`.
5. **codeql-analysis.yml** — CodeQL SAST (C#, security-extended). Triggers: push + pull_request (main, leo-desarrollo) + `workflow_dispatch`. Timeout: 15 min. Permissions: `contents: read, security-events: write`.
6. **code-quality-roslyn.yml** — Roslyn format estricto solo sobre archivos C# modificados. Triggers: push + pull_request (main, leo-desarrollo) + `workflow_dispatch`. Timeout: 15 min. Permissions: `contents: read`. No oculta fallos (sin `|| echo`, `|| true` ni `continue-on-error`). Si no hay C# modificados, termina correctamente sin ejecutar `dotnet format`. La deuda histórica de 14.673 errores ENDOFLINE/FINALNEWLINE en archivos no modificados no afecta este check.

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
- JWT secret: hardcoded fallback in code — **not a placeholder**, this is a real risk
- Dev ignores `appsettings.Local.json` (gitignored)
- Production JWT secret is empty in `appsettings.Production.json` — **must be set via env vars or Azure Key Vault**

## Risks
- **JWT secret hardcoded** in `ServiceCollectionExtensions.cs` line 84 — not a placeholder, active fallback
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

## Rules
- **No commits or pushes without explicit authorization**
- **Trabajar siempre en una rama feature y utilizar Pull Request. No hacer push directo a main.**
- **No Docker** in dev tasks unless explicitly requested
- **Run `dotnet restore`, `dotnet build`, `dotnet test` after each batch of changes**
