# Consolidación de monitoreo, permisos, mensajes y notificaciones

## Fuente canónica

`MonitoringRelationships` es la única fuente operativa para determinar quién
monitorea a quién. Una relación válida requiere:

- estado `Accepted`;
- `MonitorUserId` y `MonitoredUserId` vinculados a cuentas ImpactX;
- no estar revocada, bloqueada ni expirada;
- permisos específicos concedidos por la persona monitoreada.

El contenedor `Monitores` se conserva solo por compatibilidad histórica. Sus
registros no autorizan alertas, acceso a recursos ni mensajes rápidos nuevos.
No se borra ni migra automáticamente ningún documento legacy.

## Alertas y Firebase

El envío de una alerta consulta relaciones aceptadas donde el usuario de la
alerta es `MonitoredUserId`. Solo se incluyen relaciones con:

- `ReceiveCriticalAlerts = true`;
- `ReceiveNotifications = true`.

Se deduplican destinatarios por `MonitorUserId`. La clave idempotente sigue el
formato `alert:{alertId}:recipient:{monitorUserId}:channel:push`. El historial
de notificación conserva `PublicRelationshipId` para trazabilidad sin exponer
GUID internos en la API.

## Invitaciones

Las invitaciones duran siete días. Cuando una relación pendiente se consulta
después de su vencimiento, el backend persiste `Expired`, elimina el hash del
código y rechaza cualquier aceptación posterior.

## Permisos técnicos

Los permisos de plataforma no son permisos de monitoreo:

- `/permissions/mobile` requiere `client=mobile`;
- `/permissions/web` requiere `client=web`;
- un token de otra plataforma recibe `403`.

## Mensajes rápidos

`GET /api/v1/quick-messages/recipients` devuelve exclusivamente destinatarios
con una relación aceptada y permiso `SendMessages`. El envío continúa sin texto
libre: únicamente se admiten plantillas del sistema o plantillas personalizadas
del remitente, con snapshot inmutable en el historial.

## Compatibilidad y despliegue

Este cambio no crea contenedores, no altera partition keys y no ejecuta una
migración destructiva. Producción continúa usando `ValidateOnly` para Cosmos.
