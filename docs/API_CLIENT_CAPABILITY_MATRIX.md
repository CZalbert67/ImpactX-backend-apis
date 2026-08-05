# Matriz de capacidades por cliente — ImpactX API V1

> Fuente operativa para autorización de web, móvil y Galaxy Watch 8.
> `R` = lectura, `W` = escritura, `—` = no permitido.

## Principios

- Toda ruta se autentica y autoriza también en backend. Ocultar controles no es seguridad.
- Un JWT sin claim `client` no obtiene capacidades de escritura específicas.
- El wearable es el único cliente que controla el ciclo de vida de viajes y escribe telemetría.
- Web y móvil pueden consultar viajes, historial y telemetría.
- Plan, monitoreo, privacidad y contactos SOS se consolidan en un grupo; la prioridad SOS no crea otra relación.

## Matriz resumida

| Dominio / operación | Web | Mobile | Wearable | Regla principal |
|---|---:|---:|---:|---|
| Auth y sesiones | R/W | R/W | R/W limitada | Crear cuenta solo web/móvil; cada token incluye `client` firmado. |
| Perfil y onboarding | R/W | R/W | — | Propietario de la cuenta. |
| Ficha médica | R/W | R/W | — | Monitor solo con consentimiento explícito. |
| Plan y grupo | R/W | R/W | — | Solo el titular administra; cada miembro puede salir y conserva una cuota individual. |
| Vehículos | R/W | R/W | R selección | Cuota individual por plan. |
| Privacidad y monitoreo | R/W | R/W | — | Políticas direccionales entre integrantes del mismo grupo. |
| Contactos SOS | R/W | R/W | — | Prioridades entre integrantes; no conceden permisos adicionales. |
| Mensajes rápidos | R/W | R/W | — | Sin texto libre; relación con permiso. |
| Viajes: listar/activo/detalle | R | R | R | Solo recursos propios o relación autorizada. |
| Viajes: iniciar/pausar/reanudar/finalizar | — | — | W | Exclusivo Galaxy Watch 8. |
| Telemetría: consultar | R | R | R | Paginada y asociada a TripId. |
| Telemetría: PATCH/POST | — | — | W | Exclusivo wearable, idempotente. |
| Wearable: estado básico | R | R | R | Web solo consulta. |
| Wearable: vincular/configurar | — | W | W limitada | Nunca desde web. |
| Alertas e incidentes: consultar | R | R | R | Propietario o monitor autorizado. |
| Detección/cancelación crítica | — | — | W | Flujo local del wearable y auditoría. |
| Notificaciones | R/W | R/W | R | Firebase + historial interno. |
| Rutas | R/W | R/W | R | Wearable recibe ruta elegida, no administra. |
| Analytics/dashboard | R | R | — | Datos agregados, no control operativo. |


## Registro y onboarding

| Método y ruta | Web | Mobile | Wearable | Autenticación | Regla |
|---|---:|---:|---:|---:|---|
| `GET /api/v1/auth/registration-contract` | R | R | — | No | Publica versión y campos obligatorios. |
| `POST /api/v1/auth/register` | W | W | — | No | Frontend nuevo envía `registrationVersion=2`. |
| `GET /api/v1/profile/onboarding` | R | R | — | Sí | Estado y aceptaciones del propietario. |
| `PUT /api/v1/profile/onboarding` | W | W | — | Sí | Avance y consentimientos opcionales; no revoca aceptación legal histórica. |
| `POST /api/v1/profile/onboarding/legal-acceptance` | W | W | — | Sí | Acepta versiones legales vigentes de forma idempotente. |
| `GET/PUT /api/v1/profile/medical` | R/W | R/W | — | Sí | Ficha opcional; `Completed` o `Skipped`. |

## Rutas de viaje de escritura

| Método y ruta | Web | Mobile | Wearable |
|---|---:|---:|---:|
| `POST /api/v1/trips/start` | 403 | Relay validado | Permitido |
| `POST /api/v1/trips/{id}/pause` | 403 | Relay validado | Permitido |
| `POST /api/v1/trips/{id}/resume` | 403 | Relay validado | Permitido |
| `POST /api/v1/trips/{id}/finish` | 403 | Relay validado | Permitido |
| `PATCH /api/v1/trips/{id}/telemetry` | 403 | 403 | Permitido |
| `POST /api/v1/trips/{id}/telemetry` | 403 | 403 | Permitido |

## Compatibilidad

Los documentos históricos pueden conservar `controlClient` y
`mobileFallbackUsed`. Los viajes iniciados directamente por el wearable usan
`controlClient=wearable`, `mobileFallbackUsed=false` y `fallbackReason=null`.
Cuando el Galaxy Watch8 envía la orden por Bluetooth y el móvil la retransmite,
el backend exige que `dispositivoId` coincida con el wearable vinculado del
usuario y persiste `controlClient=wearable`, `mobileFallbackUsed=true`. El móvil
no obtiene control propio del viaje y continúa sin permiso para telemetría.

## Grupo unificado, privacidad, SOS y mensajes rápidos

| Método y ruta | Web | Mobile | Wearable | Autenticación | Regla |
|---|---:|---:|---:|---:|---|
| `GET /api/v1/family-subscriptions/current` | R | R | — | Sí | Devuelve el mismo grupo al titular y a todos los miembros activos. |
| `POST /api/v1/family-subscriptions/invitations` | W | W | — | Sí | Solo titular; una sola invitación integra al usuario al grupo. |
| `DELETE /api/v1/family-subscriptions/invitations/{id}` | W | W | — | Sí | Solo titular; revoca una invitación pendiente y libera el espacio. |
| `POST /api/v1/family-subscriptions/invitations/{id}/accept` | W | W | — | Sí | Suspende un Gratuito personal vacío y activa la membresía compartida. |
| `POST /api/v1/family-subscriptions/leave` | W | W | — | Sí | Solo miembro; reactiva o crea su plan Gratuito personal. |
| `GET /api/v1/family-subscriptions/members/access` | R | R | — | Sí | Políticas que el usuario propietario de datos concede a cada integrante. |
| `PUT /api/v1/family-subscriptions/members/{publicProfileId}/access` | W | W | — | Sí | Privacidad por persona, consentimiento médico y prioridad SOS. |
| `GET /api/v1/monitoring-relationships` | R | R | — | Sí | Proyección compatible de las políticas del grupo y relaciones legacy aceptadas. |
| `GET /api/v1/quick-messages/recipients` | R | R | — | Sí | Integrantes o relaciones aceptadas con `sendMessages=true`. |
| `POST /api/v1/quick-messages/send` | W | W | — | Sí | Sin texto libre; genera historial y notificación interna/push. |
| `PATCH /api/v1/quick-messages/conversations/{publicProfileId}/read` | W | W | — | Sí | Marca todos los mensajes entrantes de la conversación. |

Reglas del grupo:

- Gratuito: 2 personas totales y 1 vehículo por usuario.
- Estándar: 3 personas totales y 3 vehículos por usuario.
- Premium: 6 personas totales y sin límite comercial fijo de vehículos por usuario.
- Solo el titular invita, elimina miembros y administra el plan.
- Todos los integrantes quedan conectados, pero cada propietario de datos decide qué comparte con cada persona.
- La ficha médica exige consentimiento explícito.
- SOS es una prioridad sobre un integrante existente: 1 contacto en Gratuito, 2 en Estándar y 5 en Premium.

Las rutas V1 antiguas de `/api/v1/contacts/*` y creación manual de
`/api/v1/monitoring-relationships/invitations` permanecen temporalmente para
clientes anteriores. La web V1.4 no las utiliza para crear nuevas relaciones.

Las alertas se despachan primero por prioridad SOS y después al resto de
integrantes autorizados con `receiveCriticalAlerts=true` y
`receiveNotifications=true`. El historial interno se persiste antes del push.

## Galaxy Watch 8 y telemetría V2

| Método y ruta | Web | Mobile | Wearable | Autenticación | Regla |
|---|---:|---:|---:|---:|---|
| `GET /api/v1/wearable` | R | R | R | Sí | Consulta del wearable vinculado. |
| `GET /api/v1/wearable/all` | R | R | R | Sí | Historial paginado de vinculaciones. |
| `POST /api/v1/wearable/pair` | 403 | W | 403 | Sí | Solo móvil; únicamente Samsung Galaxy Watch 8 con WearOS. |
| `POST /api/v1/wearable/pair/confirm` | 403 | W | 403 | Sí | Código temporal de diez minutos; solo se persiste su hash. |
| `DELETE /api/v1/wearable/unlink` | 403 | W | 403 | Sí | Desvinculación administrada por móvil. |
| `PUT /api/v1/wearable/permissions` | 403 | W | 403 | Sí | Configuración desde móvil. |
| `POST /api/v1/wearable/heartbeat` | 403 | 403 | W | Sí | Estado, batería, versiones, reloj y capacidades. |
| `PATCH /api/v1/wearable/battery` | 403 | 403 | W | Sí | Actualización operativa de batería. |
| `POST /api/v1/wearable/sensors/diagnostics` | 403 | 403 | W | Sí | Reporta sensores disponibles/no disponibles y calidad. |
| `GET /api/v1/wearable/sensors/diagnostics` | R | R | R | Sí | Lectura del último diagnóstico real. |
| `POST /api/v1/wearable/calibration` | 403 | W | W | Sí | Calibrado válido requiere acelerómetro, giroscopio y GPS. |
| `POST /api/v1/wearable/sync` | 403 | 403 | W | Sí | Sincronización legacy de estado; no sustituye telemetría de viaje. |
| `POST /api/v1/trips/{id}/telemetry` esquema V2 | 403 | 403 | W | Sí | Lote idempotente de 1–100 eventos, máximo 256 KiB. |
| `GET /api/v1/trips/{id}/telemetry` | R | R | R | Sí | Lectura paginada de sensores y anotaciones de servidor. |

El esquema V2 requiere `batchId`, `batchSequence`, procedencia del Galaxy Watch
8, batería, secuencia por evento, precisión GPS, acelerómetro, giroscopio y
calidad. Biometría y orientación son opcionales porque su disponibilidad puede
variar por permisos, estado del sensor o condiciones de captura.

## Mobile bootstrap y motor de impacto

| Método y ruta | Web | Mobile | Wearable | Autenticación | Regla |
|---|---:|---:|---:|---:|---|
| `GET /api/v1/mobile/sync/bootstrap` | 403 | R | 403 | Sí | Snapshot de lectura; no concede control del viaje ni escritura de telemetría. |
| `POST /api/v1/alerts/detect` | 403 | 403 | W legacy | Sí | Compatibilidad wearable; el flujo normal nace del motor de reglas del backend. |
| `POST /api/v1/alerts/sos` | — | W | W | Sí | SOS manual; no equivale a etiquetado automático. |
| `POST /api/v1/alerts/{id}/confirm-ok` | — | W | W | Sí | Cancela una alerta pendiente/activa antes de despacho cuando aplique. |

La telemetría V2 se etiqueta dentro del backend con `impact-rules-v1`. Los
campos `impactCandidate`, `detectionLabel`, `severityLabel`, `ruleVersion`,
`detectionScore`, `modelVersion` y `labeledAtUtc` son de solo servidor.

## Finalización funcional V8

| Método y ruta | Web | Mobile | Wearable | Regla |
|---|---:|---:|---:|---|
| `GET /api/v1/mobile/sync/changes` | 403 | R | 403 | Cursor estable; devuelve snapshot solo cuando cambió el estado. |
| `POST /api/v1/mobile/sync/push` | 403 | W | 403 | Máximo 50 operaciones; idempotencia por `operationId`. |
| `POST /api/v1/mobile/sync/ack` | 403 | W | 403 | Solo reconoce el cursor vigente; obsoleto responde 409. |
| `GET /api/v1/subscriptions/effective` | R | R | 403 | Free/Standard/Premium y beneficios efectivos, incluidos planes familiares. |
| `POST /api/v1/subscriptions/activate` | W | W | 403 | Pago simulado; Standard se almacena como Basic por compatibilidad. |
| `POST /api/v1/subscriptions/renew` | W | W | 403 | Extiende el periodo y registra pago completado. |
| `GET /api/v1/incidents/active` | R | R | 403 | Incidentes propios pendientes, enviados o activos. |
| `POST /api/v1/incidents/{id}/confirm-ok` | 403 | W | 403 | Confirmación del usuario desde móvil. |
| `POST /api/v1/incidents/{id}/close` | W | W | 403 | Cierre auditado; no elimina el incidente. |
| `GET /api/v1/account/export` | R | R | 403 | Exportación de datos propios. |
| `GET /api/v1/account/retention` | R | R | 403 | Política y estado de eliminación. |
| `POST /api/v1/account/consents/revoke` | W | W | 403 | Revocación granular y eliminación opcional de ficha médica. |
| `DELETE /api/v1/account` | W | W | 403 | Contraseña + confirmación `DELETE`; anonimización inmediata. |

## Contrato congelado V9

| Método y ruta | Web | Mobile | Wearable | Autenticación | Regla |
|---|---:|---:|---:|---:|---|
| `GET /api/v1/meta/contract` | R | R | R | No | Contrato congelado, módulos, retención y rutas efectivas de V1. |
| `GET /api/v1/meta/clients/web` | R | R | R | No | Capacidades del JWT web. |
| `GET /api/v1/meta/clients/mobile` | R | R | R | No | Capacidades del JWT móvil. |
| `GET /api/v1/meta/clients/wearable` | R | R | R | No | Capacidades del JWT wearable. |

Versión del contrato: `2026.08.05`. Todas las respuestas incluyen headers de
versión. Las rutas legacy permanecen únicamente para transición y tienen sunset
el `2027-02-02T00:00:00Z`.
