# Mobile sync bootstrap

## Endpoint

```http
GET /api/v1/mobile/sync/bootstrap
Authorization: Bearer <JWT client=mobile>
```

Web y wearable reciben `403`. El endpoint es de lectura y devuelve un snapshot
completo para reconstruir el estado local después de login, reinstalación o
recuperación de conectividad.

## Contenido

- contrato, `snapshotId` y hora UTC del servidor;
- perfil y onboarding;
- permisos móviles/web;
- estado del Galaxy Watch 8;
- viaje activo en modo lectura;
- vehículos;
- contactos internos de emergencia;
- relaciones de monitoreo;
- plantillas y destinatarios de mensajes rápidos;
- contadores de notificaciones y mensajes no leídos;
- contrato offline vigente.

## Contrato offline

- telemetría V2: máximo 100 eventos y 256 KiB;
- escritor de telemetría: wearable;
- control del viaje desde móvil: no permitido;
- lectura de viaje desde móvil: permitida;
- clave idempotente: `eventId`;
- mismo contenido: duplicado seguro;
- contenido diferente con el mismo `eventId`: `409 Conflict`;
- el móvil puede retransmitir alertas capturadas offline mediante el endpoint
  específico, pero no fabricar etiquetas de impacto.

El snapshot se genera secuencialmente para mantener compatibilidad con el
`DbContext` scoped de EF durante pruebas. No crea un contenedor Cosmos nuevo.
