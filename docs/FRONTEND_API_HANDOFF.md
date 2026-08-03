# ImpactX — entrega de API para frontend

## Contrato congelado

- Base canónica: `/api/v1`
- Versión: `2026.08.05`
- OpenAPI: `/openapi/v1.json`
- Contrato JSON para clientes: `/api/v1/meta/contract`
- Capacidades por cliente: `/api/v1/meta/clients/web`, `/mobile`, `/wearable`

Cada respuesta expone:

```text
X-ImpactX-Api-Version: v1
X-ImpactX-Contract-Version: 2026.08.05
X-Correlation-Id: <id>
```

El frontend nuevo no debe consumir rutas `/api/*` sin `/v1`. Esas rutas son de compatibilidad temporal y responden con `Deprecation`, `Sunset`, `Warning`, `Link` y `X-ImpactX-Legacy-Route`.

## Autenticación

1. Registrar o iniciar sesión con `client: "web"`.
2. Guardar el access token únicamente en memoria o almacenamiento protegido.
3. Enviar `Authorization: Bearer <token>`.
4. Renovar mediante `POST /api/v1/auth/refresh`.
5. Ante `401`, renovar una vez y repetir la solicitud; si vuelve a fallar, cerrar sesión.
6. Un `403` indica que el token es válido, pero el tipo de cliente no puede ejecutar esa operación.

## Códigos de error

Los errores usan `application/problem+json` e incluyen `status`, `title`, `detail`, `traceId` y `correlationId`.

- `400`: validación o contrato incorrecto.
- `401`: falta token o expiró.
- `403`: capacidad de cliente insuficiente.
- `404`: recurso inexistente o no visible para el usuario.
- `409`: conflicto, duplicado o transición inválida.
- `429`: límite temporal; respetar `Retry-After`.
- `503`: dependencia crítica no disponible.

## Paginación

Los listados paginados usan:

```text
?pageSize=20&continuationToken=<token-opaco>
```

El siguiente cursor se devuelve en `X-Continuation-Token`. Nunca debe generarse ni modificarse en el frontend.

## Módulos web

| Pantalla | Endpoints principales |
|---|---|
| Inicio de sesión | `POST /api/v1/auth/login`, `POST /api/v1/auth/refresh`, `POST /api/v1/auth/logout` |
| Perfil | `GET/PUT /api/v1/profile`, `/preferences`, `/driver`, `/medical`, `/onboarding` |
| Vehículos | `GET/POST /api/v1/vehicles`, `GET/PUT/DELETE /api/v1/vehicles/{publicVehicleId}`, `PATCH .../primary` |
| Plan | `GET /api/v1/plans`, `GET /api/v1/subscriptions/effective`, `POST .../activate`, `renew`, `cancel` |
| Familia | `/api/v1/family-subscriptions/*`, incluida `GET .../invitations/incoming` |
| Contactos | `/api/v1/contacts/*` |
| Monitoreo | `/api/v1/monitoring-relationships/*` |
| Viajes | `GET /api/v1/trips`, `GET /api/v1/trips/active`, `GET /api/v1/trips/{id}/telemetry` |
| Incidentes | `/api/v1/incidents`, `/active`, `/{id}`, `/{id}/close`, `/{id}/note`, `/{id}/map` |
| Alertas | `GET /api/v1/alerts`, `GET /api/v1/alerts/{id}` |
| Notificaciones | `/api/v1/notifications/*` |
| Mensajes rápidos | `/api/v1/quick-messages/*` |
| Analítica | `/api/v1/analytics/dashboard`, `/incidents/trend`, `/trips/summary` |
| Cuenta | `GET /api/v1/account/export`, `GET /api/v1/account/retention`, `DELETE /api/v1/account` |

## Restricciones importantes

El cliente web es de consulta para wearable y viajes. No puede iniciar, pausar, reanudar o finalizar viajes ni escribir telemetría. Esas operaciones requieren un JWT con `client=wearable`.

El móvil puede vincular y desvincular el Galaxy Watch 8, sincronizar operaciones offline y confirmar que el usuario está bien. El wearable controla el viaje, emite heartbeat, diagnóstico, batería y telemetría.

## Integración recomendada

Genera tipos TypeScript desde `/openapi/v1.json` y conserva el JSON de `/api/v1/meta/contract` como comprobación de versión al arrancar. Si `contractVersion` no coincide con `2026.08.05`, bloquea funciones de escritura hasta revisar el contrato.

## Relaciones y conversaciones (2026.08.05)

- La capacidad familiar se obtiene de `totalActivePeople` y `totalPeopleLimit`; no debe calcularse solo con datos locales.
- `currentUserRole=Member` oculta todas las operaciones de cambio, renovación y cancelación de plan.
- `direction=MonitoredRequestsMonitor` significa que el usuario actual pide a otra persona que lo monitoree.
- `direction=MonitorInvitesMonitored` significa que el usuario actual solicita monitorear a la otra persona.
- Al abrir una conversación se usa `PATCH /api/v1/quick-messages/conversations/{otherPublicProfileId}/read`.
