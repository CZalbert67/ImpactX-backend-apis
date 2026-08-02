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
- **Rate limiting pendiente de calibración**. Hay políticas configuradas (13) pero los límites de producción no están calibrados.
- **Reset token con hash (PR 1B)**. El token de recuperación se almacena solo como SHA-256 base64 (`TokenHash`), uso único, expiración UTC 1h, invalidación de tokens previos. El token crudo solo viaja al servicio de correo stub.
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

### R0.3 — Identity and Device Hardening / PR 1B (completado 2026-07-30)

| # | Control | Prueba/Workflow | Qué valida |
|---|---|---|---|
| 1 | Reset token con hash SHA-256 | Unit + integración `AuthServiceTests`/`AuthControllerTests` | BD solo contiene `TokenHash` (44 chars base64), nunca el token en texto plano. Lookup por hash. |
| 2 | Uso único + expiración UTC | Unit `ResetPasswordAsync_*` | Token válido funciona; incorrecto/expirado/reutilizado fallan; segundo recover invalida el primero. |
| 3 | Sin datos sensibles en logs | `RecoverPassword_DoesNotLogCorreoOrToken`, `ResetPassword_DoesNotLogToken`, `RecoverPasswordAsync_DoesNotLogTokenOrCorreo` | Token, hash, correo y contraseña ausentes de `TestLogCapture`/`ListLogger`. |
| 4 | AuthService generic catch | `RegisterAsync_FreePlan*` (3 tests) | Conflict/429 de Cosmos y `DbUpdateException` no rompen el registro; errores inesperados y OCE propagan. Sin `Console.WriteLine`, sin `ex.Message`. |
| 5 | FCM multidispositivo | `DeviceServiceTests` + `NotificationServiceTests` (multidispositivo) + `DevicesControllerTests` (17) | Varios dispositivos por usuario; DeviceId único; **token FCM globalmente único** (`GetByTokenFcmAsync(string)` previo a crear/actualizar: EF global, Cosmos cross-partition solo para esta resolución); token de otro dispositivo del mismo usuario desactiva el anterior; token de otro usuario → 409 con mensaje genérico (sin crear ni actualizar nada); upsert reemplaza token; envío a todos los activos; fallo de un token no bloquea los demás (todos fallidos → "Fallido"); fallback legacy; plataformas normalizadas con Trim (WEAROS→WearOS, " android "→Android); migración legacy limpia `Usuario.FcmToken`. |
| 6 | Autorización horizontal | `DevicesControllerTests` (Category=Security) | A no lista dispositivos de B; A no elimina dispositivo de B (404, sin revelar existencia); token de B rechazado con 409 si lo usa A; usuarioId del body ignorado (JWT manda); endpoints privados requieren Bearer (401). |
| 7 | TokenFcm nunca en respuestas/logs | `DeviceList_NeverContainsFcmToken`, `DeviceOperations_DoNotLogFcmToken`, `DispatchToMonitor_DoesNotLogDeviceTokenValues`, `UpsertFcmTokenAsync_Conflict_DoesNotLogToken` | Ausencia de token en bodies y logs (incluido el flujo de conflicto). `DeviceDto` sin propiedad TokenFcm. |
| 8 | Revocación y migración legacy | Unit + integración | DELETE individual, DELETE todos, y borrado de cuenta revocan todos los dispositivos y limpian `Usuario.FcmToken`. Legacy `PUT/DELETE /api/users/me/fcm-token` intactos. |
| 9 | Security regression | `Category=Security` (144 tests) | 144 pruebas de seguridad, 0 fallos. |
| 10 | Suite completa | `dotnet test ImpactX.slnx --configuration Release` | **558 pruebas**, 0 fallos (antes 557/144). |
| 10b | Contrato OpenAPI 409 fcm-token | `OpenApi_DevicesFcmToken_Includes409ProblemDetails` | `PUT /api/v1/devices/fcm-token` documenta 409 con `application/problem+json` → `$ref ProblemDetails` (único PUT con conflicto real; sin 409 en endpoints sin conflicto). |
| 11 | Secret scanner | `check_hardcoded_secrets.py` | 246 archivos, 0 violaciones. |
| 12 | NuGet audit | `dotnet list package --vulnerable --include-transitive` | 0 vulnerables. |
| 13 | Actionlint | `actionlint .github/workflows/*.yml` | 7 workflows, 0 errores. |
| 14 | Roslyn | `dotnet format --verify-no-changes` | Limpio en 26 C# modificados/nuevos. |
| 15 | git diff --check | `git diff --check` | Sin errores de whitespace. |
| 16 | Resultados GitHub pendientes | Pipelines CI | Por ejecutar en el PR. |

### R0.4 — Readiness, Observability and Production Hardening / PR 1C (completado 2026-07-31)

| # | Control | Prueba/Workflow | Qué valida |
|---|---|---|---|
| 1 | Liveness sin dependencias externas | `Live_Returns200_WithoutCosmosDependency`, `Live_StillReturns200_WhenCriticalDependencyFails` | `/health/live` = 200 solo con proceso vivo + pipeline HTTP; nunca toca Cosmos ni ejecuta seeding; 200 incluso cuando `/health/ready` devuelve 503 por dependencia crítica caída. |
| 2 | Readiness con dependencias críticas | `Ready_Returns200_WithHealthyConfiguration`, `Ready_Returns503_WhenCriticalDependencyFails`, `Health_AggregatesUnhealthy_WhenCriticalDependencyFails`, `ConfigurationReadinessCheckTests` (7), `DatabaseReadinessCheckTests` (9) | `/health/ready` = 200 con configuración sana; **503** cuando una dependencia crítica falla (config inválida, clave placeholder, base inaccesible, inicialización pendiente/fallida, timeout de acceso). Checks baratos y de solo lectura (`Database.ReadAsync`). |
| 3 | Sin secretos ni internos en health | `HealthJson_DoesNotExposeSecretsOrInternals`, `Ready_Returns503_WhenCriticalDependencyFails` (asserts de ausencia) | El JSON de `/health*` nunca contiene claves, connection strings, `Exception.Message`, stack traces ni el placeholder de la key. Description de checks: solo descripciones genéricas seguras. |
| 4 | Arranque no bloqueante | `CosmosInitializationServiceTests` (8) | La inicialización Cosmos corre en BackgroundService fuera de `app.Run()`; `SeedDatabaseAsync` ya no toca Cosmos. Nada bloquea el arranque → sin ciclos de caída/reinicio (incluye WP stop requests del plan F1). |
| 5 | Reintentos limitados y no reintento de errores graves | `TransientErrors_RetryLimited_ThenSucceed`, `TransientErrors_Exhausted_FailsWithoutInfiniteRetry`, `NonTransientError_DoesNotRetry`, `Timeout_RetriedLimited_ThenFails`, `Cancellation_StopsGracefully`, `UnexpectedError_FailsWithoutInfiniteRetry` | Transitorios (408/429/5xx/timeout) reintentan hasta `MaxAttempts`; 401/403/400/404/409 y errores inesperados no se reintentan; nunca reintento infinito; cancelación por shutdown termina con gracia; `FailureDescription` siempre genérica (nunca `ex.Message`). |
| 6 | Correlation ID completo | `CorrelationIdMiddlewareTests` (6), `ObservabilityTests` (integración) | Header válido preservado y devuelto; CR/LF sanitizado (inválido → reemplazado por GUID N); >100 chars limitado; ausente → generado; presente en `HttpContext.Items`, `TraceIdentifier`, logging scope, ProblemDetails (404 y 500) y JSON de health. |
| 7 | Request logging sin fugas | `RequestLogs_ContainCorrelationId`, `RequestLogs_DoNotContainSecretsOrQueryStrings` | Log estructurado con method/path/status/elapsed/correlationId; nunca body, query string, Authorization, cookies ni tokens; `Microsoft.AspNetCore.Hosting=Warning` en Development para que el framework no registre query strings. |
| 8 | 500 sin detalles internos | `UnexpectedException_500_IncludesCorrelationIdWithoutInternals`, `UnexpectedException_500_RequestLog_RecordsStatusAndCorrelationId_WithoutSecrets` (Category=Security) | 500 ProblemDetails con `correlationId` exacto del request y sin mensaje de excepción, tipo de excepción ni stack trace; RequestLoggingMiddleware (fuera de ProblemDetails) registra `completed with status 500` con el correlationId, sin Exception.Message, password, token ni body. |
| 9 | Post-deploy verification | `test_deploy_workflow_contract.py` (11 tests) + actionlint | El workflow consulta `/health/live`, `/health/ready`, `/openapi/v1.json` con `--fail`/`--max-time` tras el deploy; reintentos limitados (12×15s); `exit 1` si no llegan a 200; sin "restart"/"redeploy" automáticos; sin credenciales hardcodeadas (solo `secrets.`); concurrency `deploy-impactx-api-main`; detecta `Site Disabled`/`quota` con warnings de diagnóstico; `vars.APP_BASE_URL` sobrescribe el fallback que coincide exactamente con el host público real. |
| 10 | Security regression | `Category=Security` (146 tests) | 146 pruebas de seguridad, 0 fallos (antes 144). |
| 11 | Suite completa | `dotnet test ImpactX.slnx --configuration Release` | **607 pruebas**, 0 fallos (antes 558/144; +49, +2 Security). 76 contratos V1 conservados. |
| 12 | Secret scanner | `check_hardcoded_secrets.py` | 260 archivos, 0 violaciones. |
| 13 | NuGet audit | `dotnet list package --vulnerable --include-transitive` | 0 vulnerables. |
| 14 | Actionlint | `actionlint .github/workflows/*.yml` | 7 workflows, 0 errores. |
| 15 | Roslyn | `dotnet format --verify-no-changes` | Limpio en 20 C# modificados/nuevos. |
| 16 | git diff --check | `git diff --check` | Sin errores de whitespace. |
| 17 | Resultados GitHub pendientes | Pipelines CI | Por ejecutar en el PR (Azure/Cosmos real no contactados en esta rama; no afirmar validación). |

### R0.5 — Cosmos Data Architecture and Persistence Hardening / PR 2A (completado 2026-07-31)

| # | Control | Prueba/Workflow | Qué valida |
|---|---|---|---|
| 1 | Catálogo central sin duplicados | `CosmosContainerCatalogTests` (12) | 18 contenedores conocidos, nombres únicos/no vacíos, partition key paths válidos (segmento único `/x`), TTL −1 o > 0, **sin throughput dedicado** (definiciones y creación no lo admiten), sin composite indexes arbitrarios, rechazo de definiciones inválidas (nombre vacío, PK inválida, TTL inválido). |
| 2 | Configuración Cosmos validada | `CosmosDatabaseOptionsTests` (9) | Bind de sección `AzureCosmosDb`; defaults (`ImpactX-Data`, 400 RU/s); **SharedThroughput entero positivo ≤ 1000**; endpoint URI absoluto; timeout/reintentos no negativos; key placeholder no bloquea opciones (readiness lo valida). |
| 3 | Esquema: creación y validación segura | `CosmosSchemaInitializationTests` (12) | Base nueva con SharedThroughput=400; contenedores nuevos sin throughput dedicado; **inicialización repetida idempotente**; **mismatch de partition key falla sin borrar ni recrear** (excepción con solo nombre lógico del contenedor); cancelación propagada; fallback sin throughput ante BadRequest; carrera de creación tolerada. |
| 4 | Aislamiento entre particiones | `CosmosPartitionKeysTests` (3, Category=Security) + revisión de los 15 repos | PartitionKey centralizada (`CosmosPartitionKeys.For(Guid)`) serializada de forma consistente; toda consulta ligada a usuario/viaje se acota con `QueryRequestOptions.PartitionKey` — un usuario nunca consulta la partición de otro. |
| 5 | SQL parametrizado | `IncidenteQueryBuilderTests` (3 Category=Security) | El texto SQL de filtrado (severidad, fechas, usuario) solo contiene cláusulas fijas y parámetros `@...`; los valores del usuario nunca se concatenan (OFFSET/LIMIT son enteros del dominio). `rg` de consultas: 0 concatenaciones de input en repos. |
| 6 | Seeding idempotente sin scans | `PlanSeederTests` (9, 2 Category=Security) | Point-reads por IDs determinísticos; **sin `SELECT * FROM c`**; no duplica planes (también contra datos legacy con IDs aleatorios); Conflict tolerado; **401/403/429 propagan**; cancelación propagada. |
| 7 | Fallo seguro de esquema | `SchemaMismatch_FailsInitializationSafely_WithoutSecrets`, `SchemaMismatch_ExceptionMessage_IsSafe` (Category=Security) | Mismatch → inicialización `Failed` con descripción genérica ("Database schema mismatch detected; controlled migration required."), sin endpoint, key ni nombre del contenedor; `SchemaMismatch_ReadinessReflectsFailure` → `/health/ready` Unhealthy. |
| 8 | Operaciones de punto | Repos + rg | `ReadItemAsync` solo con partition key correcta (Usuarios/Planes `/id`, Dispositivos `(usuarioId, id)`); `ReplaceItemAsync` en updates; borrados siempre con PK. |
| 9 | Security regression | `Category=Security` (158 tests) | 158 pruebas de seguridad, 0 fallos (antes 146). |
| 10 | Suite completa | `dotnet test ImpactX.slnx --configuration Release` | **662 pruebas**, 0 fallos (antes 607/146; +55, +12 Security). 76 contratos V1 conservados. |
| 11 | Python | `python3 -m unittest discover -s scripts/security/tests` | 30 tests, 0 fallos. |
| 12 | Secret scanner | `check_hardcoded_secrets.py` | 0 violaciones. |
| 13 | NuGet audit | `dotnet list package --vulnerable --include-transitive` | 0 vulnerables. |
| 14 | Actionlint | `actionlint .github/workflows/*.yml` | 7 workflows, 0 errores. |
| 15 | Roslyn | `dotnet format --verify-no-changes` | Limpio en archivos C# modificados/nuevos. |
| 16 | git diff --check | `git diff --check` | Sin errores de whitespace. |
| 17 | Resultados GitHub pendientes | Pipelines CI | Por ejecutar en el PR (Azure/Cosmos real no contactados en esta rama; no afirmar validación). |

### R0.6 — Pagination and Query Efficiency / PR 2B (completado 2026-08-01)

| # | Control | Prueba/Workflow | Qué valida |
|---|---|---|---|
| 1 | Validación de paginación | `PaginationValidatorTests` (17) | pageSize default 20, rango 1–100, fuera de rango → `BadRequestException` (400); token nulo permitido; token vacío/whitespace, con CR/LF o > 2048 chars → 400; token de longitud máxima permitido. |
| 2 | Token EF opaco | `OffsetContinuationTokenTests` (8) | Round-trip encode/decode (0, 20, 1000); tokens malformados (base64 inválido, `offset:-1`, `offset:` vacío, token de otro formato, vacío) → 400 genérico, sin detalles internos. |
| 3 | Paginación de servicios | `ViajeServiceTests` (4 nuevas) + `IncidentServiceTests` (4 nuevas: bounds Pagina/Tamano) | `GetTripsPagedAsync` mapea página y respeta token; pageSize fuera de rango → 400; `GetTelemetryPagedAsync` con viaje propio devuelve página; **viaje de otro usuario → `ForbiddenException` (IDOR)**; incidentes con `Pagina=0`/`Tamano=0`/`Tamano=101` → 400; `Tamano=100` permitido. |
| 4 | Contrato legacy paginado | `PaginationContractTests` (7) | `GET /api/contacts?pageSize=2` devuelve body `List<T>` (2 ítems) + header `X-Continuation-Token`; segunda página con token devuelve el resto **sin** header en la última página; **JSON raíz array sin campos de paginación**; `pageSize=0`/`pageSize=101` → 400; token inválido/malformado → 400; monitors paginados OK. |
| 5 | Endpoints nuevos paginados | `PaginationContractTests` (2) | `GET /api/trips` devuelve body `PagedResult<T>` (items, continuationToken, hasMoreResults, pageSize); **telemetría de viaje ajeno → 404** (point-read null, sin revelar existencia). |
| 5b | EfPageReader | `EfPageReaderTests` (11) | 0/parcial/llena; página exacta y parcial final **sin token**; `pageSize+1` decide `HasMoreResults`; segunda página avanza offset correcto; 4 tokens malformados → 400. |
| 5c | CosmosPageReader | `CosmosPageReaderTests` (8) | Un único `ReadNextAsync`; `MaxItemCount=pageSize`; `PartitionKey` aplicado; token SDK no descodificado/inalterado; token nulo cuando no hay más; 400 Cosmos → `BadRequestException` genérica (sin token ni activity-id); errores 5xx propagan. |
| 6 | CORS expone headers de paginación | `ApiContractV1Tests.ActualResponse_ExposesPaginationHeaders` (1) | Respuesta real con origen permitido expone `X-Continuation-Token` y `X-Correlation-Id` en `Access-Control-Expose-Headers`. Token fuera de `WithHeaders` (viaja como query param). |
| 6b | Seguridad de paginación | `PaginationContractTests` (6, Category=Security) | CR/LF y token > 2048 chars → 400 sin eco del token; 400 como ProblemDetails sin token en body; header `X-Continuation-Token` sin CR/LF; token de otro usuario no filtra datos ajenos (respuesta vacía); IDOR telemetría → 404 sin revelar propietario. |
| 7 | Security regression | `Category=Security` (164 tests) | 164 pruebas de seguridad, 0 fallos (158 + 6 nuevas de paginación). |
| 8 | Suite completa | `dotnet test ImpactX.slnx --configuration Release` | **731 pruebas**, 0 fallos (antes 705/158; +26). 77 contratos V1. Idéntico en Debug. |
| 9 | Python | `python3 -m unittest discover -s scripts/security/tests` | 30 tests, 0 fallos. |
| 10 | Secret scanner | `check_hardcoded_secrets.py` | 0 violaciones (282 archivos). |
| 11 | NuGet audit | `dotnet list package --vulnerable --include-transitive` | 0 vulnerables. |
| 12 | Roslyn | `dotnet format --verify-no-changes` (full, con include de modificados) | Limpio en C# modificados/nuevos. |
| 13 | git diff --check | `git diff --check` | Sin errores de whitespace. |
| 14 | OpenAPI en vivo | `/openapi/v1.json` (79 paths) | `pageSize` documentado con `minimum: 1, maximum: 100` y descripción; `continuationToken` documentado como token opaco del header; nuevos endpoints trips/telemetry/alerts/wearable-all presentes. |
| 15 | Resultados GitHub pendientes | Pipelines CI | Por ejecutar en el PR (Azure/Cosmos real no contactados en esta rama; no afirmar validación). |
