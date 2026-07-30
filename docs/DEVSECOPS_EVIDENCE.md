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
- **Firebase aún no está conectado a AlertService**. El token FCM se almacena pero no se utiliza para enviar alertas reales.
- **Rate limiting pendiente**. No hay límite de tasa configurado en la API.
- **Reset token en texto plano**. El token de recuperación se almacena sin hash. Mejora pendiente.
- **No hay pruebas DAST reales**. El escaneo OWASP actual es un escaneo de API estático, no un DAST autenticado contra un entorno desplegado.
- **El escaneo de secretos Gitleaks complementa pero no sustituye controles en Azure** como Key Vault o Managed Identity para secretos de producción.
- **JWT secret externalizado pero gestionado por configuración/env vars**, no por Azure Key Vault ni Managed Identity. La generación de secretos efímeros en CI mitaga exposición, pero sigue dependiendo de variables de entorno en producción.
