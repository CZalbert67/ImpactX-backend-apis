# DevSecOps Evidence — ImpactX Backend APIs

## Matriz de controles DevSecOps

| # | Control | Workflow o prueba | Trigger | Qué valida | Evidencia generada |
|---|---|---|---|---|---|
| 1 | Build Release | `dotnet-ci.yml` (build-and-test) | push (main, leo-desarrollo, feat/**), PR a main, workflow_dispatch | Compilación Release sin errores | Log de build en GitHub Actions |
| 2 | Pruebas completas | `dotnet-ci.yml` (build-and-test) | Ídem | 343 pruebas funcionales + integración pasan | TRX artifact (`test-results`) |
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

## Limitaciones honestas

- **No existe pentesting real**. No se ha contratado una prueba de penetración externa.
- **No existe DAST autenticado contra producción**. No hay escaneo dinámico automatizado contra el entorno productivo.
- **No existe rotación automática de todos los secretos**. La rotación de refresh tokens ocurre en la aplicación, pero no hay rotación automatizada de secrets de infraestructura.
- **Firebase aún no está conectado a AlertService**. El token FCM se almacena pero no se utiliza para enviar alertas reales.
- **JWT secret pendiente de externalización**. El secreto JWT tiene un fallback hardcoded en el código. Debe moverse a Azure Key Vault o variables de entorno.
- **Rate limiting pendiente**. No hay límite de tasa configurado en la API.
- **Recuperación de contraseña pendiente de endurecimiento**. El token de recuperación se devuelve en la respuesta; falta endurecimiento adicional.
- **No hay pruebas DAST reales**. El escaneo OWASP actual es un escaneo de API estático, no un DAST autenticado contra un entorno desplegado.
- **El escaneo de secretos Gitleaks complementa pero no sustituye controles en Azure** como Key Vault o Managed Identity para secretos de producción.
