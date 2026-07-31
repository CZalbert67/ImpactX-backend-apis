# DevSecOps Evidence — ImpactX Backend APIs

## Matriz de controles DevSecOps

| # | Control | Workflow o prueba | Trigger | Qué valida | Evidencia generada |
|---|---|---|---|---|---|
| 1 | Build Release | `dotnet-ci.yml` (build-and-test) | push (main, leo-desarrollo, feat/**), PR a main, workflow_dispatch | Compilación Release sin errores | Log de build en GitHub Actions |
| 2 | Pruebas completas | `dotnet-ci.yml` (build-and-test) | Ídem | 367 pruebas funcionales + integración pasan (52 Security) | TRX artifact (`test-results`) |
| 3 | Smoke test | `dotnet-ci.yml` (smoke-test) | Ídem (después de build-and-test) | API responde `/health`, `/health/live`, `/health/ready`, `/openapi/v1.json` en entorno CI | Log de smoke test + API log artifact |
| 4 | Cobertura | `dotnet-ci.yml` (build-and-test) | Ídem | Cobertura de código XPlat Code Coverage | Cobertura XML artifact (`coverage-results`) |
| 5 | Security regression tests | `dotnet-ci.yml` (build-and-test) | Ídem (después de pruebas completas) | Pruebas marcadas `Category=Security` (refresh token, FCM, auth, autorización, 401) | TRX + cobertura en `security-regression-results` |
| 6 | CodeQL SAST | `codeql-analysis.yml` | push + PR a main, leo-desarrollo, workflow_dispatch | Análisis estático C# con security-extended query suite | Code Scanning alerts en GitHub Security tab |
| 7 | Gitleaks secret scanning | `secret-scanning.yml` | push + PR a main, leo-desarrollo, workflow_dispatch | Credenciales y secrets en el código | Log de Gitleaks en Actions |
| 8 | OWASP API security audit | `api-security-audit.yml` | push + PR a main, leo-desarrollo, workflow_dispatch | Escaneo de seguridad OWASP sobre API | Log de auditoría OWASP en Actions |
| 9 | NuGet audit | `dotnet-ci.yml` (build-and-test, Restore and Audit) | Ídem | Vulnerabilidades en dependencias directas y transitivas (NU1903, NU1904 como error) | Log de `dotnet package list --vulnerable --include-transitive` |
| 10 | Roslyn code format | `code-quality-roslyn.yml` | push + PR a main, leo-desarrollo, workflow_dispatch | Formato estricto solo sobre C# modificados | Log de `dotnet format` |
| 11 | Bicep validation | `infra-validation.yml` | PR a main (solo cambios en infra/), workflow_dispatch | Compilación estricta Bicep + placeholders + sin secretos | Log de `bicep build` |
| 12 | OIDC para Azure | `main_impactx-api-backend.yml` | push a main, workflow_dispatch | Despliegue a Azure Web App con OIDC (id-token: write) | Log de deploy en Actions |
| 13 | JWT secret externalizado | `JwtSecurityConfiguration.GetRequiredSecret()` | Compilación | Sin fallback, 32 bytes mínimo, fail-fast si no configurado | InvalidOperationException en startup |
| 14 | Hardcoded secrets policy | `check_hardcoded_secrets.py` en `dotnet-ci.yml` + `secret-scanning.yml` | push + PR a main, leo-desarrollo, feat/**, workflow_dispatch | Secretos JWT, private keys, client_secret, Firebase en tracked files | `hardcoded-secrets-policy-report` artifact |
| 15 | Gitleaks bloqueante | `secret-scanning.yml` | push + PR a main, leo-desarrollo, workflow_dispatch | Sin continue-on-error; falla en hallazgos | Log de Gitleaks en Actions |
| 16 | Recuperación de contraseña segura | `AuthService.RecoverPasswordAsync` | Pruebas Security | Respuesta genérica indistinta, sin token en HTTP ni logs | Pruebas automatizadas |
| 17 | Revocación de sesiones | `IRefreshTokenRepository.RevokeAllByUsuarioIdAsync` | Pruebas Security | Reset/change/delete password revocan todas las sesiones | Pruebas automatizadas |

## Flujo DevSecOps del Pull Request

```
Código
  → Restore y auditoría NuGet (dependencias seguras)
  → Build Release (compilación)
  → Tests completos (funcionalidad)
  → Security regression tests (autenticación, autorización, refresh, FCM)
  → Cobertura (calidad)
  → SAST CodeQL (análisis estático)
  → Secret scanning Gitleaks (credenciales)
  → OWASP API audit (seguridad de API)
  → Roslyn format (calidad de código)
  → Revisión humana (aprobación)
  → Merge a main
  → Despliegue OIDC a Azure (solo main)
```

Cada pull request ejecuta los primeros 9 pasos de forma automatizada.
La revisión humana y el merge son pasos previos al despliegue.

## Evidencias esperadas en GitHub

Por cada ejecución del pipeline en un PR:

- **Checks verdes**: todos los jobs de `dotnet-ci.yml` (build-and-test, smoke-test) deben pasar
- **Artifact TRX**: `test-results` con resultados completos de pruebas
- **Artifact cobertura**: `coverage-results` con coverage.cobertura.xml
- **Artifact security regression**: `security-regression-results` con TRX filtrado + cobertura
- **Code Scanning alerts**: resultados de CodeQL en la pestaña Security
- **Logs de secret scanning**: salida de Gitleaks en Actions
- **Logs de OWASP audit**: resultados del escaneo OWASP en Actions

### Ronda 9 — Backend Security Hardening

| # | Control | Workflow o prueba | Qué valida |
|---|---|---|---|
| 13 | JWT secret externalizado | `JwtSecurityConfiguration.GetRequiredSecret()` | ✅ Sin fallback, 32 bytes mínimo, fail-fast (10 tests Security) |
| 14 | Hardcoded secrets policy | `check_hardcoded_secrets.py` en CI + secret-scanning | ✅ 8 patrones, 17 tests Python, 0 violaciones en repo |
| 15 | Secreto JWT efímero en CI | `smoke-test` en `dotnet-ci.yml` | ✅ Generado por `secrets.token_hex(32)` por ejecución |
| 16 | Recuperación de contraseña segura | `AuthService.RecoverPasswordAsync` | ✅ Respuesta genérica, sin token en HTTP ni logs (3 tests Security) |
| 17 | Revocación de sesiones | `IRefreshTokenRepository.RevokeAllByUsuarioIdAsync` | ✅ Reset/change/delete revocan todas las sesiones (5 tests Security) |
| 18 | Gitleaks bloqueante | `secret-scanning.yml` | ✅ `continue-on-error: true` eliminado |
| 19 | Scanner tests automatizados | `test_check_hardcoded_secrets.py` + CI | ✅ 17 tests sobre temp git repos |
| 20 | Hardcoded-secrets-policy-report artifact | `dotnet-ci.yml` + `secret-scanning.yml` | ✅ with `if-no-files-found: error`, 14 días retención |

## Flujo DevSecOps del Pull Request (actualizado)

```
Código
  → Hardcoded secrets policy (secretos en repositorio)
  → Restore y auditoría NuGet (dependencias seguras)
  → Build Release (compilación)
  → Tests completos (funcionalidad)
  → Security regression tests (autenticación, autorización, refresh, FCM, revocación)
  → Cobertura (calidad)
  → SAST CodeQL (análisis estático)
  → Secret scanning Gitleaks (credenciales) — bloqueante
  → OWASP API audit (seguridad de API)
  → Roslyn format (calidad de código)
  → Revisión humana (aprobación)
  → Merge a main
  → Despliegue OIDC a Azure (solo main)
```

## Evidencias esperadas en GitHub (actualizado)

- **Artifact `hardcoded-secrets-policy-report`**: reporte del scanner de secretos hardcodeados

## Limitaciones honestas

- **No existe pentesting real**. No se ha contratado una prueba de penetración externa.
- **No existe DAST autenticado contra producción**. No hay escaneo dinámico automatizado contra el entorno productivo.
- **No existe rotación automática de todos los secretos**. La rotación de refresh tokens ocurre en la aplicación, pero no hay rotación automatizada de secrets de infraestructura.
- **Firebase conectado a AlertService a través de INotificationService y gateway mockeable**. Las alertas en estado "Enviada" disparan notificaciones push a monitores activos. Solo se probaron mocks; no hay envíos reales a Firebase en las pruebas. Firebase real requiere credenciales configuradas en producción.
- **Premium ahora permite 6 monitores activos** (desde 5).
- **Invitaciones protegidas**: AcceptInvitation y RejectInvitation requieren JWT (401 sin autenticación).
- **Rate limiting pendiente**. No hay límite de tasa configurado en la API.
- **Reset token en texto plano**. El token de recuperación se almacena sin hash. Mejora pendiente.
- **No hay pruebas DAST reales**. El escaneo OWASP actual es un escaneo de API estático, no un DAST autenticado contra un entorno desplegado.
- **El escaneo de secretos Gitleaks complementa pero no sustituye controles en Azure** como Key Vault o Managed Identity para secretos de producción.
- **JWT secret externalizado pero gestionado por configuración/env vars**, no por Azure Key Vault ni Managed Identity. La generación de secretos efímeros en CI mitaga exposición, pero sigue dependiendo de variables de entorno en producción.

### Ronda 10 — Alert and Monitor Notification Integration (pendiente de PR)

| # | Control | Prueba/Workflow | Qué valida |
|---|---|---|---|
| 1 | Gateway mockeable | Pruebas unitarias NotificationService | PushGatewayResult tipado, sin Firebase real |
| 2 | AlertService no revierte | Pruebas unitarias AlertService | Fallo de NotificationService no elimina alerta |
| 3 | Resolución de monitores | Pruebas unitarias NotificationService | Solo activos, ProfileId como vínculo |
| 4 | Historial con idempotencia | Pruebas unitarias | DuplicadoOmitido para envíos repetidos |
| 5 | Premium 6 monitores | Pruebas unitarias MonitorService | 6to aceptado, 7mo rechazado, case-insensitive |
| 6 | Auth en invitaciones | Pruebas integración MonitorsController | 401 sin JWT en Accept/Reject |
| 7 | Seguridad payload | Pruebas unitarias | Solo alertId, alertType, severity, createdAt |
| 8 | 415 pruebas (104 Security) | `dotnet test` local | 0 fallos, 0 vulnerabilidades NuGet, 0 secretos |
| 9 | Actionlint | `actionlint .github/workflows/*.yml` | Sin errores en workflows |
| 10 | git diff --check | `git diff --check` | Sin errores de whitespace |
| 11 | No se modificaron archivos prohibidos | `git diff --name-only` | Azure, Bicep, CodeQL, OWASP, Roslyn, Azure Deploy, JWT no tocados |

### PR 1A — API Contracts V1 (completado 2026-07-30)

| # | Control | Prueba/Workflow | Qué valida |
|---|---|---|---|
| 1 | Problem Details RFC 7807 | `ProblemDetailsContractTests` (18 tests), `ProblemDetailsError500Tests` (2 tests) | Status, type, title, detail, instance, traceId, correlationId. Sin stacktrace. 401/403/404/409/500. Mapeo correcto. InvalidOperationException → 500. |
| 2 | CORS configurable | `ApiContractV1CorsTests` (4 tests) | Origen permitido retorna ACAO. Origen atacante no retorna ACAO. Preflight funciona. Producción vacío cierra CORS. Sin AllowAnyOrigin. |
| 3 | Rate limiting por configuración | `RateLimitingContractTests` (5 tests) | 2do request supera límite → 429. Content-Type problem+json. Retry-After. Health exento. Sin partitionKey. Fábricas aisladas por test. |
| 4 | Auth V1 minúsculas | `AllOpenApiV1Paths_AreLowercase`, `OpenApiAuthPaths_AreLowercase` | Todas las rutas V1 sin mayúsculas en segmentos. |
| 5 | Duplicado V1 → 409 ProblemDetails | `V1Auth_DuplicateRegister_Returns409ProblemDetails`, `LegacyAuth_DuplicateRegister_ReturnsConflictObject`, `Conflict_Returns409` | V1: 409 problem+json. Legacy: 409 ConflictObjectResult. |
| 6 | OpenAPI Bearer por operación | `OpenApi_BearerSecurity_OnProtectedOperations`, `OpenApi_AnonymousOperations_DoNotRequireBearer` | Profile/monitors/trips/active requieren Bearer. Register/login/recover-password/invite/details no. |
| 7 | Invitaciones token en JSON body | Revisión manual + `OpenApiDocument_DoesNotContainLegacyPaths` | Sin `{token}` en rutas. Sin query string. POST body. |
| 8 | Error 500 real (mock) | `InternalServerError_Real500_ReturnsProblemDetails` | ITokenService mock lanza InvalidOperationException → 500 problem+json. Sin stacktrace, sin mensaje interno. |
| 9 | OpenAPI 409 documentado | `OpenApi_RegisterRoute_Includes409`, `OpenApi_ConflictResponse_UsesProblemDetails`, `OpenApi_RouteWithoutConflict_DoesNotInclude409` | 23 endpoints V1 con `[ProducesResponseType(409)]`. 409 usa `application/problem+json` con `$ref ProblemDetails`. Operación sin conflicto no documenta 409. |
| 10 | Security regression | `Category=Security` (115 tests) | 115 pruebas de seguridad. 0 fallos. |
| 11 | Contrato V1 | `dotnet test` con filtro (75 tests) | 75 pruebas de contrato. 0 fallos. |
| 12 | Secret scanner | `check_hardcoded_secrets.py` | 234 archivos, 0 violaciones. |
| 13 | NuGet audit | `dotnet list package --vulnerable` | 0 vulnerables. |
| 14 | Actionlint | `actionlint .github/workflows/*.yml` | 7 workflows, 0 errores. |
| 15 | git diff --check | `git diff --check` | Solo advertencias LF/CRLF. Sin errores. |
| 16 | Python tests | `python3 -m unittest discover scripts/security/tests` | 19 tests, 0 fallos. |
| 17 | **490 pruebas totales** | `dotnet test ImpactX.slnx --configuration Release` | 0 fallos. Sin commits. |

### R0.2.1 — Baseline CodeQL (pendiente de PR, rama fix/backend-codeql-security-baseline)

| # | Control | Prueba/Workflow | Qué valida |
|---|---|---|---|
| 1 | Log forging | Revisión + `dotnet test` (490) | `RequestLoggingMiddleware` solo StatusCode/ElapsedMs, sin Path/QueryString/headers/body. `AlertService` solo IDs Guid, sin strings de DTO en logs. |
| 2 | Middleware muerto | `grep -RIn 'ExceptionHandlingMiddleware'` | Clase eliminada; `ProblemDetailsMiddleware` único manejador global (Program.cs). |
| 3 | Catch global intencional | Revisión de `ProblemDetailsMiddleware` | Catch específico `OperationCanceledException` antes del catch global; 500 genérico `problem+json`; traceId/correlationId; sin `Exception.Message`; logging con mensaje constante. Sin suppressions. |
| 4 | Fuera de alcance | Revisión | AuthService, PlanSeeder, CosmosDbContext, IncidentService, LINQ alertas, WearableService, RutaRepository, obj: deuda documentada para PR 1B/1C. |
| 5 | Security regression | `Category=Security` (115) | 115 pruebas de seguridad, 0 fallos. |
| 6 | Suite completa | `dotnet test ImpactX.slnx --configuration Release` | 490 pruebas, 0 fallos. |
| 7 | Secret scanner | `check_hardcoded_secrets.py` | 234 archivos, 0 violaciones. |
| 8 | NuGet audit | `dotnet list package --vulnerable` | 0 vulnerables. |
| 9 | Actionlint | `actionlint .github/workflows/*.yml` | 7 workflows, 0 errores. |
| 10 | Roslyn | `dotnet format --verify-no-changes` | Limpio en C# modificados. |
| 11 | Resultados GitHub pendientes | CodeQL CI | Por ejecutar en el PR. |
