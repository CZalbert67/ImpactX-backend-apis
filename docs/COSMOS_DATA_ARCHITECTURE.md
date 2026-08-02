# Cosmos Data Architecture

> Documento de arquitectura de persistencia Cosmos DB para ImpactX.
> Estado: PR 2A — Cosmos Data Architecture and Persistence Hardening (implementado en `feat/backend-cosmos-data-architecture`); actualizado por PR 2B (paginación, `docs/COSMOS_PAGINATION.md`) y PR 2C (ingesta por lotes con `TransactionalBatch`, `docs/TELEMETRY_INGESTION.md`).
> Azure y Cosmos DB **reales no fueron contactados ni modificados** en esta rama; todo lo descrito aquí es el diseño y la validación local.

## 1. Cuenta y base

| Concepto | Valor |
|---|---|
| Cuenta | `impactx-db-west-final` |
| Base de datos | `ImpactX-Data` |
| Plan | Free Tier habilitado |
| Límite total de cuenta | **1000 RU/s** |
| Throughput de la base | Manual compartido: **400 RU/s** |
| Modelo de throughput | Las **400 RU/s se comparten entre todos los contenedores**. Ningún contenedor debe tener throughput dedicado. |

El catálogo (`CosmosContainerCatalog`) y la creación de contenedores no aceptan throughput por contenedor: `CosmosDbContext.EnsureContainersAsync` crea la base con `ThroughputProperties.CreateManualThroughput(SharedThroughput)` (400 por defecto) y los contenedores con `CreateContainerIfNotExistsAsync(ContainerProperties)` sin parámetro de throughput.

## 2. Configuración

Sección `AzureCosmosDb` (Options pattern, `CosmosDatabaseOptions`, validada con `ValidateOnStart`):

| Clave | Default | Regla |
|---|---|---|
| `Endpoint` | (requerido) | URI absoluto HTTP(S). **Nunca se registra en logs ni respuestas.** |
| `Key` | (requerido en runtime) | **Nunca con valor real en appsettings.** El placeholder `YOUR_AZURE_COSMOS_KEY` se valida en readiness (`ConfigurationReadinessCheck`), no en opciones, para no impedir el arranque en Development. |
| `DatabaseName` | `ImpactX-Data` | No vacío. |
| `SharedThroughput` | `400` | Entero positivo ≤ 1000 (límite Free Tier de esta cuenta). |
| `RequestTimeoutSeconds` | `30` | Entero positivo. |
| `MaxRetryAttemptsOnRateLimitedRequests` | `3` | ≥ 0. Reintentos **limitados** para 429: sin reintentos infinitos. |
| `MaxRetryWaitTimeSeconds` | `30` | ≥ 0. |

Los tres `appsettings` (`appsettings.json`, `Development.json`, `Production.json`) documentan el throughput compartido y los límites de reintento.

## 3. Inventario de contenedores

Catálogo único: `ImpactXv1/Infrastructure/Data/CosmosContainerCatalog.cs` (18 contenedores). Fuente única de verdad para creación y acceso; impide nombres duplicados y partition key paths inválidos; conserva nombres y partition keys existentes (compatibilidad).

| Contenedor | Entidad | Partition key | TTL (s) | Consultas principales |
|---|---|---|---|---|
| `Usuarios` | Usuario | `/id` | −1 | point-read por id; correo/username (cross-partition, `TOP 1`); `SearchAsync` (CONTAINS, límite 20) |
| `RefreshTokens` | RefreshToken | `/usuarioId` | 604800 (7d) | token (cross-partition, `TOP 1`); activos por usuario (particionada); revocar todos (particionada) |
| `PasswordResetTokens` | PasswordResetToken | `/usuarioId` | 3600 (1h) | tokenHash (cross-partition, `TOP 1`); invalidar por usuario (particionada) |
| `Dispositivos` | Dispositivo | `/usuarioId` | −1 | por usuario / activos (particionadas); por deviceId (particionada, `TOP 1`); **tokenFcm global (cross-partition, deuda documentada)**; point-read por (usuarioId, id) |
| `Planes` | Plan | `/id` | −1 | point-read por id; por nombre (cross-partition, `TOP 1`); `GetAllAsync` (catálogo global) |
| `Suscripciones` | Suscripcion | `/usuarioId` | −1 | activa/historial por usuario (particionadas); por id (cross-partition, `TOP 1`); expiradas/trials (cross-partition, mantenimiento) |
| `Pagos` | Pago | `/usuarioId` | −1 | por usuario (particionada); por id (cross-partition, `TOP 1`) |
| `Monitores` | Monitor | `/usuarioId` | −1 | por usuario / activos (particionadas); tokenInvitacion (cross-partition, `TOP 1`); por usuario+monitor (particionada) |
| `ContactosEmergencia` | ContactoEmergencia | `/usuarioId` | −1 | por usuario / principal / count / existe teléfono (particionadas); por id (cross-partition, `TOP 1`) |
| `Rutas` | Ruta | `/usuarioId` | −1 | por usuario / frecuentes / historial (límite 50) / seleccionada hoy (particionadas); por id (cross-partition, `TOP 1`) |
| `Viajes` | Viaje | `/usuarioId` | 7776000 (90d) | por usuario / activo (particionadas, límite 50); por id (cross-partition, `TOP 1`) |
| `TelemetriaViaje` | ViajeTelemetry | `/viajeId` | 7776000 (90d) | por viaje (particionada por `/viajeId`, `ORDER BY timestamp ASC`); **point-read por evento**: `ReadItemAsync(eventId, PartitionKey(viajeId))` (idempotencia de ingesta); **escritura atómica por lote**: `TransactionalBatch` sobre la partición `/viajeId`. **La telemetría permanece relacionada con TripId.** |
| `Alertas` | Alerta | `/usuarioId` | 31536000 (1y) | por usuario / pendientes / activas / count (particionadas); por id (cross-partition, `TOP 1`) |
| `Notificaciones` | Notificacion | `/usuarioId` | 2592000 (30d) | por usuario / count no leídas (particionadas); por clave de idempotencia (particionada cuando hay destinatario; cross-partition si no); por id (cross-partition, `TOP 1`) |
| `Wearables` | Wearable | `/usuarioId` | −1 | por usuario / vinculado (particionadas); pairingToken / dispositivoId (cross-partition, `TOP 1`); por id (cross-partition, `TOP 1`) |
| `AppInvites` | AppInvite | `/usuarioId` | 2592000 (30d) | (sin repositorio aún) |
| `ChatThreads` | ChatThread | `/usuarioId` | −1 | (sin repositorio aún) |
| `Incidentes` | Incidente | `/usuarioId` | −1 | por usuario / filtrado / counts (particionadas, OFFSET/LIMIT con parámetros); por id (cross-partition, `TOP 1`) |

**No existe dominio Vehicles**: la telemetría se relaciona con `TripId` (`ViajeTelemetry.ViajeId`), nunca con vehículos.

## 4. Índices

- Todos los contenedores usan la **política de índices por defecto de Cosmos** (todos los paths indexados automáticamente).
- **No se definen composite indexes**: todas las consultas con `ORDER BY` están acotadas a una sola partición (`QueryRequestOptions.PartitionKey`), y Cosmos no exige composite indexes para `ORDER BY` dentro de una partición. No hay consultas cross-partition con `ORDER BY`.
- El catálogo **permite representar** composite indexes por contenedor (`CosmosContainerDefinition.CompositeIndexes`, vacío hoy) sin duplicar la política; se agregarán solo si una consulta real los requiere.
- No se excluyen paths: no hay campos grandes consultados que justifiquen exclusión.

## 5. Particionamiento y operaciones de punto

Principios aplicados en los repositorios (`ImpactXv1/Infrastructure/Data/Repositories/Cosmos/`):

1. **Point reads** (`ReadItemAsync`) cuando id + partition key son conocidos: `Usuarios` (`/id`), `Planes` (`/id`), `Dispositivos` (`usuarioId, id`).
2. **`QueryRequestOptions.PartitionKey`** en toda consulta filtrada por el valor de la partition key del contenedor (usuarioId, viajeId): sin cross-partition para datos ligados a usuario/viaje/alerta/dispositivo.
3. **`ReplaceItemAsync`** en `UpdateAsync` (los servicios siempre leen antes de actualizar; el reemplazo es más correcto que upsert y no crea documentos accidentales).
4. **Todas las consultas parametrizadas** con `QueryDefinition`; los valores del usuario nunca se concatenan en SQL (ver `IncidenteQueryBuilder`: cláusulas fijas + parámetros; OFFSET/LIMIT son enteros del dominio, no strings).
5. **`MaxItemCount`** prudente en todos los iteradores (1 en `TOP 1`/COUNT, 50/100 en listados). `MaxItemCount` es el tamaño de página, **no** el límite total del resultado: los listados sin `break` recorren todas las páginas de la partición del usuario. Los límites totales explícitos están donde hay detención temprana (ver sección 12).
6. **`CancellationToken`** respetado en las operaciones que lo aceptan.

### Cross-partition justificadas (conservadas deliberadamente)

| Consulta | Motivo |
|---|---|
| `Usuarios` por correo/username/search | Partición `/id`; búsqueda global por negocio. |
| `RefreshTokens.GetByTokenAsync` | Lookup por token sin usuarioId. |
| `PasswordResetTokens.GetByTokenHashAsync` | Lookup por hash sin usuarioId. |
| `Dispositivos.GetByTokenFcmAsync` | Unicidad global de token (deuda documentada). |
| `Monitores.GetByTokenAsync`, `Wearables.GetByPairingTokenAsync`/`GetByDispositivoIdAsync` | Lookup por token/deviceId sin usuarioId. |
| `Planes.GetByNameAsync`/`GetAllAsync`, `PlanSeeder` COUNT por nombre | Catálogo pequeño (≤ 6 documentos). |
| `Suscripciones.GetByIdAsync` | El contrato de repositorio solo recibe id; partición `/usuarioId` no disponible. `TOP 1` + detención temprana. |
| `Suscripciones.GetExpiredAsync`/`GetTrialsEndingAsync` | Mantenimiento global sin partición conocida (procesa todas las páginas). **Reemplazados en PR 2B por `ExpireAllAsync`/`ProcessTrialsEndingAsync` (página a página); conservados por compatibilidad de contrato.** |
| `Viajes/Rutas/Alertas/Incidentes/Notificaciones/Monitores/ContactosEmergencia/Wearables/Pagos.GetByIdAsync` | El contrato solo recibe id; partición `/usuarioId` no disponible. `TOP 1` + detención temprana (los servicios usan los nuevos point-reads `GetByIdAsync(usuarioId, id)` de PR 2B cuando el partition key está disponible). |

### Escritura atómica por lote (PR 2C)

- `TelemetriaViaje` (partition key `/viajeId`) se escribe por lotes con un único
  `TransactionalBatch` sobre la partición del viaje: todos los eventos del
  lote se insertan atómicamente (o ninguno). No hay transacciones
  cross-partition: el lote nunca cruza particiones.
- Idempotencia por `eventId` (clave de idempotencia del cliente = `Id` del
  documento): point-read `ReadItemAsync(eventId, PartitionKey(viajeId))`
  antes del batch y, si el `TransactionalBatch` devuelve 409 Conflict (carrera),
  re-lectura point-read del evento para decidir duplicado (idéntico) o
  `ConflictException` (contenido diferente). Sin upsert/replace: un evento
  existente nunca se sobrescribe.
- Un batch fallido **nunca** cuenta inserciones (los estados individuales 200/
  409/FailedDependency no prueban persistencia) y `FailedDependency` se
  resuelve como candidato mediante point-read. Reintentos limitados: 1 inicial
  + 2 adicionales (máximo 3 intentos), `CancellationToken` respetado;
  agotamiento → `CosmosException` segura. Detalles en
  `docs/TELEMETRY_INGESTION.md` (sección 3).
- **Sin cambios de throughput ni de partition keys**: la ingesta por lote no
  altera el catálogo de contenedores, el throughput compartido de la base ni
  las definiciones de partición (la telemetría permanece en `/viajeId`).

> **Corrección indispensable (PR 2A)**: `GetByIdAsync` de estos contenedores usaba `ReadItemAsync(id, PartitionKey(id))`, que con partición `/usuarioId` devuelve **404 siempre** (partition key incorrecta). Se reemplazó por consulta parametrizada `SELECT TOP 1 * FROM c WHERE c.id = @id` con `MaxItemCount=1`: corrige el fallo funcional sin cambiar el contrato del repositorio. Si se quiere un point-read real, el contrato debe evolucionar a `GetByIdAsync(usuarioId, id)` (pendiente).

## 6. Estrategia de inicialización

`CosmosInitializationService` (BackgroundService, solo con `UseCosmosDb=true`):

1. `EnsureDatabaseAsync`: `CreateDatabaseIfNotExistsAsync` con throughput manual compartido (`SharedThroughput`); si la cuenta rechaza throughput manual (400 BadRequest), reintenta sin throughput. 401/403/429/5xx propagan.
2. Por cada definición del catálogo: `ReadContainerAsync` → si no existe, `CreateContainerIfNotExistsAsync` **sin throughput** → re-lectura y validación.
3. **Contenedor existente con partition key distinta al catálogo**: no se borra, no se recrea, no se modifica; la inicialización queda `Failed` con descripción segura ("Database schema mismatch detected; controlled migration required.") y `/health/ready` queda **Unhealthy**. Solo se registra el nombre lógico del contenedor.
4. Idempotente: una segunda ejecución no crea nada si el esquema coincide.
5. `PlanSeeder.SeedPlansAsync`: point-read por **IDs determinísticos** (`FreePlanId`/`BasicPlanId`/`PremiumPlanId` = GUIDs fijos `00000000-...-0001..3`); fallback COUNT parametrizado por nombre (cubre planes sembrados con IDs aleatorios antes de PR 2A); `CreateItemAsync` con manejo de Conflict (carrera); 401/403/429/5xx y OCE propagan. **Sin `SELECT * FROM c`.**
6. El throughput de una base existente **no se cambia desde la API**.

## 7. Comportamiento ante 429

- `CosmosClientOptions.MaxRetryAttemptsOnRateLimitedRequests=3` y `MaxRetryWaitTimeOnRateLimitedRequests=30s`: reintentos **limitados** (no infinitos) para 429.
- `CosmosInitializationService` reintenta transitorios (408/429/5xx/timeout) hasta `MaxAttempts` (3) con `RetryDelaySeconds`; los 429 durante la inicialización no marcan `Failed` hasta agotar reintentos.
- **No reiniciar repetidamente App Service por throttling**: el 429 de Cosmos se resuelve con backoff del SDK; un reinicio no libera RU y en el plan F1 los stop requests por cuota se diagnostican con `az webapp show --query state` / `az webapp list-usages` (no con restart).

## 8. Procedimiento de migración si una partition key no coincide

Si la validación de arranque detecta `CosmosSchemaValidationException` (contenedor con PK distinta al catálogo):

1. **Detener el rollout** del cambio (readiness queda Unhealthy con descripción segura; no se elimina ni recrea nada automáticamente).
2. **Diseñar la migración controlada** (fuera de banda): exportar los datos del contenedor (Azure Data Factory / Cosmos DB migration tools), crear el contenedor nuevo con la partition key del catálogo y el TTL correspondiente, reinsertar con transformación del partition key, verificar conteos y dual-reads, y solo entonces cambiar el código al contenedor nuevo.
3. Mientras tanto, el catálogo es la fuente de verdad: **cualquier cambio de partition key debe pasar por una nueva definición + procedimiento**, nunca por borrado/recreación automática.
4. No afirmar que este procedimiento fue ejecutado: es la guía para un incidente futuro.

## 9. Deuda de unicidad global de TokenFcm (documentada, no resuelta en PR 2A)

- La comprobación **query-before-write** (`GetByTokenFcmAsync` global en `CosmosDispositivoRepository`) reduce los conflictos comunes pero **no garantiza unicidad atómica global** bajo concurrencia extrema (dos writes simultáneos con el mismo token pueden pasar ambos la comprobación).
- **No se implementa una falsa transacción entre contenedores**: `TransactionalBatch` solo funciona dentro de una misma partition key y no cubre la unicidad global.
- Solución futura propuesta (no implementada): un contenedor global de registro con `id` derivado de un hash del token, con la escritura del registro como precondition de la del dispositivo; requerirá compensación o rediseño del almacenamiento para coordinar registro y dispositivo (p. ej. misma partición o `TransactionalBatch`).
- El contrato funcional de Devices **no cambió** en PR 2A.

## 10. Recomendaciones de escalamiento futuro

- Telemetría: considerar partición compuesta `/viajeId` con TTL de 90 días (ya aplicado) y, si crece, sumidero de archivo (p. ej. Azure Storage) en lugar de lecturas completas por viaje.
- Si algún contenedor excede el rendimiento compartido: **escalar el throughput de la base dentro del límite de 1000 RU/s del Free Tier primero**, y evaluar throughput dedicado solo con análisis de RU por consulta (continuación del punto 7: no reinventar).
- Composite indexes solo cuando aparezca una consulta cross-partition real con `ORDER BY` (hoy no existe).
- `GetByIdAsync(usuarioId, id)` en los contratos de repositorio para convertir las cross-partition justificadas de point-lookup en point-reads reales.
- Página pública de paginación con continuation tokens — **implementada en PR 2B** (`docs/COSMOS_PAGINATION.md`); hoy los límites totales explícitos son 50 en rutas/viajes y 20 en búsqueda de usuarios.
- Monitorear 429 con Application Insights (workspace ya desplegado en `infra/`): alertar por tasa de 429 sostenida, no por ocurrencias puntuales.

## 12. Límites reales de resultados (clasificación revisada en PR 2A)

`MaxItemCount` es tamaño de página; el límite total solo existe donde el iterador tiene detención temprana. Clasificación por consulta:

**A. Existencia / TOP 1 — acotadas y con detención temprana (MaxItemCount=1):**
`GetByIdAsync` de los 15 repos de partición `/usuarioId` y `/id`, `GetByCorreoAsync`/`GetByUsernameAsync`/`GetByTokenAsync`/`GetByTokenHashAsync`/`GetByTokenFcmAsync`/`GetByPairingTokenAsync`/`GetByDispositivoIdAsync`/`GetByDeviceIdAsync`/`GetByUsuarioYMonitorAsync`/`GetByNameAsync`, `GetActiveByUserAsync`/`GetSelectedTodayAsync`/`GetPrincipalAsync`, todos los COUNT/EXISTS y `GetByIdempotencyKeyAsync`.

**B. Listados por usuario — particionados (`QueryRequestOptions.PartitionKey`), volumen acotado por dominio o TTL:**

| Consulta | Límite total | Observación |
|---|---|---|
| `Rutas.GetFrequentByUserAsync`/`GetHistoryByUserAsync` | **50** (`break`) | Límite explícito con detención temprana. |
| `Viajes.GetByUserAsync` | **50** (`break`) | Límite explícito con detención temprana. |
| `Usuarios.SearchAsync` | **20** (`break`) | Búsqueda global cross-partition con límite explícito. |
| `Alertas.GetByUserAsync`/`GetPendingByUserAsync`/`GetActiveAlertsAsync` | sin límite total | Partición del usuario; TTL 1 año; eventos de negocio escasos. |
| `Contactos.GetByUserAsync` | sin límite total | Capado por plan (3/5/10). |
| `Monitores.GetByUserAsync`/`GetActiveByUserAsync` | sin límite total | Capado por plan (1/3/6). |
| `Notificaciones.GetByUserAsync` | sin límite total | **El de mayor volumen**: TTL 30 días, alta frecuencia. Paginación implementada en PR 2B (header `X-Continuation-Token`). |
| `Pagos.GetByUserAsync` | sin límite total | Acotado por compras reales. |
| `Suscripciones.GetHistoryByUserAsync`/`GetActiveByUserAsync` | sin límite total | Pocas por usuario. |
| `Dispositivos.GetByUsuarioIdAsync`/`GetActiveByUsuarioIdAsync` | sin límite total | Pocos por usuario. |
| `Wearables.GetAllByUsuarioIdAsync` | sin límite total | Pocos por usuario. |
| `Incidentes.GetByUserAsync`/`GetFilteredAsync` | sin límite total | Filtro paginado OFFSET/LIMIT; **`Tamano` acotado en PR 2B a 1–100 (default 20) y `Pagina ≥ 1`** (400 fuera de rango); el endpoint de tendencia analítica pasa `int.MaxValue` internamente. Particionado, sin fuga entre usuarios. |
| `Viajes.GetTelemetryByViajeAsync` | sin límite total | Partición `/viajeId`; lectura completa necesaria para calcular distancia/media/máx al finalizar el viaje. |

**C. Procesamiento administrativo / global — cross-partition justificada, procesa todas las páginas:**
`Suscripciones.GetExpiredAsync`/`GetTrialsEndingAsync` (mantenimiento global, comentado en código), `RefreshTokens.RevokeAllByUsuarioIdAsync`, `PasswordResetTokens.InvalidateAllByUsuarioIdAsync`, `Dispositivos.DeleteAllByUsuarioIdAsync`, `Notificaciones.MarkAllAsReadAsync`/`DeleteAllByUserAsync` (deben procesar todo el conjunto del usuario para cumplir su semántica; particionadas).

**Deuda de paginación (continuation tokens) — resuelta en PR 2B:**
- PR 2B implementó paginación con continuation tokens (ver `docs/COSMOS_PAGINATION.md`): los endpoints legacy conservan `List<T>` + header `X-Continuation-Token`; los endpoints nuevos (`GET /api/v1/trips`, `GET /api/v1/trips/{id}/telemetry`, `GET /api/v1/alerts`, `GET /api/v1/wearable/all`) devuelven `PagedResult<T>`. `IncidentFilterRequest.Tamano` ahora tiene cota 1–100 y `Pagina ≥ 1` (400). `GetExpiredAsync`/`GetTrialsEndingAsync` fueron reemplazadas por `ExpireAllAsync`/`ProcessTrialsEndingAsync` (página a página).
- La telemetría por viaje quedará acotada cuando exista exportación a sumidero (sección 10).

## 13. Pruebas (PR 2A)

- `CosmosContainerCatalogTests`: nombres únicos/no vacíos, PK paths válidos, 18 contenedores conocidos, sin throughput dedicado, TTL válido, sin composite indexes, rechazo de definiciones inválidas.
- `CosmosDatabaseOptionsTests`: bind válido, defaults, validación de endpoint/databaseName/throughput (≤1000)/timeouts, placeholder de key permitido (readiness lo valida).
- `CosmosSchemaInitializationTests`: base con SharedThroughput=400, contenedores nuevos sin throughput dedicado, idempotencia, **mismatch de PK falla sin borrar ni recrear**, cancelación, fallback sin throughput ante BadRequest, carrera de creación (Conflict), fallo seguro sin secretos, readiness Unhealthy (Category=Security).
- `PlanSeederTests`: 3 planes determinísticos, idempotencia total/parcial, legacy por nombre sin duplicados, Conflict tolerado, 401 propaga, cancelación, sin scan del contenedor (point-reads + COUNT).
- `CosmosPartitionKeysTests` (Category=Security): serialización consistente de partition keys.
- `IncidenteQueryBuilderTests` (Category=Security): SQL sin input crudo del usuario, parámetros enlazados.

## 14. Pruebas adicionales (PR 2C — ingesta y batch)

- `CosmosViajeRepositoryTelemetryTests` (15: 11 de ingesta + 4 de la auditoría atómica): point-read de telemetría con `PartitionKey(viajeId)`; lote atómico con un único `ExecuteAsync`; **409 del `TransactionalBatch` resuelto por re-lectura point-read** (idéntico → duplicado; diferente → `ConflictException`); **batch fallido con operación individual 200 → cero insertados**; `FailedDependency` resuelto como candidato; carrera con reintento solo de pendientes (1+1); agotamiento de reintentos → `CosmosException` segura; resultados mezclados; **sin upsert/replace**; detección de errores de operación indexada; parte de Category=Security.
- Documentación completa del endpoint, contrato e idempotencia en `docs/TELEMETRY_INGESTION.md`.
