# ImpactX — Azure IaC Foundation

## ⚠️ ESTA PLANTILLA TODAVÍA NO DEBE DESPLEGARSE

Esta es la primera fundación de infraestructura como código con Azure Bicep.
No se ha desplegado ningún recurso. No se han adoptado recursos existentes.
No se han modificado los secretos, workflows ni código de la aplicación.

Antes del primer despliegue deben completarse los pasos de verificación
descritos en la sección [Proceso futuro de what-if y deployment].

## Objetivo

La carpeta `infra/` contiene la definición de infraestructura como código
(IaC) para los entornos de Azure del backend ImpactX, utilizando el lenguaje
Bicep de Microsoft.

El alcance es **subscription** (`targetScope = 'subscription'`), por lo que
`main.bicep` es responsable de crear su propio Resource Group y los recursos
dentro de él. No modifica ni adopta recursos existentes.

## Arquitectura

```
[Subscription]
  └── Resource Group (nuevo, uno por ambiente)
        ├── Log Analytics Workspace
        ├── Application Insights (workspace-based)
        ├── App Service Plan (Linux)
        └── Web App (Linux, System-Assigned Managed Identity)
```

La connection string de Application Insights se pasa internamente del módulo
de monitoreo al módulo de App Service, pero no se expone como output del
archivo principal.

## Estructura de archivos

```
infra/
├── bicepconfig.json              # Configuración del linter de Bicep
├── main.bicep                    # Plantilla principal (alcance: subscription)
├── README.md                     # Este archivo
├── modules/
│   ├── monitoring.bicep          # Log Analytics + Application Insights
│   └── app-service.bicep         # App Service Plan + Web App + Managed Identity
└── environments/
    ├── dev.bicepparam            # Parámetros para desarrollo
    ├── test.bicepparam           # Parámetros para pruebas
    └── prod.bicepparam           # Parámetros para producción
```

## Recursos creados

Por cada ambiente se crean los siguientes recursos:

| Tipo | Recurso | Nombre de ejemplo |
|------|---------|-------------------|
| Resource Group | `Microsoft.Resources/resourceGroups` | `rg-impactx-dev` |
| Log Analytics Workspace | `Microsoft.OperationalInsights/workspaces` | `law-impactx-dev` |
| Application Insights | `Microsoft.Insights/components` | `ai-impactx-dev` |
| App Service Plan (Linux) | `Microsoft.Web/serverfarms` | `asp-impactx-dev-<suffix>` |
| Web App (Linux) | `Microsoft.Web/sites` | `app-impactx-dev-<suffix>` |

El sufijo (`<suffix>`) se genera con `uniqueString(subscription().id, resourceGroupName)` para garantizar nombres globalmente únicos sin colisiones.

### Configuración de la Web App

- HTTPS obligatorio (`httpsOnly: true`)
- `clientAffinityEnabled: false` (sin afinidad de sesión)
- `minTlsVersion: '1.2'`
- `ftpsState: 'Disabled'`
- `http20Enabled: true`
- `healthCheckPath: '/health/ready'`
- `alwaysOn` activado excepto en SKU Free/Shared
- System-Assigned Managed Identity habilitada

### App Settings

| Variable | Valor |
|----------|-------|
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Test` / `Production` según ambiente |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Connection string de Application Insights (heredada del módulo de monitoreo) |
| `ApplicationInsightsAgent_EXTENSION_VERSION` | `~3` |
| `XDT_MicrosoftApplicationInsights_Mode` | `recommended` |
| `WEBSITE_HEALTHCHECK_MAXPINGFAILURES` | `2` |

La Web App Linux queda preparada para la autoinstrumentación administrada
por App Service mediante los tres app settings de Application Insights:
`APPLICATIONINSIGHTS_CONNECTION_STRING`, `ApplicationInsightsAgent_EXTENSION_VERSION=~3`
y `XDT_MicrosoftApplicationInsights_Mode=recommended`. La telemetría no ha
sido comprobada en Azure porque no se ha desplegado ningún recurso.

No se configuran variables de Cosmos DB, JWT, ni UseCosmosDb/UseInMemoryDatabase
en esta fase.

## Recursos excluidos

Los siguientes recursos **no** se incluyen en esta fundación:

- Azure Cosmos DB
- Azure Key Vault
- Azure Machine Learning
- Storage Accounts
- Virtual Networks / Subnets
- Private Endpoints
- Alert rules
- Availability tests
- Diagnostic settings
- Certificates / App Service Managed Certificates
- Slots (deployment slots)
- Autoscale rules
- Backup / Snapshot policies
- Domain / Custom domains
- CDN / Front Door / API Management
- Container Registry
- Azure AD / RBAC role assignments

## Parámetros por ambiente

| Parámetro | Dev | Test | Prod |
|-----------|-----|------|------|
| `environmentName` | `dev` | `test` | `prod` |
| `location` | `eastus2` | `eastus2` | `eastus2` |
| `resourceGroupName` | `rg-impactx-dev` | `rg-impactx-test` | `rg-impactx-prod` |
| `namePrefix` | `impactx` | `impactx` | `impactx` |
| `appServicePlanSkuName` | `F1` | `B1` | `P1v3` |
| `appServicePlanSkuTier` | `Free` | `Basic` | `PremiumV3` |
| `appServicePlanCapacity` | `1` | `1` | `2` |
| `linuxFxVersion` | `DONT_DEPLOY_UNTIL_RUNTIME_IS_VERIFIED` | (ídem) | (ídem) |
| `logAnalyticsRetentionInDays` | `30` | `30` | `365` |

Los valores de región (`location`) y SKU son ejemplos. Deben revisarse por
disponibilidad regional y costo antes del primer despliegue.

### linuxFxVersion

El valor `DONT_DEPLOY_UNTIL_RUNTIME_IS_VERIFIED` es un marcador de posición
que permite validar la compilación de Bicep pero **no debe desplegarse**. Antes
de cualquier `what-if` o `deployment`, debe sustituirse por el runtime real
de .NET, por ejemplo:

```
dotnet|10.0
```

Para verificar la pila disponible:

```bash
az webapp list-runtimes --os linux | grep DOTNET
```

## Outputs de `main.bicep`

| Output | Descripción |
|--------|-------------|
| `environmentName` | Nombre del ambiente (`dev`, `test`, `prod`) |
| `resourceGroupName` | Nombre del Resource Group creado |
| `appServicePlanName` | Nombre del App Service Plan |
| `webAppName` | Nombre de la Web App |
| `webAppUrl` | URL pública de la Web App (`https://<hostname>`) |
| `webAppPrincipalId` | Object ID de la System-Assigned Managed Identity |
| `applicationInsightsName` | Nombre del recurso de Application Insights |
| `logAnalyticsWorkspaceName` | Nombre del Log Analytics Workspace |

No se exponen connection strings, instrumentation keys, ni ningún secreto
como output de `main.bicep`.

## Convenciones de nombres

```
Resource Group:       rg-{namePrefix}-{environmentName}
App Service Plan:     asp-{namePrefix}-{environmentName}-{uniqueSuffix}
Web App:              app-{namePrefix}-{environmentName}-{uniqueSuffix}
Log Analytics:        law-{namePrefix}-{environmentName}
Application Insights: ai-{namePrefix}-{environmentName}
```

El `uniqueSuffix` se calcula con `uniqueString(subscription().id, resourceGroupName)`.
Es determinista dentro de una misma suscripción y resource group.

## Managed Identity

La Web App tiene habilitada una **System-Assigned Managed Identity**. Su Object
ID (`webAppPrincipalId`) se expone como output para que pueda asignársele
acceso a otros recursos (por ejemplo, Key Vault, Cosmos DB) en fases futuras.

## Monitoreo

Application Insights se despliega en modo **workspace-based**, enlazado al
Log Analytics Workspace local. Esto permite almacenar todos los logs y
métricas en un mismo workspace y facilita la consulta unificada.

No se crean alertas, availability tests ni diagnostic settings en esta fase.

## Seguridad

- HTTPS obligatorio en la Web App
- TLS mínimo 1.2
- FTP deshabilitado
- Sin app settings con secretos
- Sin outputs con secretos
- Sin parámetros secretos
- System-Assigned Managed Identity permite acceso sin credenciales estáticas

## Costos y revisión de SKU

Los SKU definidos en los archivos bicepparam son **ejemplos**:

| Ambiente | SKU | Propósito |
|----------|-----|-----------|
| Dev | F1 (Free) | Validación sin costo |
| Test | B1 (Basic) | Pruebas de integración |
| Prod | P1v3 (Premium V3) | Producción con escalabilidad |

Antes de desplegar, revisar:

1. Disponibilidad regional del SKU elegido.
2. Costos estimados en la calculadora de Azure.
3. Capacidad necesaria (instancias y tier).
4. Restricciones del SKU Free/Shared (sin alwaysOn, sin SSL personalizado, etc).

## Validación automática (GitHub Actions)

El workflow `.github/workflows/infra-validation.yml` valida
automáticamente los archivos Bicep en cada Pull Request que modifique la
carpeta `infra/` o el propio workflow.

### Cuándo se ejecuta

- **Pull Requests** hacia `main` que modifiquen archivos en `infra/` o
  `.github/workflows/infra-validation.yml`.
- **Ejecución manual** mediante `workflow_dispatch`.

### Qué NO hace

- No se conecta a Azure.
- No ejecuta `az`.
- No ejecuta `what-if`.
- No despliega recursos.
- No publica ARM JSON en el repositorio ni como artefactos.
- No necesita OIDC, secrets, ni credenciales.

### Validaciones

1. **Compilación Bicep estricta**: `infra/main.bicep` y los 3 archivos
   `bicepparam` (`dev`, `test`, `prod`) se compilan con `bicep build` /
   `bicep build-params`. Falla si hay errores o warnings.
2. **Placeholder**: Verifica que
   `DONT_DEPLOY_UNTIL_RUNTIME_IS_VERIFIED` esté presente exactamente en
   `infra/README.md` y los 3 `bicepparam`, y en ningún otro archivo.
3. **Archivos ARM JSON**: Falla si existe cualquier `*.json` fuera de
   `infra/bicepconfig.json`.
4. **Secretos prohibidos**: Escanea `*.bicep` y `*.bicepparam` en busca de
   patrones de credenciales (`Jwt__Secret`, `AzureCosmosDb__Key`, etc.).
5. **Outputs seguros**: Verifica que ningún nombre de `output` en
   `main.bicep` contenga términos sensibles (`secret`, `key`, `token`,
   `password`, etc.).
6. **Git diff**: Ejecuta `git diff --check` con el rango completo del
   Pull Request para detectar problemas de whitespace.

### Instalación de Bicep

El workflow descarga temporalmente **Bicep 0.45.15** en `$RUNNER_TEMP`
con el SHA-256 fijado:

```
ff5b194b042c220df4a50d6768ed1d6c39a32894bfdc4ff83d62b115d966a7ce
```

Este SHA fue verificado localmente durante la auditoría del repositorio.
La versión y el SHA están fijados para garantizar reproducibilidad. No
se usa `latest`, no se consulta la API de GitHub, no se instala
globalmente.

### Cómo actualizar la versión de Bicep

Para actualizar Bicep a una nueva versión:

1. Descargar el binario `bicep-linux-x64` de la nueva versión desde
   https://github.com/Azure/bicep/releases.
2. Calcular el SHA-256 localmente:
   ```bash
   sha256sum bicep-linux-x64
   ```
3. Actualizar `BICEP_VERSION` y `BICEP_SHA256` en el paso `Install Bicep`
   del workflow. Ambas variables deben actualizarse conjuntamente.

## Separación entre IaC y despliegue de aplicación

Esta plantilla se ocupa **exclusivamente** de la infraestructura: Resource
Group, plan, web app, monitoreo y managed identity.

El **despliegue de la aplicación** (publicación del binario .NET en la Web
App) es responsabilidad de:

1. El workflow `main_impactx-api-backend.yml` existente en GitHub Actions, o
2. Comandos `az webapp deploy` en pipelines futuros.

No se configura CI/CD de IaC en esta fase. Los workflows de GitHub Actions
existentes no han sido modificados.

## Proceso futuro de what-if

Antes de desplegar cualquier recurso, ejecutar un what-if para validar los
cambios planeados:

```bash
az deployment sub what-if \
  --location <region> \
  --template-file infra/main.bicep \
  --parameters infra/environments/<env>.bicepparam
```

Revisar que los recursos creados sean exactamente los esperados y que no haya
modificaciones sobre recursos existentes.

## Proceso futuro de deployment

Una vez aprobado el what-if:

```bash
az deployment sub create \
  --location <region> \
  --template-file infra/main.bicep \
  --parameters infra/environments/<env>.bicepparam
```

## Proceso futuro de adopción de recursos existentes

Los recursos actuales (como el App Service `impactx-api-backend` existente) no
han sido importados ni validados contra esta plantilla. No están administrados
por ella.

Para analizar un recurso existente se puede exportar la plantilla ARM del
Resource Group que lo contiene:

```bash
az group export --name "<resource-group>" > exported-template.json
```

Luego puede decompilarse a Bicep:

```bash
az bicep decompile --file exported-template.json
```

Como alternativa, en VS Code se puede usar el comando "Bicep: Insert Resource"
proporcionando el resource ID del recurso existente.

La salida exportada o decompilada es de mejor esfuerzo y debe revisarse
cuidadosamente. La palabra clave `existing` en Bicep permite referenciar
recursos sin desplegarlos, pero no adopta automáticamente su ciclo de vida
dentro de la plantilla.

Para comenzar a administrar un recurso existente mediante una declaración
normal de Bicep se requiere revisar cuidadosamente sus propiedades y ejecutar
what-if antes de cualquier despliegue.

No se realizará esa adopción en esta fase. Se recomienda mantener los nombres
actuales fuera del alcance de esta plantilla hasta que se decida una estrategia
de migración.

## Pasos previos al primer despliegue

1. Instalar Azure CLI (`az`).
2. Autenticarse en la suscripción correcta:
   ```bash
   az login
   az account set --subscription "<subscription-id>"
   ```
3. Verificar la pila Linux de .NET disponible:
   ```bash
   az webapp list-runtimes --os linux | grep DOTNET
   ```
4. Sustituir `DONT_DEPLOY_UNTIL_RUNTIME_IS_VERIFIED` por el runtime real
   (ej: `dotnet|10.0`) en los archivos bicepparam.
5. Revisar región y SKU según disponibilidad y costo.
6. Ejecutar Bicep build:
   ```bash
   bicep build infra/main.bicep
   bicep build-params infra/environments/dev.bicepparam
   bicep build-params infra/environments/test.bicepparam
   bicep build-params infra/environments/prod.bicepparam
   ```
7. Ejecutar what-if (ver sección correspondiente).
8. Obtener aprobación del equipo antes de desplegar.

## Nota final

Los recursos existentes de Azure (Web App `impactx-api-backend`, Cosmos DB,
etc.) no fueron importados, validados ni modificados por esta plantilla. Esta
fundación de IaC es un punto de partida para la gestión declarativa de la
infraestructura futura.
