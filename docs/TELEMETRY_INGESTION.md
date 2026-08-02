# Telemetry Ingestion — PR 2C

> Ingesta por lotes de telemetría de viaje con **idempotencia por EventId** y
> **escritura atómica por lote**.
> Estado: implementado en `feat/backend-telemetry-ingestion-idempotency`.
> Azure y Cosmos DB **reales no fueron contactados ni modificados** en esta
> rama; todo lo descrito aquí es el diseño y la validación local.

## 1. Endpoint

```
POST /api/v1/trips/{id}/telemetry
Authorization: Bearer <JWT>
Content-Type: application/json
```

- Es el mismo endpoint por el que la aplicación móvil envía telemetría en
  lote. Coexiste con el endpoint legacy de un solo evento
  (`PATCH /api/trips/{id}/telemetry` y `PATCH /api/v1/trips/{id}/telemetry`),
  que **no se modificó**.
- `[Authorize]` de clase: el `usuarioId` sale del JWT
  (`ClaimTypes.NameIdentifier`), nunca del body.
- `[EnableRateLimiting("telemetry-ingestion")]`: la misma política que ya
  limita la ingesta legacy.
- `[RequestSizeLimit(TelemetryIngestionLimits.MaxBodyBytes)]` (32 KB): cuerpos
  mayores se rechazan en el servidor (Kestrel) con **413** antes de llegar a
  la acción.

## 2. Contrato de entrada

```jsonc
{
  "eventos": [
    {
      "eventId": "c9f2...",          // GUID del cliente — es la clave de idempotencia
      "timestamp": "2026-08-01T10:00:00Z",  // UTC obligatorio (sufijo Z o +00:00)
      "lat": 19.432608,              // [-90, 90]
      "lng": -99.133208,             // [-180, 180]
      "velocidad": 42.5,             // [0, 400] km/h
      "altitud": 2240,               // opcional, [-1000, 10000] m
      "heading": 90                  // opcional, [0, 360) grados
    }
  ]
}
```

| Regla | Valor |
|---|---|
| Lote | de **1 a 100** eventos (`TelemetryIngestionLimits`). |
| `eventId` | GUID único **dentro del lote**, obligatorio. Un GUID vacío/duplicado → 400. |
| `timestamp` | UTC con sufijo explícito (`Z` o `+00:00`); sin sufijo → **400** (converter `UtcTimestampJsonConverter`). Tolerancia de hasta 5 minutos hacia el futuro (reloj del cliente); más → 400. |
| Números | NaN/Infinity → 400 (JSON.NET no los serializa; validados igualmente). Rangos de lat/lng/velocidad/altitud/heading en `TelemetryIngestionLimits`. |
| Semántica | El timestamp del cliente y el `RecibidoEn` del servidor son independientes: `RecibidoEn` se fija en el servidor (`DateTime.UtcNow`) en el momento de la ingesta y **no participa** en la igualdad idempotente. |

## 3. Idempotencia y atomicidad

- **Clave de idempotencia**: `eventId` del cliente, persistido como `Id` del
  documento `ViajeTelemetry` (contenedor `TelemetriaViaje`, partition key
  `/viajeId`).
- **`TransactionalBatch` atómico**: si `IsSuccessStatusCode` es `true`, todas
  las operaciones quedaron confirmadas; si el batch falla, **ninguna** se
  persiste. Los estados individuales de operación (200, 409,
  FailedDependency) **nunca** declaran inserciones dentro de un batch global
  fallido: operaciones no responsables del fallo aparecen como
  `FailedDependency` y se clasifican por point-read, no por su estado.
- **Pre-check** (servicio): point-read por evento antes del lote; existente
  **idéntico** → duplicado (no se reinserta); **diferente** → `ConflictException`
  → 409 genérico. Con cero eventos nuevos el servicio **no crea ningún batch**.
- **Resolución de carrera** (repositorio Cosmos): si un batch falla:
  1. No se cuenta ninguna inserción del batch fallido.
  2. Se relee cada EventId candidato con point-read (`id` +
     `PartitionKey(viajeId)`): existente idéntico → duplicado; existente con
     contenido diferente → `ConflictException` (409, **sin reintentar el batch**
     y sin revelar EventId ni payload); inexistente → pendiente.
  3. Con solo duplicados idénticos y pendientes, se reconstruye el batch
     **únicamente con los pendientes** y se reintenta, **máximo 2 reintentos
     adicionales** (3 intentos en total), respetando `CancellationToken`.
  4. Solo un batch confirmado cuenta `Insertados`. Si todos existen idénticos:
     `Insertados=0`, `Duplicados=Recibidos`, sin segundo batch.
  5. Reintentos agotados → `CosmosException` segura (mensaje genérico, sin
     EventId ni payload), sin afirmar ninguna inserción, sin ciclo infinito.
- **Invariante en respuestas 200**: `Recibidos == Insertados + Duplicados`
  (garantizado y verificado por pruebas; conteos nunca negativos ni mayores
  que `Recibidos`).
- **EF InMemory**: misma semántica observable — pre-check por `ViajeId +
  EventId`, idéntico → duplicado, diferente → 409, pendientes en un único
  `SaveChangesAsync` (transacción EF). Sin simulación de `TransactionalBatch`.

## 4. Estados del viaje

Solo se acepta telemetría cuando el viaje existe, pertenece al usuario
autenticado y su estado es `Activo` o `Pausado` (misma regla que la ingesta
legacy). Cualquier otro estado → 409.

## 5. Respuesta

`200 OK` con `TelemetryIngestionResultDto`:

| Campo | Significado |
|---|---|
| `viajeId` | Id del viaje. |
| `recibidos` | Eventos válidos recibidos en el lote. |
| `insertados` | Eventos nuevos persistidos. |
| `duplicados` | Eventos ya existentes con contenido idéntico (`insertados + duplicados = recibidos`). |
| `primerEventoUtc` / `ultimoEventoUtc` | `min`/`max` de los `timestamp` del cliente del lote (UTC), independientes del `RecibidoEn`. |

Errores: 400 (validación/JSON/UTC/duplicados dentro del lote), 401 (sin
JWT), 403/404 (viaje ajeno → 404 sin revelar existencia; el middleware
mapea el `ForbiddenException` interno a 404), 404 (viaje inexistente), 409
(estado no permitido o `eventId` reenviado con contenido diferente), 413
(payload > 32 KB), 429 (rate limit).

## 6. Implementación

| Capa | Archivo | Responsabilidad |
|---|---|---|
| Límites | `ImpactXv1/Core/Telemetry/TelemetryIngestionLimits.cs` | Constantes únicas (tamaño de lote, bytes, tolerancia temporal, rangos). |
| Validación | `ImpactXv1/Core/Telemetry/TelemetryBatchValidator.cs` | Valida el lote completo → `BadRequestException` (400). |
| Igualdad | `ImpactXv1/Core/Telemetry/TelemetryEventEquality.cs` | `IsIdentical` entre evento persistido y reenviado (ignora `RecibidoEn`). |
| DTOs | `ImpactXv1/Models/DTOs/TelemetryIngestionDtos.cs` | `TelemetryBatchRequest`, `TelemetryEventRequest`, `TelemetryIngestionResultDto`. |
| Converter | `ImpactXv1/Converters/UtcTimestampJsonConverter.cs` | Exige UTC explícito; violación → `JsonException` → 400. |
| Resultado | `ImpactXv1/Core/Domain/TelemetryBatchWriteResult.cs` | `Insertados`/`Duplicados` devueltos por el repositorio. |
| Servicio | `ImpactXv1/Services/ViajeService.cs` → `IngestTelemetryAsync` | Orquesta: validación → propiedad del viaje → estado → duplicados → batch → resultado. Logs solo con conteos. |
| Repo Cosmos | `ImpactXv1/Infrastructure/Data/Repositories/Cosmos/CosmosViajeRepository.cs` | Point-read por evento + `TransactionalBatch` + resolución de 409 por re-lectura. |
| Repo EF | `ImpactXv1/Infrastructure/Data/Repositories/EF/ViajeRepository.cs` | Pre-check por evento + `AddRange` + un solo `SaveChangesAsync`. |
| Controlador | `ImpactXv1/Controllers/TripsController.cs` | `POST {id:guid}/telemetry` (rutas duales legacy/V1), rate limiting, límite de body, `CancellationToken`. |
| OpenAPI | `ImpactXv1/Extensions/OpenApiV1OperationTransformer.cs` | Descripción del POST con la semántica de idempotencia y límites. |

## 7. Seguridad

- `usuarioId` siempre del JWT; el body no lo admite.
- Viaje ajeno → el `ForbiddenException` interno se traduce a **404
  ProblemDetails** genérico (sin revelar existencia ni propietario).
- `eventId` reenviado con contenido diferente → 409 sin revelar el contenido
  almacenado.
- Sin consultas cross-partition: point-reads con `PartitionKey(viajeId)`.
- Errores de validación: mensajes genéricos, sin eco del payload.
- El `RecibidoEn` y los conteos nunca distinguen el motivo interno de un 409.

## 8. Pruebas

- **Unitarias**:
  - `TelemetryBatchValidatorTests` — lote vacío/101/100; GUID duplicado,
    vacío e inválido; timestamps no-UTC/no-ISO, futuro > 5 min; NaN/Infinity;
    rangos de lat/lng/velocidad/altitud/heading; batch válido (teorías);
    **tamaño serializado del lote máximo válido (100 eventos con todos los
    campos ≈ 19 KB) ≤ `TelemetryIngestionLimits.MaxBodyBytes` (32 KB)**.
  - `ViajeServiceTelemetryIngestionTests` — viaje inexistente (404), viaje
    ajeno (404, Category=Security), estado no permitido (409), duplicados
    idénticos sin llamar al batch, `eventId` con contenido diferente (409),
    batch mezclado, `CancellationToken`, **invariantes 200**:
    `Recibidos == Insertados + Duplicados` para todos nuevos, todos
    duplicados y mezcla pre-check + carrera (Category=Security en logs sin
    datos sensibles).
  - `CosmosViajeRepositoryTelemetryTests` — point-read con partición correcta
    (Category=Security); **batch fallido con operación individual 200 → cero
    insertados (el 200 no implica persistencia)** (Category=Security);
    **Conflict + `FailedDependency` resueltos por point-reads** (el
    FailedDependency no es fatal ni se cuenta como insertado)
    (Category=Security); carrera duplicado idéntico + nuevo → reintento solo
    con el nuevo (1 insertado + 1 duplicado) (Category=Security); conflicto
    de contenido → 409 sin reintentar y sin revelar EventId
    (Category=Security); todos idénticos → cero inserciones sin segundo
    batch; segundo reintento con carrera y tercero confirmado; **agotamiento
    de reintentos → `CosmosException` segura** (Category=Security);
    `CancellationToken` en point-reads y `ExecuteAsync`; **sin
    upsert/replace**; `PartitionKey(viajeId)` en batches y point-reads.
  - `ViajeRepositoryTelemetryTests` — lote insertado con un solo
    `SaveChangesAsync`, duplicado idéntico no insertado, batch mezclado sin
    duplicar, conflicto de contenido → `ConflictException` (Category=Security).
  - `TelemetryEventEqualityTests` — igualdad exacta por `EventId`: mismos
    valores → `true`; cambio en Lat/Lng/Velocidad/Altitud/Heading → `false`;
    null vs null → `true`; null vs valor → `false` (sin epsilon; la exactitud
    es intencional para la idempotencia).
- **Integración** (`TripsTelemetryIngestionTests`, TestServer + EF InMemory):
  401 sin JWT en ambas rutas (legacy y V1, Category=Security); 400 con
  `eventId` duplicado en el lote y con timestamp sin sufijo UTC; 404 para
  viaje inexistente y para viaje ajeno sin revelar información
  (Category=Security); 409 para estado no permitido y para `eventId`
  reenviado con contenido diferente; ingesta completa y re-envío del mismo
  lote → duplicados sin doble inserción; batch mezclado; `GET` paginado sigue
  devolviendo la telemetría; `POST` documentado en OpenAPI con requestBody,
  respuestas 200/400/401/403/404/409/429/500 y schemas; límite de body 32 KB
  (metadata del endpoint, Category=Security).
