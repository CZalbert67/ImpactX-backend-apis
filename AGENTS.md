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
7. `MapHealthChecks("/health")`

## OpenAPI + Scalar (Development only)
```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}
```
- OpenAPI spec at `/openapi/v1.json`
- Scalar UI replaces Swagger UI (package `Scalar.AspNetCore`)

## Health checks
- `builder.Services.AddHealthChecks()` — basic, no custom health checks registered
- Mapped at `GET /health` (no authentication, no authorization)

## Tests — 306 total (xUnit + Moq + WebApplicationFactory)
- **Unit tests** (14 files in `ImpactX.Tests/Unit/`): pure Moq per service
- **Integration tests** (16 files in `ImpactX.Tests/Integration/`): `CustomWebApplicationFactory<Program>` forces `UseCosmosDb=false` + `UseInMemoryDatabase=true`, injects test JWT secret
- API project exposes `partial class Program` for `WebApplicationFactory<Program>`

## CI/CD — 6 GitHub Actions workflows
1. **dotnet-ci.yml** — push/PR to `leo-desarrollo` / `main`: restore → NuGet audit (NU1903/1904 as error) → build Release → test with XPlat Code Coverage + TRX → docker smoke test (health check at `/health`)
2. **main_impactx-api-backend.yml** — push to `main` only: build → publish → deploy to Azure Web App `impactx-api-backend` (oidc login)
3. **api-security-audit.yml** — OWASP API security scan
4. **secret-scanning.yml** — Gitleaks credential scan
5. **codeql-analysis.yml** — CodeQL SAST (C#, security-extended)
6. **code-quality-roslyn.yml** — `dotnet format --verify-no-changes`

## Docker
- Multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` → `mcr.microsoft.com/dotnet/aspnet:10.0`
- Port **8080** (`ASPNETCORE_HTTP_PORTS=8080`)
- Entrypoint: `dotnet ImpactX.Api.dll`

## Dev server
- HTTP: `http://localhost:5000` / `http://localhost:5161`
- HTTPS: `https://localhost:7278`
- Launch profiles: `http` and `https` (both set `ASPNETCORE_ENVIRONMENT=Development`)
- API routes: `api/auth/*`, `api/users/*`, `api/plans/*`, `api/subscription/*`, `api/wearable/*`, `api/permissions/*`, `api/contacts/*`, `api/monitors/*`, `api/routes/*`, `api/trips/*`, `api/alertas/*`, `api/incidentes/*`, `api/notificaciones/*`, `api/analytics/*`, `api/settings/*`

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

## Rules
- **No commits or pushes without explicit authorization**
- **Trabajar siempre en una rama feature y utilizar Pull Request. No hacer push directo a main.**
- **No Docker** in dev tasks unless explicitly requested
- **Run `dotnet restore`, `dotnet build`, `dotnet test` after each batch of changes**
