# Auditoría de Línea Base — ImpactX Backend APIs

## Datos de la auditoría

| Campo | Valor |
|---|---|
| Rama auditada | `feat/backend-platform-foundation` |
| Último commit | (Ronda 9 — Backend Security Hardening) |
| .NET SDK | 10.0.110 |
| Framework objetivo | `net10.0` |
| Solución | `ImpactX.slnx` |
| Proyecto API | `ImpactXv1/ImpactX.Api.csproj` |
| Proyecto de pruebas | `ImpactX.Tests/ImpactX.Tests.csproj` |
| Build Release | Correcto |
| Pruebas | **367 de 367** correctas (52 Security) |

---

## Requisitos que cumple

| # | Requisito | Verificación |
|---|---|---|
| 1 | .NET 10 | Ambos `.csproj` usan `net10.0`. Paquetes NuGet en versiones `10.0.9` |
| 2 | UUID como identificadores | Todos los Id de dominio y DTOs son `Guid`. `Guid.NewGuid()` como valor por defecto |
| 3 | JSON camelCase | System.Text.Json serializa en camelCase por defecto. No se registró `AddNewtonsoftJson()` |
| 4 | Fechas en UTC | Todos los valores por defecto usan `DateTime.UtcNow`. No hay `DateTime.Now` en el código |
| 5 | Bearer JWT | HMAC-SHA256. `ConfigureJwtAuthentication()` en pipeline. `ClockSkew = TimeSpan.Zero` |
| 6 | 367 pruebas | 52 Security (JWT config, revocation, recover/reset, refresh token, FCM, auth, authorization) |
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
| 17 | 7 pipelines CI/CD | .NET CI, Azure deploy, OWASP audit, Gitleaks, CodeQL, Roslyn format, Bicep infra validation |

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
| R01 | JWT secret externalizado con fail-fast | 🟢 Mitigado | Ronda 9: `JwtSecurityConfiguration.GetRequiredSecret()` centraliza validación, rechaza <32 bytes, lanza `InvalidOperationException` si no configurado. Sin fallback en appsettings ni código. |
| R02 | CORS permisivo con AllowCredentials | 🔴 Crítico | `SetIsOriginAllowed(_ => true)` + `AllowCredentials()` permite cualquier origen con credenciales |
| R03 | Cosmos key placeholder en 3 appsettings | 🔴 Crítico | `Key: "YOUR_AZURE_COSMOS_KEY"`. Debe reemplazarse en producción. |
| R04 | `test_output.txt` eliminado del repositorio | 🟢 Mitigado | Archivo eliminado del repositorio y agregado al `.gitignore`. |
| R05 | Production JWT secret vacío | 🟢 Mitigado | Ronda 9: `JwtSecurityConfiguration.GetRequiredSecret()` lanza `InvalidOperationException` en startup si `Jwt:Secret` falta, está vacío o tiene <32 bytes. Sin fallback. |
| R06 | Sin analizadores Roslyn | 🟠 Importante | No hay `Directory.Build.props` ni `<AnalysisMode>`. Solo se ejecuta `dotnet format --verify-no-changes` en CI |

---

## Orden inicial de implementación

| Paso | Cambio | Dependencias |
|---|---|---|
| 1 | Eliminar `test_output.txt` del repositorio y registrar la eliminación con git add -A | ✅ Hecho |
| 2 | Restringir CORS a orígenes conocidos o eliminar `AllowCredentials` | Ninguna |
| 3 | Eliminar fallback hardcodeado del JWT secret. Exigir clave por config/env/user-secrets | ✅ Ronda 9 |
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
| `.gitattributes` | ✅ Creado (`* text=auto`, `*.cs text eol=crlf diff=csharp`) |
| `ImpactXv1/Program.cs` | ✅ Modificado (`app.MapOpenApi()` fuera del bloque Development) |
| `ImpactX.Tests/Integration/PlatformFoundationTests.cs` | ✅ Modificado (prueba `OpenApiV1Json_Returns200_InProduction` agregada) |
| `AGENTS.md` | ✅ Modificado (317 tests, OpenAPI global, Scalar+Swagger solo Dev) |
| `docs/BACKEND_AUDIT.md` | ✅ Modificado (documentación de Ronda 2) |

- Se configuró `core.whitespace cr-at-eol` en el repositorio.
- `.editorconfig` exige `end_of_line = crlf` para todos los archivos.
- `.gitattributes` configura CRLF para los archivos C# en el directorio de trabajo durante el checkout, mientras Git mantiene el contenido de texto normalizado internamente.
- `Program.cs` y `PlatformFoundationTests.cs` usan CRLF.
- El checkout limpio todavía presenta 14.673 errores ENDOFLINE/FINALNEWLINE preexistentes en otros archivos (ViajeService.cs, WearableService.cs, y otros 13 archivos).
- `app.MapOpenApi()` ahora se ejecuta en todos los ambientes; solo Scalar y Swagger UI permanecen en Development.
- Total de pruebas: **317** (306 base + 11 nuevas: 9 de Ronda 1 + 2 de Ronda 2).

### Ronda 3 — Pipeline Foundation (backend-ci-foundation)

| Archivo | Acción |
|---|---|
| `.github/workflows/dotnet-ci.yml` | ✅ Triggers actualizados: `push` a `main`/`leo-desarrollo`/`feat/**`, `pull_request` a `main`, `workflow_dispatch` |
| `.github/workflows/dotnet-ci.yml` | ✅ `docker-smoke-test` eliminado, reemplazado por `smoke-test` con `dotnet publish` y ejecución directa de `ImpactX.Api.dll` con `dotnet` |
| `.github/workflows/dotnet-ci.yml` | ✅ Validación de 4 endpoints: `/health`, `/health/live`, `/health/ready`, `/openapi/v1.json` |
| `.github/workflows/dotnet-ci.yml` | ✅ Validación JSON con Python 3: status, service, environment, timestamp ISO 8601 |
| `.github/workflows/dotnet-ci.yml` | ✅ Validación de `GET /swagger/index.html` → 404 en CI |
| `.github/workflows/dotnet-ci.yml` | ✅ Publicación de log de arranque como artefacto (`api-smoke-log`) |
| `.github/workflows/dotnet-ci.yml` | ✅ `timeout-minutes: 5` en smoke-test, `set -Eeuo pipefail`, `trap` para limpieza |
| `AGENTS.md` | ✅ Actualizado con flujo real del pipeline principal |
| `docs/BACKEND_AUDIT.md` | ✅ Documentación de avance del pipeline |

#### Detalle del nuevo job `smoke-test`

```
env:
  ASPNETCORE_ENVIRONMENT: CI
  ASPNETCORE_URLS: http://127.0.0.1:5055
  UseCosmosDb: "false"
  UseInMemoryDatabase: "true"
  Jwt__Secret: ImpactX_CI_Test_Secret_2026_Only_For_Automated_Tests_123456
  Jwt__Issuer: ImpactX-CI
  Jwt__Audience: ImpactX-CI-Client

pasos:
  1. checkout + setup-dotnet + restore (solo API project)
  2. dotnet publish --configuration Release
  3. Iniciar API en background con trap EXIT
  4. Espera activa (30 intentos × 2s) hasta /health responda 200
  5. Validar /health → JSON con status=healthy, service=impactx-api, environment=CI, timestamp ISO 8601
  6. Validar /health/live → mismo schema
  7. Validar /health/ready → mismo schema
  8. Validar /openapi/v1.json → contiene openapi y paths (objeto)
  9. Validar /swagger/index.html → 404 en CI
  10. Publicar log de arranque como artefacto (if: always(), retention 7d)
```

Sin Docker. Sin contenedores.

#### dotnet format — verificación en checkout limpio

Se creó un clon temporal del repositorio en `/tmp/clean-checkout` y se ejecutó:

```
dotnet restore ImpactX.slnx
dotnet format ImpactX.slnx --verify-no-changes --no-restore
```

**Resultado**: Exit code 2. **14.673 errores** ENDOFLINE/FINALNEWLINE.

Los errores se concentran en `ViajeService.cs` (16 líneas), `WearableService.cs` (el archivo completo, 185 líneas), más 13 archivos con FINALNEWLINE en ambos proyectos (PlanType.cs, IEncryptionService.cs, ITokenService.cs, UsuarioRepository.cs, EncryptionService.cs, ExceptionHandlingMiddleware.cs, RequestLoggingMiddleware.cs, SecurityHeadersMiddleware.cs, AuthResponse.cs, ErrorResponse.cs, LoginRequest.cs, RegisterRequest.cs, IAuthService.cs).

**Decisión**: No se agregó `dotnet format --verify-no-changes` al pipeline principal porque fallaría en checkout limpio. El workflow `code-quality-roslyn.yml` se conserva como control separado. Los errores de formato son preexistentes (14.673) y deben abordarse en una ronda futura corrigiendo los finales de línea o la configuración de `.editorconfig`/`.gitattributes`.

#### Problemas pendientes de los workflows secundarios

| Workflow | Problema |
|---|---|
| `main_impactx-api-backend.yml` | `include-prerelease: true` obsoleto; sin `timeout-minutes` |
| `api-security-audit.yml` | `include-prerelease: true` obsoleto; duplica restore/build del pipeline principal |
| `codeql-analysis.yml` | `include-prerelease: true` obsoleto; duplica build |
| `code-quality-roslyn.yml` | `include-prerelease: true` obsoleto; 14.673 errores ENDOFLINE/FINALNEWLINE |
| `secret-scanning.yml` | Sin problemas detectados |

### Ronda 4 — Secondary Workflows Foundation (fix/secondary-workflows-foundation)

| Archivo | Acción |
|---|---|
| `.github/workflows/api-security-audit.yml` | ✅ Eliminado `include-prerelease: true`. Agregado `workflow_dispatch`. Agregado `timeout-minutes: 15`. Permisos `contents: read` |
| `.github/workflows/codeql-analysis.yml` | ✅ Eliminado `include-prerelease: true`. Agregado `workflow_dispatch`. Conserva `timeout-minutes: 15`. Permisos `contents: read, security-events: write` |
| `.github/workflows/code-quality-roslyn.yml` | ✅ Eliminado `include-prerelease: true`. Agregado `workflow_dispatch`. Agregado `timeout-minutes: 15`. Permisos `contents: read`. Eliminados ocultadores de error (`|| echo`). Agregado `fetch-depth: 0`. Implementada detección de archivos C# modificados según evento (`pull_request`/`push`/`workflow_dispatch`). `dotnet format` solo se ejecuta sobre archivos C# modificados. Si no hay cambios, termina correctamente sin ejecutar `dotnet format` |
| `.github/workflows/main_impactx-api-backend.yml` | ✅ Eliminado `include-prerelease: true`. Agregado `timeout-minutes: 15` (build) y `timeout-minutes: 20` (deploy). Permisos: `build: contents: read`, `deploy: id-token: write + contents: read` |
| `.github/workflows/secret-scanning.yml` | ✅ Agregado `workflow_dispatch`. Agregado `timeout-minutes: 10`. Permisos `contents: read` |
| `.github/workflows/dotnet-ci.yml` | ✅ Sin cambios (no modificado) |
| `AGENTS.md` | ✅ Actualizado con detalles de los 6 workflows |
| `docs/BACKEND_AUDIT.md` | ✅ Documentación de Ronda 4 |

#### Correcciones aplicadas

1. **Eliminación de `include-prerelease: true`**: No es una entrada válida de `actions/setup-dotnet@v4`. Se eliminó de `api-security-audit.yml`, `codeql-analysis.yml`, `code-quality-roslyn.yml` y `main_impactx-api-backend.yml`.

2. **Timeouts**: Se agregaron timeouts a jobs que no tenían:
   - `api-security-audit.yml`: 15 min
   - `code-quality-roslyn.yml`: 15 min
   - `main_impactx-api-backend.yml`: build 15 min, deploy 20 min
   - `secret-scanning.yml`: 10 min

3. **`workflow_dispatch`**: Agregado a `api-security-audit.yml`, `codeql-analysis.yml`, `code-quality-roslyn.yml` y `secret-scanning.yml`. `main_impactx-api-backend.yml` ya lo tenía.

4. **Permisos mínimos**: CodeQL conserva `security-events: write`. Azure deploy conserva `id-token: write + contents: read`. El resto usa `contents: read`.

5. **Versiones de acciones**: Todas conservadas — `actions/checkout@v4`, `setup-dotnet@v4`, `upload-artifact@v4`, `download-artifact@v4`, `github/codeql-action/init@v3`, `github/codeql-action/analyze@v3`, `azure/login@v2`, `azure/webapps-deploy@v3`, `gitleaks/gitleaks-action@v2`.

#### Resultado de actionlint

```
actionlint .github/workflows/*.yml
→ 0 errores estructurales en los 6 workflows
```

#### Roslyn estricto sobre archivos modificados

En Ronda 4 se reescribió `code-quality-roslyn.yml` para:
- Eliminar `|| echo` que ocultaba errores de `dotnet format`.
- Agregar `fetch-depth: 0` en `actions/checkout@v4`.
- Detectar archivos C# modificados según el tipo de evento:
  - `pull_request`: compara `github.event.pull_request.base.sha` con `GITHUB_SHA`
  - `push`: compara `github.event.before` con `GITHUB_SHA`
  - `workflow_dispatch`: compara `HEAD^` con `HEAD`
- Si `BASE` es un SHA compuesto completamente por ceros, usar `HEAD^` como fallback.
- Filtrar solo archivos `*.cs` con estados Added, Copied, Modified o Renamed (`--diff-filter=ACMR`).
- Si no hay archivos C# modificados, terminar correctamente (`exit 0`) sin ejecutar `dotnet format`.
- Si hay archivos modificados, ejecutar estrictamente:
  ```
  dotnet format ImpactX.slnx --verify-no-changes --no-restore --include "${CS_FILES[@]}"
  ```
  sin ocultar fallos (sin `|| echo`, `|| true` ni `continue-on-error`).
- La deuda histórica de 14.673 errores ENDOFLINE/FINALNEWLINE en archivos no modificados no afecta este check.
- CodeQL ya tenía `timeout-minutes: 15` configurado desde rondas anteriores.

#### Deuda pendiente

- La deuda histórica completa de 14.673 errores ENDOFLINE/FINALNEWLINE sigue pendiente, pero está acotada a los archivos legacy no modificados. El workflow `code-quality-roslyn.yml` ya no falla por archivos que no forman parte del cambio.
- Roslyn ya no oculta fallos. El workflow valida estrictamente solo los archivos C# modificados. Si no hay cambios C#, el check termina correctamente.

### Ronda 5 — Azure IaC Foundation (feat/azure-iac-foundation)

| Archivo | Acción |
|---|---|
| `infra/main.bicep` | ✅ Creado — `targetScope='subscription'`, Resource Group, 2 módulos, 11 parámetros, 8 outputs seguros |
| `infra/bicepconfig.json` | ✅ Creado — linter habilitado, 3 reglas en nivel error |
| `infra/modules/monitoring.bicep` | ✅ Creado — Log Analytics + Application Insights workspace-based, 5 outputs |
| `infra/modules/app-service.bicep` | ✅ Creado — Linux App Service Plan + Web App + System-Assigned MI, 4 app settings, 6 outputs |
| `infra/environments/dev.bicepparam` | ✅ Creado — F1 Free, eastus2, retention 30 días |
| `infra/environments/test.bicepparam` | ✅ Creado — B1 Basic, eastus2, retention 30 días |
| `infra/environments/prod.bicepparam` | ✅ Creado — P1v3 PremiumV3, eastus2, retention 365 días |
| `infra/README.md` | ✅ Creado — documentación completa con advertencia de no despliegue |
| `AGENTS.md` | ✅ Modificado — sección Azure IaC añadida |
| `docs/BACKEND_AUDIT.md` | ✅ Modificado — documentación de Ronda 5 |

#### Recursos creados

- Resource Group (`Microsoft.Resources/resourceGroups`)
- Log Analytics Workspace (`Microsoft.OperationalInsights/workspaces`)
- Application Insights workspace-based (`Microsoft.Insights/components`)
- Linux App Service Plan (`Microsoft.Web/serverfarms`)
- Linux Web App con System-Assigned Managed Identity (`Microsoft.Web/sites`)

#### Recursos excluidos

- Cosmos DB (queda fuera)
- Key Vault (queda fuera)
- Machine Learning (queda fuera)
- Storage Accounts, VNets, Private Endpoints
- Alertas, availability tests, diagnostic settings
- Slots, autoscale, backup policies
- Certificados, dominios, CDN, Front Door, API Management
- Role assignments, Azure AD configuration
- Cualquier secreto, JWT, connection string de base de datos

#### Decisiones de diseño

1. **Scope subscription**: `targetScope = 'subscription'` permite que `main.bicep` cree su propio Resource Group, sin depender de uno preexistente.
2. **Naming determinista**: `uniqueString(subscription().id, resourceGroupName)` genera el mismo sufijo siempre para el mismo par suscripción + resource group, evitando colisiones globales.
3. **Sin secretos en outputs**: Application Insights connection string fluye internamente entre módulos pero no aparece en los outputs de `main.bicep`.
4. **AlwaysOn condicional**: Se desactiva solo cuando el SKU es Free (F1) o Shared (D1).
5. **Health check path**: Configurado en `/health/ready`, el endpoint existente de la aplicación.
6. **App settings mínimos**: Solo `ASPNETCORE_ENVIRONMENT`, `APPLICATIONINSIGHTS_CONNECTION_STRING`, `ApplicationInsightsAgent_EXTENSION_VERSION=~3`, `XDT_MicrosoftApplicationInsights_Mode=recommended` y `WEBSITE_HEALTHCHECK_MAXPINGFAILURES`. Sin Cosmos DB ni JWT en esta fase. La Web App queda preparada para autoinstrumentación administrada por App Service.

#### Estado de validación

- Bicep build: `infra/main.bicep` → sin errores ni warnings
- Bicep build-params: `dev`, `test`, `prod` → sin errores ni warnings
- Compilación .NET: `dotnet build --configuration Release` → correcta
- Pruebas: 317/317 correctas
- Paquetes NuGet: 0 vulnerabilidades
- Archivos modificados: solo `infra/`, `AGENTS.md`, `docs/BACKEND_AUDIT.md`
- Workflows de GitHub Actions: no modificados
- Código C#: no modificado
- appsettings: no modificados
- Secretos en infra/: ninguno

#### Endurecimiento (Ronda 5.1)

- Se agregó `XDT_MicrosoftApplicationInsights_Mode=recommended` como app setting para habilitar la autoinstrumentación administrada por App Service.
- Se reescribió la sección de adopción de recursos existentes en README.md eliminando la referencia a un supuesto comando de importación directa de despliegues y la referencia incorrecta a una consulta individual de recursos como entrada directa de decompilación. Ahora documenta correctamente `az group export` + `az bicep decompile`, "Bicep: Insert Resource" en VS Code, y la palabra clave `existing`.
- No se modificaron workflows, C# ni appsettings.

#### Notas

- `linuxFxVersion` usa `DONT_DEPLOY_UNTIL_RUNTIME_IS_VERIFIED` como marcador de posición. Debe sustituirse antes de cualquier what-if o deployment.
- Los SKU (F1, B1, P1v3) son ejemplos. Deben revisarse por disponibilidad regional y costo.
- La pila .NET debe verificarse con `az webapp list-runtimes --os linux | grep DOTNET` antes de desplegar.
- Los recursos existentes (Web App `impactx-api-backend`, Cosmos DB, etc.) no fueron importados ni validados.
- No se ejecutó `az deployment`. No se modificaron workflows de GitHub Actions.

### Ronda 6 — Azure IaC Validation (ci/azure-iac-validation)

| Archivo | Acción |
|---|---|
| `.github/workflows/infra-validation.yml` | ✅ Creado — Bicep Infrastructure Validation |
| `AGENTS.md` | ✅ Modificado — 7 workflows, documentación del nuevo workflow |
| `docs/BACKEND_AUDIT.md` | ✅ Modificado — documentación de Ronda 6 |
| `infra/README.md` | ✅ Modificado — sección de validación automática añadida |

#### Workflow creado

- **Nombre**: Bicep Infrastructure Validation
- **Archivo**: `.github/workflows/infra-validation.yml`
- **Triggers**:
  - `pull_request` a `main` con cambios en `infra/**` o `.github/workflows/infra-validation.yml`
  - `workflow_dispatch` (manual)
- **Sin trigger `push`**
- **Permisos**: `contents: read` únicamente
- **Sin OIDC**: no se configura `id-token: write`
- **Sin Azure login**: no hay comandos `az`
- **Sin despliegue**: no hay `az deployment`, no hay what-if
- **Sin artefactos**: no se publica nada
- **Concurrency**: `cancel-in-progress: true` agrupado por workflow + PR/ref

#### Job: `validate`

- `runs-on: ubuntu-latest`
- `timeout-minutes: 10`
- Checkout con `fetch-depth: 0` (historial completo necesario para `git diff --check`)
- Bash estricto (`set -Eeuo pipefail`) en todos los pasos

#### Bicep CLI

- **Versión fijada**: `0.45.15`
- **SHA-256**: `ff5b194b042c220df4a50d6768ed1d6c39a32894bfdc4ff83d62b115d966a7ce`
- El SHA fue verificado localmente durante la auditoría del repositorio
- Descarga temporal a `$RUNNER_TEMP/bicep/bicep` con `curl` estricto (`--fail`, `--show-error`, `--silent`, `--location`, `--retry 3`, `--retry-all-errors`, `--proto '=https'`, `--tlsv1.2`)
- Validación del archivo con `sha256sum --check --strict`
- Se agrega al `PATH` mediante `GITHUB_PATH`
- No se instala globalmente, no se usa `sudo`, no se consulta la API de GitHub

#### Validaciones realizadas

1. **Compilación Bicep estricta**: `bicep build infra/main.bicep --stdout` y `bicep build-params infra/environments/{dev,test,prod}.bicepparam --stdout`. stdout va a `/dev/null`, stderr se captura en `RUNNER_TEMP`. Falla si exit code != 0 o si hay cualquier diagnóstico/warning en stderr.

2. **Placeholder `DONT_DEPLOY_UNTIL_RUNTIME_IS_VERIFIED`**: Falla si no está presente en exactamente `infra/README.md`, `infra/environments/dev.bicepparam`, `infra/environments/test.bicepparam`, `infra/environments/prod.bicepparam`. Falla si aparece en cualquier otro archivo.

3. **Archivos ARM JSON**: Falla si existe cualquier archivo `*.json` dentro de `infra/` que no sea `infra/bicepconfig.json`.

4. **Secretos prohibidos**: Analiza `*.bicep` y `*.bicepparam`. Patrones prohibidos: `Jwt__Secret`, `AzureCosmosDb__Key`, `AZUREAPPSERVICE_CLIENTID`, `AZUREAPPSERVICE_TENANTID`, `AZUREAPPSERVICE_SUBSCRIPTIONID`, `clientSecret`, `tenantSecret`, `subscriptionSecret`. No analiza README.

5. **Outputs de `main.bicep`**: Falla si cualquier nombre de output contiene (case-insensitive): `secret`, `password`, `token`, `key`, `connectionstring`, `instrumentationkey`, `credential`.

6. **`git diff --check`**: En `pull_request` compara `base.sha` vs `head.sha`. En `workflow_dispatch` compara `HEAD^` vs `HEAD`.

#### Resultado de validación local

- **actionlint**: 0 errores estructurales en los 7 workflows
- **Bicep build**: `infra/main.bicep` + 3 `bicepparam` → sin errores ni warnings
- **Build .NET**: `dotnet build --configuration Release` → correcto
- **Pruebas**: 317/317 correctas
- **Paquetes NuGet**: 0 vulnerabilidades
- **Archivos modificados**: solo `.github/workflows/infra-validation.yml`, `AGENTS.md`, `docs/BACKEND_AUDIT.md`, `infra/README.md`
- **Bicep no modificado**: ningún `.bicep` ni `.bicepparam` fue alterado
- **Código C#**: no modificado
- **appsettings**: no modificados

## Historial de versiones del documento

| Versión | Fecha | Cambios |
|---|---|---|
| 1.0 | — | Creación inicial del documento con auditoría de línea base |
| 1.1 | — | Ronda 1: health checks, Swagger UI, 315 pruebas |
| 1.2 | — | Ronda 2: `.gitattributes`, OpenAPI global, 317 pruebas |
| 1.3 | — | Ronda 3: Pipeline Foundation — sin Docker, smoke test con dotnet publish y ejecución directa de `ImpactX.Api.dll`, 4 endpoints, 317 pruebas |
| 1.4 | — | Ronda 4: Secondary Workflows Foundation — `include-prerelease` eliminado, timeouts, `workflow_dispatch`, permisos mínimos, actionlint 0 errores |
| 1.5 | — | Ronda 4 (continuación): Roslyn estricto — `|| echo` eliminado, `fetch-depth: 0`, detección de archivos C# modificados por evento, `dotnet format --include` solo sobre cambios, sin ocultar fallos. CodeQL timeout ya configurado (15 min) |
| 1.6 | — | Ronda 5: Azure IaC Foundation — Bicep `targetScope='subscription'`, módulos monitoring + app-service, 3 ambientes, naming determinista, sin secretos, sin deployment. 317 pruebas, NuGet sin vulnerabilidades |
| 1.7 | — | Ronda 5.1 (endurecimiento): `XDT_MicrosoftApplicationInsights_Mode=recommended` agregado. README corregido — se eliminó la referencia a un supuesto comando de importación directa de despliegues y la referencia incorrecta a una consulta individual de recursos como entrada directa de decompilación, se documentó `az group export` + `az bicep decompile`, "Bicep: Insert Resource" y palabra clave `existing`. Sin cambios en workflows, C# ni appsettings |
| 1.8 | — | Ronda 6: Azure IaC Validation. Workflow `infra-validation.yml` creado. Bicep 0.45.15 fijado con SHA-256. Sin Azure login, sin OIDC, sin despliegue. 6 validaciones: compilación estricta, placeholder, ARM JSON, secretos prohibidos, outputs, git diff. 7 workflows. 317 pruebas. actionlint 0 errores |
| 1.9 | — | Ronda 8: Backend Functional Readiness y Security Regression. POST `/api/auth/refresh` con rotación de refresh token. PUT y DELETE `/api/users/me/fcm-token` (un token por usuario). Pruebas unitarias e integración. Pruebas marcadas `Category=Security`. Pipeline `dotnet-ci.yml` fortalecido con paso `Run security regression tests`, validación TRX con Python y artifact `security-regression-results`. 7 workflows intactos. Documentación DevSecOps. Firebase no conectado a AlertService. Secreto JWT pendiente. Recuperación de contraseña pendiente. Sin cambios en Azure, Cosmos DB real, appsettings, Bicep ni otros workflows. |

## Ronda 8 — Backend Functional Readiness y Security Regression

### Endpoints agregados

#### POST /api/auth/refresh

Renovación de sesión mediante refresh token con rotación.

- **Request body**: `{ "refreshToken": "string" }` (obligatorio)
- **200**: `AuthResponse` con nuevo `Token`, nuevo `RefreshToken`, datos del usuario
- **400**: modelo inválido (refresh token vacío)
- **401**: genérico — no distingue entre token inexistente, expirado, revocado, usuario inexistente o inactivo
- **500**: solo mediante middleware global
- **No requiere** access token válido
- **Rotación**: el refresh token anterior se revoca (`RevokedAt`) y se genera uno nuevo

#### PUT /api/users/me/fcm-token

Registra el token FCM del usuario autenticado.

- **Request body**: `{ "token": "string" }` (obligatorio, máximo 1000 caracteres)
- **Requiere autenticación**: `[Authorize]`
- **204**: sin body — el token no se devuelve en la respuesta
- **400**: token vacío o mayor a 1000 caracteres
- **401**: sin JWT
- **404**: usuario autenticado no existe
- No acepta `IdUsuario` del request — usa exclusivamente claims
- No valida contra Firebase
- No realiza llamadas de red
- Se almacena en `Usuario.FcmToken` (nullable, `HasMaxLength(1000)`)

#### DELETE /api/users/me/fcm-token

Elimina el token FCM del usuario autenticado.

- **Requiere autenticación**: `[Authorize]`
- **204**: sin body — token eliminado
- **401**: sin JWT
- **404**: usuario autenticado no existe
- **Idempotente**: segundo DELETE también retorna 204

### Persistencia

- `Usuario.FcmToken` ya existía como `string?` con `HasMaxLength(1000)` en `ApplicationDbContext`
- No requiere migración
- Cosmos puede leer documentos antiguos sin `fcmToken` (propiedad nullable)
- `UpdateAsync` conserva todos los demás datos del usuario
- **Deuda técnica**: `CosmosUsuarioRepository.AddAsync` no fue corregido en este PR

### Pruebas

- **Unitarias**: 10 nuevas para refresh token (válido, nulo, vacío, inexistente, expirado, revocado, usuario inexistente, inactivo, generación de token, validación fallida)
- **Unitarias**: 5 nuevas para FCM token (válido, usuario inexistente, elimina token, usuario inexistente en delete, idempotente)
- **Integración**: 6 nuevas para refresh token (200, 400, 401 inválido, 401 expirado, 401 genérica sin info interna)
- **Integración**: 7 nuevas para FCM (204 auth, 400 vacío, 401 sin JWT, token no en response, DELETE 204, DELETE 401, DELETE idempotente)
- Todas las pruebas nuevas están marcadas con `[Trait("Category", "Security")]`
- Categoría `Category=Security` ahora tiene un conjunto real y no vacío de pruebas

### Pipeline

- `dotnet-ci.yml` fortalecido con paso `Run security regression tests`
- Ejecuta `dotnet test --filter "Category=Security"` después de las pruebas completas
- Valida TRX con Python: falla si total = 0 o si failed > 0
- Publica artifact `security-regression-results` (14 días, `if-no-files-found: error`)
- Conserva intactos: triggers, permissions, concurrency, build-and-test, smoke-test, NuGet audit, cobertura, artifacts existentes
- No usa `|| true`, `|| echo`, `continue-on-error` ni `eval`
- No contiene secretos ni llamadas externas

### Actionlint

- 7 workflows continúan siendo válidos estructuralmente

### DevSecOps

- Matriz de controles documentada en `docs/DEVSECOPS_EVIDENCE.md`
- Incluye: Build Release, pruebas, smoke test, cobertura, security regression, CodeQL, Gitleaks, OWASP, NuGet audit, Roslyn, Bicep, OIDC
- Flujo DevSecOps del PR documentado
- Limitaciones honestas documentadas

### No se modificó

- Program.cs
- NotificationService
- AlertService
- appsettings
- Bicep
- Workflows distintos de `dotnet-ci.yml`
- Azure
- Cosmos DB real

## Ronda 9 — Backend Security Hardening

### Cambios realizados

| Archivo | Acción |
|---|---|
| `ImpactXv1/Infrastructure/Security/JwtSecurityConfiguration.cs` | ✅ Creado — centraliza obtención y validación del JWT secret |
| `ImpactXv1/Models/DTOs/RecoverPasswordResponse.cs` | ✅ Creado — DTO seguro sin tokens ni datos sensibles |
| `scripts/security/check_hardcoded_secrets.py` | ✅ Creado — política contra secretos hardcodeados |
| `ImpactXv1/Extensions/ServiceCollectionExtensions.cs` | ✅ Modificado — usa `JwtSecurityConfiguration.GetSigningKey()`, sin fallback |
| `ImpactXv1/Infrastructure/Security/JwtTokenService.cs` | ✅ Modificado — usa `JwtSecurityConfiguration.GetRequiredSecret()`, sin fallback |
| `ImpactXv1/Infrastructure/Security/StubEmailService.cs` | ✅ Modificado — no registra el token, solo destinatario enmascarado |
| `ImpactXv1/appsettings.json` | ✅ Modificado — JWT Secret/SecretKey eliminados |
| `ImpactXv1/appsettings.Development.json` | ✅ Modificado — JWT Secret/SecretKey eliminados |
| `ImpactXv1/appsettings.Production.json` | ✅ Modificado — JWT Secret eliminado |
| `ImpactXv1/Services/IAuthService.cs` | ✅ Modificado — `RecoverPasswordAsync` retorna `RecoverPasswordResponse` |
| `ImpactXv1/Services/AuthService.cs` | ✅ Modificado — recover genérico, revocación en reset/change/delete |
| `ImpactXv1/Core/Interfaces/Repositories/IRefreshTokenRepository.cs` | ✅ Modificado — nuevo método `RevokeAllByUsuarioIdAsync` |
| `ImpactXv1/Infrastructure/Data/Repositories/EF/RefreshTokenRepository.cs` | ✅ Modificado — implementa `RevokeAllByUsuarioIdAsync` |
| `ImpactXv1/Infrastructure/Data/Repositories/Cosmos/CosmosRefreshTokenRepository.cs` | ✅ Modificado — implementa `RevokeAllByUsuarioIdAsync` |
| `ImpactX.Tests/Unit/AuthServiceTests.cs` | ✅ Modificado — pruebas de seguridad para recover, reset, change, delete |
| `ImpactX.Tests/Unit/JwtSecurityConfigurationTests.cs` | ✅ Creado — 10 pruebas de validación JWT |
| `ImpactX.Tests/Integration/AuthControllerTests.cs` | ✅ Modificado — pruebas de seguridad integración |
| `.github/workflows/dotnet-ci.yml` | ✅ Modificado — check_hardcoded_secrets + artifact + JWT efímero |
| `.github/workflows/secret-scanning.yml` | ✅ Modificado — bloqueante, policy check, artifact |
| `AGENTS.md` | ✅ Modificado — JWT externalizado, no hardcoded secrets |
| `docs/BACKEND_AUDIT.md` | ✅ Modificado — documentación de Ronda 9 |
| `docs/DEVSECOPS_EVIDENCE.md` | ✅ Modificado — matriz actualizada, secreto JWT ya no es deuda |

### Diseño final de JWT

- `JwtSecurityConfiguration.GetRequiredSecret(IConfiguration)` es la única fuente
- Lee únicamente `Jwt:Secret` (no `Jwt:SecretKey`)
- Sin fallback literal
- Rechaza null, vacío, whitespace
- Exige al menos 32 bytes UTF-8
- Lanza `InvalidOperationException` sin incluir el valor
- Tanto `ConfigureJwtAuthentication` como `JwtTokenService` la usan
- `GetSigningKey(IConfiguration)` devuelve `SymmetricSecurityKey` listo para usar

### Variable externa esperada

- `Jwt__Secret` en Azure (`Jwt:Secret` en .NET)
- Mínimo 32 bytes UTF-8
- Configurable localmente mediante `dotnet user-secrets set "Jwt:Secret" "..."` o variable de entorno

### Validación fail-fast

- `InvalidOperationException` si no está configurado
- `InvalidOperationException` si es menor a 32 bytes
- Sin fallback a valores predeterminados
- Sin `SecretKey` como alternativa

### Contrato final recover-password

- `POST /api/auth/recover-password` → `RecoverPasswordResponse`
- Respuesta idéntica exista o no el usuario
- Sin `ResetToken`, `Token`, `RefreshToken`, `Usuario` en la respuesta
- Sin revelar existencia de cuenta
- Sin token en logs del StubEmailService
- IEmailService se invoca solo si el usuario existe

### Revocación implementada

- `IRefreshTokenRepository.RevokeAllByUsuarioIdAsync(Guid, DateTime, CancellationToken)`
- Implementada en `RefreshTokenRepository` (EF) y `CosmosRefreshTokenRepository`
- Se ejecuta después de:
  - `ChangePasswordAsync` exitoso
  - `ResetPasswordAsync` exitoso
  - `DeleteAccountAsync` exitoso
- Idempotente: tokens ya revocados permanecen revocados
- No modifica tokens de otros usuarios
- Logout individual y rotación de refresh token conservados

### Pruebas agregadas

**Unitarias — JwtSecurityConfiguration (10):**
- Configuración válida devuelve el secreto
- Configuración ausente lanza InvalidOperationException
- Configuración vacía lanza InvalidOperationException
- Whitespace lanza InvalidOperationException
- Secreto menor a 32 bytes lanza InvalidOperationException
- Excepción no contiene el secreto
- GetSigningKey válido devuelve SymmetricSecurityKey
- Secreto de 31 bytes rechazado
- Secreto de 32 bytes aceptado

**Unitarias — AuthService (9 nuevas marcadas Security):**
- RecoverPasswordAsync con email existente devuelve respuesta genérica
- RecoverPasswordAsync con email inexistente devuelve misma respuesta
- RecoverPasswordResponse no contiene ResetToken
- RecoverPasswordAsync no revela existencia de usuario
- ResetPasswordAsync con token usado es rechazado
- ResetPasswordAsync token no puede reutilizarse
- ResetPasswordAsync revoca todas las sesiones
- ChangePasswordAsync revoca todas las sesiones
- ChangePasswordAsync no afecta otro usuario
- DeleteAccountAsync revoca todas las sesiones
- DeleteAccountAsync segunda revocación no falla

**Integración (nuevas marcadas Security):**
- RecoverPassword response no contiene datos sensibles
- RecoverPassword misma respuesta para email existente e inexistente
- ResetPassword token no devuelto en HTTP
- ChangePassword no devuelve tokens
- DeleteAccount revoca sesiones (refresh rechazado)

### Resultados de verificación

- Total completo de pruebas: se obtiene de ejecución real
- Total Category=Security: se obtiene de ejecución real
- Scanner de secretos: `python3 scripts/security/check_hardcoded_secrets.py` — sin hallazgos

### Cambios en dotnet-ci.yml

- Nuevo paso `Check hardcoded secrets policy` antes de restore
- Nuevo artifact `hardcoded-secrets-policy-report` (14 días, `if-no-files-found: error`)
- Smoke test genera `Jwt__Secret` efímero con `python3 -c "import secrets; print(secrets.token_hex(32))"`
- Sin valores JWT literales en el workflow

### Cambios en secret-scanning.yml

- Eliminado `continue-on-error: true` de Gitleaks
- Agregado paso `ImpactX hardcoded secrets policy`
- Agregado artifact `hardcoded-secrets-policy-report`
- Sin permisos de escritura adicionales

### Artifacts configurados

- `hardcoded-secrets-policy-report` — reporte de texto (14 días)
- `security-regression-results` — TRX + cobertura de pruebas Security
- `test-results` — TRX completo (7 días)
- `coverage-results` — cobertura (7 días)
- `api-smoke-log` — log de arranque (7 días)

### Deuda técnica pendiente

- Reset token en texto plano almacenado sin hash. Mejora: aplicar hash SHA-256 al token antes de persistir. El token de extremo a extremo (URL seguro, tiempo limitado, un solo uso) ya está implementado.
- Rate limiting no implementado en endpoints públicos (login, recover-password)
- Email real no implementado (StubEmailService)
- CORS permisivo (`SetIsOriginAllowed(_ => true)`) pendiente de revisión
- Múltiples dispositivos FCM no soportados

### No se modificó

- Program.cs (salvo que el uso de JwtSecurityConfiguration no requirió cambios en Program.cs)
- NotificationService
- AlertService
- Bicep
- CodeQL
- OWASP
- Roslyn
- Azure deploy
- Cosmos DB real
- Firebase

### Ronda 10 — Alert and Monitor Notification Integration (pendiente de PR)

| # | Requisito | Verificación |
|---|---|---|
| 1 | `IPushNotificationGateway` mockeable | `Core/Interfaces/Services/IPushNotificationGateway.cs` creado. `FirebasePushNotificationGateway` en infraestructura. DI registrado como Scoped. |
| 2 | `PushGatewayResult` tipado | `sealed record PushGatewayResult(bool Success, string Status, string? ExternalMessageId)`. Estados: Enviado, FirebaseNoConfigurado, Fallido. |
| 3 | `NotificationDispatchResult` | `sealed record NotificationDispatchResult(Guid NotificationId, Guid RecipientUserId, string Status, bool Sent)`. Sin FcmToken, sin excepciones internas. |
| 4 | Notificacion extendida | Nuevos campos: `AlertaId`, `Canal="Push"`, `EstadoEnvio`, `Intentos`, `UltimoIntentoEn`, `EnviadoEn`, `ClaveIdempotencia`. EF configurado. |
| 5 | Idempotencia | Clave: `alert:{AlertaId}:recipient:{UsuarioId}:channel:push`. `GetByIdempotencyKeyAsync` en repositorios EF y Cosmos. |
| 6 | AlertService → INotificationService | Solo cuando `alerta.Estado == "Enviada"`. Fallos de notificación no eliminan la alerta ni causan 500. DetectAsync, SendSosAsync, BypassCriticalAsync, RetryAsync, SyncOfflineAsync protegidos. ConfirmOkAsync y CloseAsync nunca notifican. |
| 7 | NotificationService resuelve monitores | Usa `IMonitorRepository.GetActiveByUserAsync`, `ProfileId` para vincular Usuario. Monitores sin ProfileId → DestinatarioNoVinculado. Usuario inactivo → no push. Sin FcmToken → SinToken. |
| 8 | Gateway mockeable | Firebase detrás de `IPushNotificationGateway`. No se llama Firebase en pruebas. |
| 9 | Premium 6 monitores | Límite cambiado de 5 a 6. `planName.ToLowerInvariant()` para case-insensitive. |
| 10 | Auth en invitaciones | `[AllowAnonymous]` eliminado de Accept/Reject. Requieren JWT. 401 sin autenticación. |
| 11 | Token no expuesto en URL | Rutas cambiadas a `POST /api/monitors/invite/{details,accept,reject}` con token en JSON body. Sin `{token}` en rutas. No se registra en logs, rutas ni scopes. |
| 12 | 415 pruebas totales | 104 Category=Security. 0 fallos. |
| 13 | Seguridad de datos | FcmToken no se registra en logs HTTP ni excepciones. Payload push limitado a `alertId`, `alertType`, `severity`, `createdAt`. Sin datos médicos en payload. Tokens de invitación no se registran en logs. |

### Deuda técnica pendiente

- Temporizador de 10 segundos para detección automática
- Clasificación definitiva leve/grave/fatal de impacto
- Machine Learning para clasificación de severidad
- Reset token almacenado en texto plano en `PasswordResetToken`
- Rate limiting (login/register)
- Correo real (`StubEmailService`)
- CORS restrictivo
- Múltiples tokens FCM por usuario
- Notificaciones por correo/SMS
- Transacción alerta/incidente (no atómico)
- OpenTelemetry / tracing distribuido
- Problem Details (RFC 7807)
