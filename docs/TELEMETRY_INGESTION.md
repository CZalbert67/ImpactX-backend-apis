# Ingesta de telemetría de viaje — contrato V1/V2

> Contrato canónico del endpoint wearable. La versión 1 conserva compatibilidad
> histórica; la versión 2 es el formato operativo del Galaxy Watch 8 y la base
> para reglas de detección y futuro entrenamiento de Machine Learning.

## Endpoint y autorización

```http
POST /api/v1/trips/{id}/telemetry
Authorization: Bearer <JWT client=wearable>
Content-Type: application/json
```

- Solo `client=wearable` puede escribir. Web y móvil reciben `403` y conservan
  lectura mediante `GET /api/v1/trips/{id}/telemetry`.
- Viaje inexistente o ajeno se responde sin revelar propiedad.
- Solo admite viajes `Activo` o `Pausado`.
- Lotes de 1 a 100 eventos y cuerpo máximo de **256 KiB**.
- Rate limiting `telemetry-ingestion`.

## Versiones

### Versión 1, compatibilidad

Campos mínimos por evento: `eventId`, `timestamp` UTC, `lat`, `lng`,
`velocidad`, y opcionales `altitud` y `heading`.

### Versión 2, Galaxy Watch 8

El lote requiere:

- `schemaVersion=2`;
- `batchId` y `batchSequence`;
- `wearableDeviceId`, `wearableModel="Galaxy Watch 8"`;
- `batteryLevel`;
- opcionalmente versión de app, WearOS, firmware y desfase del reloj;
- `capturedOffline` para distinguir captura sin conectividad.

Cada evento requiere:

- `eventId`, `timestamp` UTC y `sequenceNumber`;
- GPS, velocidad y `gpsAccuracyMeters`;
- acelerómetro X/Y/Z en m/s²;
- giroscopio X/Y/Z en rad/s;
- `calidadSensor`: `unknown`, `low`, `medium` o `high`.

También admite desaceleración, frecuencia cardiaca, HRV, SpO₂, orientación
`pitch/roll/yaw` y banderas técnicas de sensores. Las magnitudes de
acelerómetro y giroscopio se calculan en servidor cuando existen los tres ejes;
un valor derivado enviado por el cliente no sustituye el cálculo canónico.

Ejemplo abreviado:

```json
{
  "schemaVersion": 2,
  "batchId": "b7bb24d5-fc21-44b7-bc51-41134e57cd57",
  "batchSequence": 14,
  "capturedOffline": true,
  "wearableDeviceId": "GW8-ABC123",
  "wearableModel": "Galaxy Watch 8",
  "wearableAppVersion": "2.0.0",
  "wearableOsVersion": "WearOS",
  "wearableFirmwareVersion": "FW-01",
  "batteryLevel": 78,
  "clockOffsetMilliseconds": 25,
  "eventos": [
    {
      "eventId": "a70b772a-408c-46fa-aa76-9bc3d409b945",
      "timestamp": "2026-08-03T00:00:00Z",
      "sequenceNumber": 1401,
      "lat": 19.4326,
      "lng": -99.1332,
      "velocidad": 42.5,
      "gpsAccuracyMeters": 4.0,
      "aceleracionX": 3.0,
      "aceleracionY": 4.0,
      "aceleracionZ": 0.0,
      "giroscopioX": 0.1,
      "giroscopioY": 0.2,
      "giroscopioZ": 0.3,
      "frecuenciaCardiaca": 86,
      "hrvMilisegundos": 44.0,
      "spo2Porcentaje": 98.0,
      "calidadSensor": "high",
      "sensorFlags": ["gps_degraded"]
    }
  ]
}
```

## Idempotencia y sincronización offline

- `eventId` es la clave idempotente persistida como id del documento dentro de
  la partición `/viajeId`.
- El mismo `eventId` con contenido canónico idéntico se cuenta como duplicado y
  responde `200`; con contenido diferente responde `409`.
- `batchId` identifica el paquete de sincronización, pero la idempotencia final
  permanece por evento para tolerar reempaquetado y reintentos parciales.
- `sequenceNumber` no puede repetirse dentro del lote. El resultado informa la
  primera y última secuencia para que el cliente confirme el rango procesado.
- Los eventos nuevos se escriben en un `TransactionalBatch` de una sola
  partición. Un lote fallido no declara inserciones parciales.

## Respuesta

`TelemetryIngestionResultDto` contiene `viajeId`, `batchId`, `schemaVersion`,
`capturedOffline`, conteos recibidos/insertados/duplicados, rango temporal,
rango de secuencias y `procesadoEnUtc`. Nunca devuelve EventIds, coordenadas ni
documentos internos.

## Persistencia y preparación ML

`ViajeTelemetry` conserva procedencia del wearable, vehículo, batería, desfase
de reloj, calidad GPS, movimiento, biometría, orientación y banderas técnicas.
Los campos `impactCandidate`, `detectionLabel`, `severityLabel`, `ruleVersion`,
`modelVersion` y `labeledAtUtc` son de servidor y no forman parte del payload de
ingesta. Esto permite etiquetar datos posteriormente sin aceptar etiquetas
manipulables desde el wearable.

La retención del contenedor `TelemetriaViaje` continúa en 90 días. La creación
de un dataset de entrenamiento permanente deberá ser un proceso explícito,
consentido y separado de la telemetría operativa.

## Errores esperados

- `400`: esquema/campos/rangos/UTC inválidos o duplicados dentro del lote.
- `401`: sin autenticación.
- `403`: cliente web/móvil o token sin claim `client`.
- `404`: viaje inexistente o no visible.
- `409`: viaje cerrado o EventId existente con contenido diferente.
- `413`: cuerpo mayor de 256 KiB.
- `429`: rate limit.

## Pruebas clave

- validación de versiones 1 y 2, rangos y campos requeridos;
- magnitudes canónicas y banderas normalizadas;
- reenvío idéntico y conflicto de contenido;
- atomicidad Cosmos e idempotencia EF InMemory;
- recorrido HTTP de escritura wearable y lectura web;
- OpenAPI con límite, sensores y respuesta `403`.


## Etiquetado automático de reglas v1

Después de canonicalizar magnitudes y antes de persistir el batch, el backend
evalúa cada evento con `impact-rules-v1`. El resultado se guarda en campos de
solo servidor: `impactCandidate`, `detectionLabel`, `severityLabel`,
`ruleVersion`, `detectionScore` y `labeledAtUtc`. Estos campos no forman parte
del DTO de entrada y por ello no pueden ser forzados por el wearable.

El motor exige una señal primaria de aceleración/desaceleración y al menos una
señal de corroboración. Calidad baja y banderas de sensores degradados reducen
el puntaje. Un batch repetido no vuelve a generar alertas porque la telemetría
se deduplica por `eventId` y la alerta conserva `sourceTelemetryEventId`.

- `severe` y `critical`: envío interno inmediato.
- `bump` y `moderate`: estado `Pendiente`, con `autoSendAtUtc` a 10 segundos.
- Confirmar que el usuario está bien cierra la alerta antes del despacho.

El motor es explicable y provisional; no es un diagnóstico médico ni sustituye
al futuro modelo de Machine Learning.
