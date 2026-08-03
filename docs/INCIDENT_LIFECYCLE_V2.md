# Ciclo de incidentes V2

## Fuente

Toda alerta manual, offline o creada por `impact-rules-v1` mantiene un incidente
asociado por `alertaId`. El upsert evita duplicados durante reintentos y conserva
la trazabilidad del evento de telemetría.

## Estados y auditoría

El incidente refleja estado, severidad, ubicación, viaje, `sourceTelemetryEventId`,
etiqueta de detección, versión de regla, puntaje, línea de tiempo, destinatarios,
confirmación y cierre.

Estados operativos de lectura activa: `Pendiente`, `Enviada` y `Activa`.
Confirmar que el usuario está bien lo registra como falsa alarma. Cerrar conserva
método y nota de cierre.

## Rutas

- `GET /api/v1/incidents`
- `GET /api/v1/incidents/active`
- `GET /api/v1/incidents/{id}`
- `POST /api/v1/incidents/{id}/confirm-ok` — solo móvil
- `POST /api/v1/incidents/{id}/close` — web o móvil
- `PATCH /api/v1/incidents/{id}/mark-false-alarm`
- `PATCH /api/v1/incidents/{id}/note`
- `GET /api/v1/incidents/{id}/map`
- `GET /api/v1/incidents/export`

El wearable no administra incidentes. El mapa y la exportación detallada requieren
las capacidades del plan Premium.

## Retención

Cada documento nuevo de incidente usa TTL de 365 días. El contenedor existente
permanece sin cambios estructurales y conserva compatibilidad con documentos
históricos.
