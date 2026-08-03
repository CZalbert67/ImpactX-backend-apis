# Ciclo de suscripciones V2

## Nombres públicos y compatibilidad

La API publica `Free`, `Standard` y `Premium`. Cosmos conserva `Basic` como nombre
de almacenamiento histórico para el plan público `Standard`; no existe migración
destructiva.

## Suscripción individual

- La cuenta nueva recibe Free permanente, no un trial.
- Standard y Premium se activan con pago simulado mensual o anual.
- Un upgrade reemplaza lógicamente la suscripción anterior.
- La renovación registra un pago simulado completado y extiende el periodo.
- Al vencer, entra en `Grace` durante tres días.
- Al terminar la gracia, pasa a `Expirada` y el usuario vuelve a Free.

Rutas V1:

- `GET /api/v1/subscriptions`
- `GET /api/v1/subscriptions/effective`
- `GET /api/v1/subscriptions/history`
- `POST /api/v1/subscriptions/activate`
- `POST /api/v1/subscriptions/renew`
- `POST /api/v1/subscriptions/cancel`
- `GET /api/v1/subscriptions/payments`
- `GET /api/v1/subscriptions/payments/{id}/receipt`

## Suscripción familiar

- La capacidad publicada incluye al propietario.
- Free permite 2 personas totales, Standard 3 y Premium 6.
- Esto equivale a 1, 2 y 5 integrantes invitados respectivamente.
- Las invitaciones pendientes reservan espacio para evitar sobrecupo.
- Los integrantes activos heredan el plan y su cuota individual de vehículos.
- Al vencer, el plan pasa a `PastDue` y conserva tres días de gracia.
- Al expirar la gracia, las membresías activas terminan, las invitaciones
  pendientes expiran y todos los integrantes vuelven a Free.
- Una renovación durante gracia restaura `Active`.

Rutas de lectura de invitaciones:

- `GET /api/v1/family-subscriptions/invitations`: invitaciones enviadas por el titular.
- `GET /api/v1/family-subscriptions/invitations/incoming`: invitaciones pendientes dirigidas al usuario autenticado.

El frontend puede consultar la ruta de entrada de forma periódica; aceptar o
rechazar una invitación invalida inmediatamente el resumen, miembros e
invitaciones. No se requiere crear un contenedor Cosmos adicional.

## Proceso automático

`SubscriptionLifecycleWorker` procesa suscripciones individuales y familiares.
Está deshabilitado por defecto en desarrollo/pruebas y habilitado en Production
cada 15 minutos. La lectura de plan efectivo también aplica el ciclo de forma
perezosa para evitar beneficios vencidos entre ejecuciones del worker.

## Restricciones

Un integrante que recibe beneficios familiares no puede contratar simultáneamente
un plan individual pagado. No se crean ni modifican contenedores Cosmos.
