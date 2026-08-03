# Mobile Offline Sync V2

## Objetivo

Contrato definitivo para que la aplicación móvil recupere estado, envíe cambios
hechos sin conexión y confirme el cursor aplicado sin obtener control de viajes
o telemetría.

## Rutas

- `GET /api/v1/mobile/sync/bootstrap`
- `GET /api/v1/mobile/sync/changes?cursor=...`
- `POST /api/v1/mobile/sync/push`
- `POST /api/v1/mobile/sync/ack`

Todas requieren JWT con `client=mobile`. Web y wearable reciben 403.

## Snapshot

El snapshot V2 contiene perfil, onboarding, permisos, plan efectivo, Galaxy Watch
8, viaje activo, vehículos, contactos internos, relaciones de monitoreo,
incidentes activos, mensajes rápidos y contadores de no leídos. El cursor se
calcula sobre contenido estable; identificadores y tiempos generados para la
respuesta no alteran el hash.

## Push e idempotencia

Cada operación usa un `operationId` UUID. El backend conserva hasta 200 recibos
por usuario y acepta hasta 50 operaciones por lote. Repetir el mismo UUID devuelve
el resultado previo con `wasDuplicate=true`; no repite la escritura.

Operaciones soportadas:

- notificaciones: marcar una o todas como leídas;
- mensajes rápidos: marcar leído y enviar una plantilla permitida;
- permisos, token FCM, perfil, preferencias y onboarding;
- vehículos: crear, actualizar, eliminar y establecer principal.

Errores de dominio esperados se registran como `rejected`. Errores inesperados no
se ocultan y mantienen el comportamiento HTTP estándar.

## Conflictos

`baseCursor` permite detectar que el móvil trabajó sobre una versión anterior.
El lote puede aplicarse de manera idempotente y la respuesta indica
`requiresPull=true` para que el cliente recupere el snapshot actual.

`ack` solo acepta el cursor vigente; un cursor obsoleto responde 409.

## Límites de seguridad

El móvil no puede iniciar, pausar, reanudar o finalizar viajes ni escribir
telemetría. Esas operaciones siguen siendo exclusivas del Galaxy Watch 8.
