# Ciclo de cuenta V2

## Rutas

- `GET /api/v1/account/export`
- `GET /api/v1/account/retention`
- `POST /api/v1/account/consents/revoke`
- `DELETE /api/v1/account`

Solo web y móvil. Wearable recibe 403.

## Exportación

La exportación devuelve la información propia de la cuenta: perfil, plan efectivo,
historial y pagos, familia, wearable, vehículos, contactos, monitoreo, viajes,
telemetría, alertas, incidentes, notificaciones y mensajes rápidos.

## Consentimientos

El usuario puede revocar por separado el uso de ubicación para incidentes y el
procesamiento de patrones de conducción. También puede eliminar la ficha médica
opcional. La aceptación legal histórica no se borra ni se reescribe.

## Eliminación

Requiere contraseña actual y confirmación literal `DELETE`. El backend revoca
sesiones y dispositivos, desactiva la cuenta y anonimiza inmediatamente nombre,
username, identificadores públicos, correo, teléfono, tokens, perfiles sensibles
y recibos de sincronización.

Los registros de dominio siguen su política de retención:

- viajes y telemetría: 90 días;
- alertas e incidentes: 365 días;
- notificaciones: 30 días.

No se ejecutan borrados masivos destructivos sobre Cosmos durante la solicitud.
