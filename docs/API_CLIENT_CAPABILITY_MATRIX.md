# Matriz de capacidades por cliente — ImpactX API V1

> Fuente operativa para autorización de web, móvil y Galaxy Watch 8.
> `R` = lectura, `W` = escritura, `—` = no permitido.

## Principios

- Toda ruta se autentica y autoriza también en backend. Ocultar controles no es seguridad.
- Un JWT sin claim `client` no obtiene capacidades de escritura específicas.
- El wearable es el único cliente que controla el ciclo de vida de viajes y escribe telemetría.
- Web y móvil pueden consultar viajes, historial y telemetría.
- Familia, monitoreo y contacto de emergencia son relaciones independientes.

## Matriz resumida

| Dominio / operación | Web | Mobile | Wearable | Regla principal |
|---|---:|---:|---:|---|
| Auth y sesiones | R/W | R/W | R/W limitada | Crear cuenta solo web/móvil; cada token incluye `client` firmado. |
| Perfil y onboarding | R/W | R/W | — | Propietario de la cuenta. |
| Ficha médica | R/W | R/W | — | Monitor solo con consentimiento explícito. |
| Planes y familia | R/W | R/W | — | Solo propietario paga/administra; miembro puede salir. |
| Vehículos | R/W | R/W | R selección | Cuota individual por plan. |
| Monitoreo y permisos | R/W | R/W | — | Relación aceptada, vigente y no bloqueada. |
| Contactos internos | R/W | R/W | — | Deben ser usuarios ImpactX aceptados. |
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
| `POST /api/v1/trips/start` | 403 | 403 | Permitido |
| `POST /api/v1/trips/{id}/pause` | 403 | 403 | Permitido |
| `POST /api/v1/trips/{id}/resume` | 403 | 403 | Permitido |
| `POST /api/v1/trips/{id}/finish` | 403 | 403 | Permitido |
| `PATCH /api/v1/trips/{id}/telemetry` | 403 | 403 | Permitido |
| `POST /api/v1/trips/{id}/telemetry` | 403 | 403 | Permitido |

## Compatibilidad

Los documentos históricos pueden conservar `controlClient` y
`mobileFallbackUsed`. Para viajes nuevos, `controlClient=wearable`,
`mobileFallbackUsed=false` y `fallbackReason=null`.

## Contactos internos de emergencia

| Método y ruta | Web | Mobile | Wearable | Autenticación | Regla |
|---|---:|---:|---:|---:|---|
| `GET /api/v1/contacts` | R | R | — | Sí | Lista relaciones propias, entrantes y preinvitaciones dirigidas al correo de la cuenta. |
| `GET /api/v1/contacts/{id}` | R | R | — | Sí | Solo propietario o destinatario; `id` es `PublicContactId`. |
| `POST /api/v1/contacts/invitations` | W | W | — | Sí | Exactamente username, PublicProfileId o email; devuelve el código manual una sola vez. |
| `POST /api/v1/contacts/invitations/accept` | W | W | — | Sí | Solo el destinatario; body con `publicContactId` o `code`, nunca query string. |
| `POST /api/v1/contacts/invitations/reject` | W | W | — | Sí | Rechazo explícito e invalidación del código. |
| `PATCH /api/v1/contacts/{id}` | W | W | — | Sí | Solo propietario; parentesco y prioridad. |
| `PATCH /api/v1/contacts/{id}/primary` | W | W | — | Sí | Solo relaciones `Accepted`; un principal por propietario. |
| `POST /api/v1/contacts/{id}/block` | W | W | — | Sí | Participante; impide nuevas invitaciones entre las cuentas. |
| `DELETE /api/v1/contacts/{id}` | W | W | — | Sí | Revocación lógica por cualquiera de las partes. |
| `GET /api/v1/contacts/sync` | R | R | — | Sí | Snapshot para sincronización; no expone teléfono, ids internos ni hashes. |

La ruta legacy `/api/contacts` conserva temporalmente el CRUD histórico de
nombre/teléfono, pero esos documentos son `LegacyUnverified`, quedan fuera de
OpenAPI V1 y no se consideran contactos operativos.

## Monitoreo, permisos y mensajes rápidos

| Método y ruta | Web | Mobile | Wearable | Autenticación | Regla |
|---|---:|---:|---:|---:|---|
| `GET /api/v1/monitoring-relationships` | R | R | — | Sí | Fuente canónica de relaciones propias; las invitaciones pendientes vencidas pasan a `Expired`. |
| `POST /api/v1/monitoring-relationships/invitations` | W | W | — | Sí | Invitación de siete días; código manual de un solo uso almacenado como hash. |
| `POST /api/v1/monitoring-relationships/invitations/accept` | W | W | — | Sí | Solo destinatario; cupo por plan del monitor. |
| `POST /api/v1/monitoring-relationships/invitations/reject` | W | W | — | Sí | Solo destinatario; invalida el código. |
| `PATCH /api/v1/monitoring-relationships/{id}/permissions` | W | W | — | Sí | Solo la persona monitoreada concede permisos; ficha médica exige consentimiento explícito. |
| `POST /api/v1/monitoring-relationships/{id}/block` | W | W | — | Sí | Bloquea la relación y elimina todos los permisos. |
| `DELETE /api/v1/monitoring-relationships/{id}` | W | W | — | Sí | Revocación lógica por cualquiera de los participantes. |
| `GET /api/v1/quick-messages/recipients` | R | R | — | Sí | Solo participantes de relaciones `Accepted` con `sendMessages=true`. |
| `GET /api/v1/quick-messages/templates` | R | R | — | Sí | Ocho plantillas del sistema y máximo diez personalizadas activas. |
| `POST /api/v1/quick-messages/send` | W | W | — | Sí | Sin texto libre; usa snapshot inmutable de una plantilla aprobada. |
| `GET /api/v1/quick-messages/history` | R | R | — | Sí | Historial propio, opcionalmente filtrado por perfil público. |
| `PATCH /api/v1/quick-messages/{id}/read` | W | W | — | Sí | Solo el destinatario. |
| `GET /api/v1/permissions` | R | R | — | Sí | Estado de permisos técnicos por plataforma. |
| `PUT /api/v1/permissions/mobile` | — | W | — | Sí | Solo un token `client=mobile`. |
| `PUT /api/v1/permissions/web` | W | — | — | Sí | Solo un token `client=web`. |

Las alertas se despachan exclusivamente a relaciones canónicas `Accepted`
que tengan simultáneamente `receiveCriticalAlerts=true` y
`receiveNotifications=true`. El contenedor legacy `Monitores` deja de ser
fuente de destinatarios para Firebase.

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

Versión del contrato: `2026.08.04`. Todas las respuestas incluyen headers de
versión. Las rutas legacy permanecen únicamente para transición y tienen sunset
el `2027-02-02T00:00:00Z`.
