# COSMOS_PAGINATION.md — Paginación y eficiencia de consultas (PR 2B)

## Problema resuelto
Las listas por usuario se leían completas con `ReadNextAsync` en bucle (while) o con
`SELECT * FROM c` sin límite, y las lecturas por id usaban `SELECT TOP 1 * WHERE c.id = @id`
cross-partition aunque el servicio conociera la partition key (usuario). Esto degradaba RU/s
compartidas (400 RU/s) y latencia a medida que crecen las colecciones.

## Solución: paginación por continuation tokens
- **Cosmos**: `CosmosPageReader.ReadSinglePageAsync` — un único `ReadNextAsync` por petición con
  `QueryRequestOptions.PartitionKey` (partición única), `MaxItemCount = pageSize` y el token de
  continuación del SDK. El token de Cosmos se pasa de vuelta al cliente tal cual (opaco).
- **EF (InMemory/dev/tests)**: `EfPageReader` + `OffsetContinuationToken` — token base64 opaco
  `offset:N` con `Skip/Take`. Consulta con probe `pageSize + 1` para decidir `HasMoreResults` sin
  falsear el token: si hubo elemento extra no se devuelve y el token apunta a `offset + pageSize`;
  una página final exacta o parcial nunca genera token (no hay más páginas). Un token malformado →
  `BadRequestException` (400) genérica.
- **Validación centralizada** (`Core/Pagination/PaginationValidator`): pageSize default 20,
  mínimo 1, máximo 100 (fuera de rango → 400); token nulo permitido (primera página); token
  vacío, con CR/LF o > 2048 chars → 400. El token nunca aparece en errores ni logs.
- **Contrato legacy conservado**: los endpoints existentes (routes/frequent, routes/history,
  notifications, contacts, devices, monitors, subscription history, subscription payments)
  devuelven `List<T>` en el body como siempre y exponen el siguiente token en el header
  **`X-Continuation-Token`** (nunca en URL ni body). Cuando no hay más páginas, no se envía el
  header. `HasMoreResults` se infiere de que la página vino llena.
- **Endpoints nuevos paginados** (cuerpo `PagedResult<T>`: items, continuationToken,
  hasMoreResults, pageSize): `GET /api/trips`, `GET /api/trips/{id}/telemetry`,
  `GET /api/alerts`, `GET /api/wearable/all`.
- **CORS**: `X-Continuation-Token` y `X-Correlation-Id` en `WithExposedHeaders` (solo lectura del
  header, nunca en `WithHeaders`: el token viaja como query param, no como header de petición) en
  `Program.cs`.

## Lecturas por id sin cross-partition (point-reads)
Nuevos `GetByIdAsync(usuarioId, id)` en 11 repositorios usan `ReadItemAsync` con
`PartitionKey(usuarioId)` (lectura puntual por contrato, 404 → null) y validan propiedad en el
servicio. Se conserva `GetByIdAsync(id)` (cross-partition TOP 1 documentada) solo para flujos
sin partition key. Servicios con chequeo de propiedad: Viaje (telemetría incluida), Alerta,
Incidente, Notificación, Contacto, Monitor, Pago, Ruta, Dispositivo, Wearable, Suscripción.

## Procesos administrativos incrementales
Antes: cargar todo el conjunto y procesar en memoria. Ahora pagina y procesa página a página
sin acumular: `RevokeAllByUsuarioIdAsync` (refresh tokens), `InvalidateAllByUsuarioIdAsync`
(password reset), `DeleteAllByUsuarioIdAsync` (dispositivos/notificaciones),
`MarkAllAsReadAsync` (notificaciones), `ExpireAllAsync` y `ProcessTrialsEndingAsync`
(suscripciones). `PlanService.ExpireSubscriptionsAsync` usa `ExpireAllAsync(process, ct)`.

## Límites de incidentes
`IncidentFilterRequest` (OFFSET/LIMIT legacy): `Pagina ≥ 1` y `Tamano` entre 1 y 100
(default 20) validados en el servicio → 400. Nunca se mezcla OFFSET con continuationToken.

## Contratos y seguridad
- 400 ProblemDetails para pageSize/token inválidos (via `BadRequestException`).
- IDOR: telemetría de viaje ajeno → 404 (point-read null) / 403 (entidad de otro usuario).
- OpenAPI: descripciones de `pageSize` (1–100, default 20) y `continuationToken` (token opaco
  del header) en el operation transformer; 400 documentado por el response transformer.
- Token nunca se registra (RequestLoggingMiddleware no loguea query string).

## Resultados
731 pruebas (705 baseline + 26 nuevas: EfPageReader 11, CosmosPageReader 8, contrato paginación
+7: JSON legacy 1, clientes independientes, seguridad 5), 164 Category=Security, 77 contratos V1,
30 Python, scanner 0 violaciones (282 archivos), NuGet 0 vulnerables, actionlint limpio,
`dotnet format` limpio en C# modificados/nuevos, `git diff --check` limpio, build/tests OK Debug
y Release (731/731 ambos).

## Pendiente / deuda
- Cosmos real no contactado en esta rama; verificación pendiente con emulador/cuenta real.
- Telemetría de viajes usa TTL 7776000; listas muy grandes siguen el patrón página a página.
- Rate limiting de paginación sin calibrar (política por defecto).
