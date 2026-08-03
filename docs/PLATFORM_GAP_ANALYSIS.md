# Platform Gap Analysis — ImpactX backend

> Auditoría documental del backend (`ImpactXv1/`) contra la fuente de verdad
> `docs/PRODUCT_SCOPE_CANONICAL.md`.
>
> - Rama: `docs/platform-alignment-audit`.
> - No se ha modificado código, pruebas, OpenAPI, configuración, workflows,
>   Azure ni ningún otro archivo que no sean los dos documentos autorizados
>   (`PRODUCT_SCOPE_CANONICAL.md` y este análisis).
> - No se ha ejecutado ninguna operación de control de versiones
>   (add/commit/push/pull/merge/rebase/reset/restore/clean/stash).

---

## 1. Resumen ejecutivo

El backend cumple bien la **base autenticada**: JWT + refresh tokens con
rotación, ProblemDetails (RFC 7807), correlation ID, rate limiting por
políticas, paginación con continuation token, idempotencia de telemetría por
`EventId` y persistencia EF InMemory + Cosmos. Sin embargo, la alineación con
el alcance canónico es **parcial** y hay **cuatro brechas estructurales** que
impiden declarar el backend completo:

1. **Identidad pública ausente.** No existe `PublicProfileId`; el `username`
   se genera pero no se normaliza ni se puede modificar; no se reservan
   usernames anteriores; el login solo acepta correo; y los DTOs exponen
   primary keys y campos internos (`Id`, `AppId`, `InviteCode`) en la mayoría
   de respuestas (perfil, búsqueda, auth, monitores, viajes, wearables,
   alertas, pagos).
2. **Faltan los dominios de vehículos y de suscripción familiar.** No existe
   entidad `Vehiculo` (los datos de vehículo viven embebidos en
   `PerfilConduccion`, con campos no aprobados como `Placa`/`Color`, en
   singular). No existe membresía familiar, propietario/integrantes, código
   manual seguro, canje de un solo uso ni reducción de plan.
3. **Falta el módulo de mensajes rápidos (nuevo modelo P0).** No hay plantillas
   del sistema ni personalizadas, ni historial, ni contador de no leídos, ni
   endpoints. `ChatThread` (entidad y contenedor `ChatThreads`) existe pero
   está **muerto**: sin repositorio, sin servicio y sin endpoint, y representa
   el modelo de chat dinámico que el canon descarta.
4. **Sin autorización por capacidad de cliente.** Todo endpoint con
   `[Authorize]` acepta a *cualquier cliente autenticado*. No hay claim ni
   policy de cliente (web / móvil / wearable). Por tanto, **la web puede
   técnicamente** iniciar/pausar/reanudar/finalizar viajes, enviar telemetría,
   vincular wearables y configurar sensores, en contradicción con el canon
   («La aplicación web NO puede» — sección 4).

Relaciones de monitoreo, incidentes/alertas/notificaciones, contactos y
wearable existen, pero con **contratos simplificados**: sin dirección explícita
de la relación, sin estados completos, sin permisos granulares ni
consentimiento de ficha médica, sin integración de cupos por plan del
propietario, contactos externos por teléfono (prohibidos), y `Wearable.Sync`
sin persistencia ni idempotencia.

Las **837 pruebas** actuales cubren bien la base técnica, pero **no** cubren
identidad pública, vehículos, suscripción familiar, mensajes rápidos ni la
restricción de capacidades web. Se proponen **cuatro pull requests** de
implementación backend (sección 12) con pruebas y sin tocar CI/CD ni Azure.

---

## 2. Inventario de endpoints actuales agrupados por dominio

Convención: **[E]** endpoint existente · **[P]** endpoint propuesto ·
**[C]** debe conservarse · **[R]** debe restringirse su autorización.

### 2.1 Identidad — `AuthController`, `UsersController`

| Endpoint | Tipo | Nota |
|---|---|---|
| `POST /api/v1/auth/register` | [E][C] | Crea cuenta; genera `username` y `AppId`, no `PublicProfileId`. |
| `POST /api/v1/auth/login` | [E] | Solo correo; debe aceptar también `username`. |
| `POST /api/v1/auth/recover-password` · `reset-password` | [E][C] | Token con hash (`PasswordResetTokenHasher`). |
| `POST /api/v1/auth/change-password` | [E][C] | |
| `POST /api/v1/auth/logout` · `refresh` · `GET /sessions` · `DELETE /sessions/{id}` | [E][C] | Refresh con rotación. |
| `DELETE /api/v1/auth/account` · `GET /auth/account/export` | [E][C] | Export expone `Id` (interno). |
| `GET /api/v1/profile/search` | [E][R?] | Expone `Id`/`AppId`; sin rate limiting; sin búsqueda por `PublicProfileId`. |
| `GET/PUT /api/v1/profile` · `preferences` · `driver` · `medical` | [E][C] | Sin cambio de username ni estado de onboarding. |

### 2.2 Onboarding, perfil y ficha médica — `UsersController`
*(Mismos endpoints de 2.1.)* Falta estado de onboarding, registro de ficha
completada/omitida y consentimientos como recurso explícito.

### 2.3 Vehículos — **ausente**
No existen endpoints. Propuesta: CRUD de colección de vehículos (sección 5).

### 2.4 Suscripción familiar — `PlansController`, `SubscriptionController`
| Endpoint | Tipo | Nota |
|---|---|---|
| `GET /api/v1/plans` (anónimo) | [E][C] | Lista de planes. |
| `GET /api/v1/subscriptions` · `history` · `payments` | [E][C] | No hay membresías. |
| `POST /api/v1/subscriptions/change-plan` | [E][R] | Solo upgrade; no admite reducción de plan. |
| `POST /api/v1/subscriptions/cancel` | [E][C] | |
| `GET /api/v1/subscriptions/payments/{id}/receipt` | [E][C] | Propiedad por punto de lectura. |
| `POST /api/v1/subscriptions/expire` | [E][R] | Proceso batch administrativo, hoy invocable por cualquier usuario. |
| *(propuesta)* unirse por código / membresías / integrantes / canje | [P] | *(sección 5)* |

### 2.5 Relaciones de monitoreo — `MonitorsController`
| Endpoint | Tipo | Nota |
|---|---|---|
| `GET /api/v1/monitors` | [E][C] | Lista solo de la red del propietario (quien invita). |
| `POST /api/v1/monitors/invite` | [E][R] | Cupo según plan (hoy Free=0/Basic=2/Premium=6). |
| `POST {id}/resend` · `POST {id}/restore` · `DELETE {id}` | [E][C] | Dirección implícita. |
| `POST /api/v1/monitors/invite/details` (anónimo) | [E][C] | Expone `UsuarioId` interno. |
| `POST /api/v1/monitors/invite/accept` · `reject` | [E][C] | Token en cuerpo JSON, no en URL. |

### 2.6 Contactos (emergencia) — `ContactsController`
| Endpoint | Tipo | Nota |
|---|---|---|
| `GET/POST/PUT/DELETE /api/v1/contacts`, `GET {id}`, `PATCH make-primary`, `GET sync` | [E][R?] | Permite contactos externos (teléfono), contradice el alcance (solo internos). |

### 2.7 Mensajes rápidos — **ausente**
No existen endpoints. La única entidad colindante (`ChatThread`) está sin uso.

### 2.8 Viajes, rutas y telemetría — `TripsController`, `RoutesController`
| Endpoint | Tipo | Nota |
|---|---|---|
| `GET /api/v1/trips` · `GET {id}/telemetry` | [E][C] | Lectura paginada (web ok). |
| `GET /api/v1/trips/active` | [E][C] | Lectura. |
| `POST /api/v1/trips/start` · `{id}/pause` · `{id}/resume` · `{id}/finish` | [E][R] | **Hoy la web puede**; debe restringirse a wearable/móvil. |
| `PATCH {id}/telemetry` · `POST {id}/telemetry` | [E][R] | **Hoy la web puede**; restringir. |
| Rutas `frequent`, `history`, `select-today` | [E][C] | Lectura y preferencia. |

### 2.9 Dispositivos y wearable — `DevicesController`, `WearableController`
| Endpoint | Tipo | Nota |
|---|---|---|
| `GET /api/v1/devices` · `GET /api/v1/wearable/all` | [E][C] | Lectura web según canon. |
| `PUT /api/v1/devices/fcm-token` · `DELETE ...` | [E][C] | Dispositivos móvil/web autenticados. |
| `POST /api/v1/wearable/pair` · `pair/confirm` · `calibration` · `permissions` · `battery` | [E][R] | Deben restringirse a solo móvil/wearable. |
| `POST /api/v1/wearable/sync` | [E][R] | No idempotente y no asocia `TripId`. |

### 2.10 Incidentes, alertas y notificaciones — `AlertasController`, `IncidentesController`, `NotificacionesController`
| Endpoint | Tipo | Nota |
|---|---|---|
| `POST /api/v1/alerts/detect` · `sos` · `{id}/confirm-ok` (cancel. 10 s) · `bypass-critical` · `retry` · `close` · `sync-offline` | [E][R] | Escrituras de alerta, no disponibles para web. |
| `GET /api/v1/alerts` · `GET /api/v1/alerts/{id}` | [E][C] | Lectura (validar lectura de monitores autorizados). |
| `GET /api/v1/incidents` · `GET {id}` · `{id}/map` · `export` · PATCH `/mark-false-alarm` · `/note` | [E][C] | Lectura y acciones propias. |
| `GET /api/v1/notifications` · `unread-count` · `PATCH {id}/read` · `read-all` · `DELETE ...` | [E][C] | |

### 2.11 Permisos, Settings, Analytics
| Endpoint | Tipo | Nota |
|---|---|---|
| `GET/PUT /api/v1/permissions` (mobile/web) | [E][C] | Permisos de plataforma del dispositivo. |
| `GET/PUT /api/v1/settings` · `2fa/setup` · `enable` · `disable` | [E][C] | |
| `GET /api/v1/analytics/dashboard` · `incidents/trend` · `trips/summary` | [E][C] | |

---

## 3. Endpoints existentes que deben conservarse

- **Auth completa**: `register`, `login`, `refresh`, `logout`, `change-password`,
  `recover-password`, `reset-password`, `sessions`, `account`, `account/export`.
- **Perfil y preferencias**: `GET/PUT /profile`, `preferences`,
  `driver-profile`, `medical`, `fcm-token` (legacy).
- **Plan listado** (`GET /api/v1/plans`, anónimo).
- **Suscripción (lectura y administración)** y `change-plan`/`cancel`, `expire`.
- **Monitores**: lectura + flujo de invitación por token en cuerpo JSON.
- **Viajes/telemetría lectura** (`GET /api/v1/trips`, `GET {id}/telemetry`,
  `GET active`) — para web.
- **Rutas** (`frequent`, `history`, `select-today`).
- **Dispositivos FCM** y **lectura de wearable** (`GET /api/v1/wearable`,
  `/all`).
- **Notificaciones** completas (incluye `unread-count`).
- **Alertas e incidentes en lectura**.
- **Permissions / Settings / Analytics / 2FA**.
- **Health checks** `/health`, `/health/live`, `/health/ready` (no modificar)
  y OpenAPI V1.

---

## 4. Endpoints existentes que necesitan autorización adicional

**Premisa: la web no debe controlar viajes ni enviar telemetría.** Hoy todos
estos endpoints son alcanzables por cualquier cliente autenticado:

| Endpoint | Capacidad requerida |
|---|---|
| `POST /api/v1/trips/start` · `{id}/pause` · `{id}/resume` · `{id}/finish` | solo `mobile` (respaldo) \| `wearable` |
| `PATCH/POST {id}/telemetry` | solo `mobile` (respaldo) \| `wearable` |
| `POST /api/v1/wearable/pair` · `pair/confirm` · `calibration` · `permissions` · `battery` | solo `mobile` \| `wearable` |
| `POST /api/v1/alerts/detect` · `sos` · `{id}/confirm-ok` · `bypass-critical` · `retry` · `sync-offline` | solo `wearable` \| `mobile` (respaldo) |
| `POST /api/v1/alerts/{id}/close` | solo el titular del viaje |
| `GET /api/v1/profile/search` | rate limiting + devolver solo IDs públicos/username |
| `POST /api/v1/subscriptions/expire` | rol interno/cron, nunca usuario anónimo |
| `POST /api/v1/monitors/invite` | cupo de plan + solo propietario de la red |

Mecanismo recomendado: **capability claim** (`client` = `web`/`mobile`/`wearable`)
firmada en el JWT + authorization policies (`RequireClient("wearable|mobile")`,
`RequireClient("web")`, etc.) aplicadas con `[Authorize(Policy = "...")]`, sin
exponer secretos y sin cambios en `.github/`, `infra/` ni `Program.cs` health.

---

## 5. Contratos faltantes

Estados: **[F]** falta completa · **[P]** propuesta (no existe).

### Identidad / perfil
- `PublicProfileId` en `Usuario`, expuesto solo en su forma pública. **[F]**
- `PUT /api/v1/profile/username` — cambio de username por su propietario. **[F]**
- Reserva de usernames anteriores (entidad de historial). **[F]**
- Login con username o email — `LoginRequest` acepta `identifier`. **[P]**
- Estado de onboarding (`OnboardingProgress`), ficha completada/omitida. **[F]**
- Búsqueda `GET /api/v1/profile/search?by=username|publicProfileId`. **[P]**

### Vehículos (nuevo)
- `GET/POST /api/v1/vehicles`, `PUT/DELETE /api/v1/vehicles/{id}`. **[P]**
- `PATCH /api/v1/vehicles/{id}/primary` — vehículo principal. **[P]**
- `GET /api/v1/vehicles/{id}` por identificador público. **[P]**

### Suscripción familiar
- Invitación interna por `username`/`PublicProfileId`. **[P]**
- Código manual seguro: `POST /api/v1/family/members/code`. **[P]**
- Resumen familiar: `GET /api/v1/family` (plan + propietario + integrantes +
  cupos usados/disponibles). **[F]**
- Miembros: `POST /api/v1/family/members/join` · `leave` · `accept` ·
  `reject` · `remove`. **[F]**
- Cambio/reducción de plan con «pendiente de ajuste». **[P]**

### Preinvitaciones (personas sin cuenta)
- `POST /api/v1/invites/preinvite` (crear, expirable, un solo uso). **[F]**
- `POST /api/v1/invites/redeem` (canje tras el registro). **[P]** (canon:
  mecanismo pendiente).

### Mensajes rápidos (nuevo, P0)
- `GET /api/v1/messages/templates` (sistema + personalizadas). **[F]**
- `POST/PUT/DELETE /api/v1/messages/templates/{id}` (personal, máx 10). **[F]**
- `POST /api/v1/messages` — enviar por plantilla + destinatarios + contexto. **[F]**
- `GET /api/v1/messages?recipientId=` — historial paginado. **[F]**
- `PATCH /api/v1/messages/{id}/read` · `GET /api/v1/messages/unread-count`. **[F]**

### Relaciones de monitoreo (endurecimiento)
- `PATCH /api/v1/monitors/{id}/permissions` — permisos granulares. **[P]**
- `PATCH /api/v1/monitors/{id}/consent` — consentimiento ficha médica. **[F]**
- `POST /api/v1/monitors/{id}/block` · `unblock`. **[F]**
- `GET /api/v1/monitors/mine` vs `GET /api/v1/monitors/watching`. **[P]**

---

## 6. Modelos faltantes

| Modelo requerido | Entidad actual | Descripción del cambio | Estado |
|---|---|---|---|
| `Vehiculo` (colección) | `PerfilConduccion` (1 objeto embebido) | Nueva entidad con `publicId`, tipo 4 ruedas, marca, modelo, año, velocidad promedio, uso, baja lógica, principal | **Ausente** |
| `Membresia` (suscripción) | `Suscripcion` (solo 1 por usuario) | Entidad separada con rol Owner/Member, estado, fechas | **Ausente** |
| `CodigoInvite` seguro | `Usuario.InviteCode` en claro | Hash del código, un solo uso, expiración | **Ausente** |
| `PlantillaMensaje` | `ChatThread` (chat dinámico) | Plantillas system (seed) y personalizadas (máx 10) | **Ausente** |
| `MensajeRapido` / historial | — | Copia inmutable, contexto ruta/incidente, leído/fecha lectura | **Ausente** |
| Relación de monitoreo direccional | `Monitor` (1 dueño, sin dirección) | `MonitorUserId` + `MonitoredUserId`, estados completos, permisos, consentimientos | **Parcial** |
| `Consentimiento` por relación | `PermisosApp` (plataforma) | ficha médica off por defecto | **Ausente** |
| `OnboardingProgress` | — | Estado completado/pendiente | **Ausente** |
| Historial de username | — | Reservas de usernames anteriores | **Ausente** |
| `PublicProfileId` | `AppId` (no inmutable, no garantiza único) | ID público inmutable y único | **Ausente** |
| Consentimiento de ficha médica para monitor | `FichaMedica` (solo propietario) | permiso por relación + subconjunto de campos | **Ausente** |

> **ChatThread** (entidad + contenedor `ChatThreads`) está **muerta**: sin
> repositorio, sin servicio y sin endpoint. Su único costo es el contenedor,
> que no debe considerarse como implementación del módulo de mensajes rápidos.

---

## 7. Pruebas (cobertura actual y faltante)

Cobertura actual (por archivos `ImpactX.Tests/Unit/*` e
`ImpactX.Tests/Integration/*`): servicios de Auth, User, Plan, Monitor, Viaje,
Contact, Notification, Wearable, Device, Settings, Analytics, Alert, Incident;
controladores; contrato V1; paginación; ingesta de telemetría; Category=Security.

Pruebas **faltantes** (por dominio):

| Área | Pruebas requeridas |
|---|---|
| Identidad | Normalización de email; username único e insensitive; cambio de username y reserva de históricos; `PublicProfileId` única e inmutable; login por username; no exponer `Id` interno en ningún DTO. |
| Vehículos | CRUD; cuota 1/3/∞ por usuario activo según plan; principal único; baja lógica; no exponer `Id`; asociación a viaje. |
| Suscripción familiar | Propietario/integrante; cupos 1/3/6 (propietario no consume); invitación + canje por código con hash, un solo uso y expiración; activación atómica de membresía + relación; `leave`/`remove` libera cupo; reducción con «pendiente de ajuste». |
| Mensajes rápidos | Máx 10 plantillas personalizadas; máx 160 caracteres; sistema no modificable; envío solo con relación aceptada/vigente/no-bloqueada y permiso; copia inmutable; contexto ruta/incidente; leído/no leído + contador; 403 para no autorizados; rate limiting. |
| Capacidad de cliente | La web **no** inicia/pausa/reanuda/finaliza viajes ni envía telemetría/sensores (403); móvil respaldo sí; wearable sí. |
| Relaciones | Estados `Pending/Accepted/Rejected/Revoked/Blocked/Expired`; consentimiento; sin duplicados; cupo por plan; preinvitaciones. |
| Incidentes/alertas | Cancelación en ventana de 10 s; lectura de monitores autorizados con permiso; consentimiento de ubicación. |
| Seguridad transversal | 401 vs 403 coherentes; 404 indistinguible en recursos ajenos; tokens/códigos nunca en logs ni query. |

---

## 8. Riesgos de seguridad

1. **Fuga de identificadores internos (P0).** `Id`, `AppId`, `InviteCode` y
   `UsuarioId` se exponen en `AuthResponse.UsuarioDto`, `UserSearchResultDto`,
   `UserProfileDto`, `MonitorDto`, `InvitationInfoDto`, `ViajeDto`,
   `WearableDto`, `AlertStatusDto`, `PagoDto`, `SuscripcionDto`. Contradice las
   palabras de canon (secciones 5 y 17) y permite enumeración/cripteo.
2. **Sin separación de clientes (P0).** La web puede ejecutar viajes,
   telemetría y acciones de wearable/sensor; el canon lo prohíbe.
3. **`PublicProfileId` inexistente → búsquedas usan `AppId`/`Id` fluidx o
   username desnormalizado**, con riesgo de colisión y de exposición.
4. **Tokens débiles y en claro.** `TokenInvitacion` (12 chars de Guid) y
   `PairingToken` (8 chars de Guid) no son criptográficos ni se hash;
   el canon exige tokens expirables, de un solo uso y almacenados seguramente.
5. **Códigos de invitación en claro** en `Usuario.InviteCode` y visible en el
   perfil DTO.
6. **Endpoint administrativo público**: `POST /api/v1/subscriptions/expire`
   (batch) y `POST /api/v1/monitors/invite/details` (anónimo) sin restricción
   clara; deben quedar con policy interna.
7. **Contactos externos por teléfono** (`ContactsController`): contradice el
   alcance que exige contactos de emergencia y monitores **internos**.
8. **Rate limiting parcial**: search, contactos, mensajes (inexistentes) y
   preinvitaciones sin límite; se debe extender el patrón de políticas.
9. **Wearable.uvSync no idempotente** y sin `TripId` (no persiste; riesgo de
   duplicados y telemetría sin viaje).
10. **Ficha médica sin control por relación**: no se garantiza
    «desactivada por defecto» para monitores (no hay endpoint de lectura como
    monitor).
11. **Auditoría limitada**: no hay registro persistente de cambios sensibles
    (suscripción, permisos, consentimientos, plan, membresías); solo logs de
    negocio.

Todos los hallazgos son sobre el backend existente y no implican
recomendaciones de eliminar config de despliegue.

---

## 9. Activos críticos de despliegue que no deben modificarse

Tratar como **solo lectura** (no borrar, renombrar, reemplazar ni simplificar)
en las PRs siguientes:

| Ruta real | Función |
|---|---|
| `.github/workflows/dotnet-ci.yml` | CI: build + tests + cobertura + security regression + smoke test. |
| `.github/workflows/main_impactx-api-backend.yml` | Deploy a Azure Web App via OIDC + verificación post-deploy (`/health/live`, `/health/ready`, `/openapi/v1.json`). |
| `.github/workflows/api-security-audit.yml` | OWASP API security scan. |
| `.github/workflows/secret-scanning.yml` | Gitleaks + política de secretos. |
| `.github/workflows/codeql-analysis.yml` | CodeQL SAST (security-extended). |
| `.github/workflows/code-quality-roslyn.yml` | `dotnet format` estricto sobre C# modificados. |
| `.github/workflows/infra-validation.yml` | Validación de Bicep (compilación estricta). |
| `ImpactXv1/ImpactX.Api.csproj` · `ImpactX.Tests/ImpactX.Tests.csproj` · `ImpactX.slnx` | Proyectos y solución. |
| `ImpactXv1/appsettings.json` · `appsettings.Development.json` · `appsettings.Production.json` | Configuración compartida (Cosmos, JWT, inicialización). |
| `ImpactXv1/Program.cs` | Pipeline de middlewares, health checks, OpenAPI, CORS, rate limiting. |
| `ImpactXv1/Health/*` | `ConfigurationReadinessCheck`, `DatabaseReadinessCheck`. |
| `ImpactXv1/Extensions/WebApplicationExtensions.cs` | Seeding (`PlanSeeder`). |
| `ImpactXv1/Extensions/OpenApiV1*.cs` · `Filter/V1ProblemDetailsResultFilter.cs` | OpenAPI V1 y ProblemDetails. |
| `infra/**` | Bicep (modules ADE, monitoring) — no desplegado por defecto. |
| `scripts/security/check_hardcoded_secrets.py` · `scripts/security/tests/*` | Política de secretos y contrato de workflows. |
| `docs/COSMOS_*.md` · `docs/BACKEND_AUDIT.md` · `docs/DEVSECOPS_EVIDENCE.md` | Documentación de arquitectura y evidencia. |

Estos activos determinan **build**, **CI/CD**, **deploy** y **readiness**.
Ninguna de las PRs propuestas debe modificarlos. Los valores reales de secrets
(deploy) no se reproducen aquí.

### 9.1 Nota CORS / rate limiting

`appsettings.json` define `Cors:AllowedOrigins` (lista blanca) y las políticas
`RateLimiting:*`. Los nuevos endpoints de mensajes/precitaciones deben **reusar**
el patrón por usuario/IP de las políticas existentes (`monitor-invite-create`,
`telemetry-ingestion`, `fcm-token`), nunca eliminar las actuales, y calibrar
límites como decisión del propietario (canon lo deja pendiente).

---

## 10. Tabla principal de auditoría por dominio

Leyenda de **Estado**: `Completo` / `Parcial` / `Ausente` / `Incorrecto` /
`Requiere verificación`. Prioridad: P0 bloqueante, P1 necesario, P2 mejora.

### 10.1 Identidad
| Capacidad | Estado | Archivo actual | Brecha | Cambio requerido | Prioridad |
|---|---|---|---|---|---|
| Email único y normalizado | Parcial | `AuthService.RegisterAsync` | Duplicados no enmascarados, sin normalización | Normalizar y validar de forma única | P0 |
| Login por email o username | Parcial | `AuthService.LoginAsync`, `LoginRequest.Correo` | Solo correo | aceptar `identifier` email/username | P1 |
| Username automático, único y modificable | Parcial | `GenerateUsername` | No modificable, no normalizado | `PUT /profile/username` + reglas | P1 |
| Reserva de usernames anteriores | Ausencia | — | No hay historial | Entidad y regla de reserva (impedir reutilización | P1 |
| `PublicProfileId` (único, inmutable, seguro) | Ausencia | `Usuario.AppId` | `AppId` no es garante | nueva su ´Id` | P0 |
| `InternalUserId` nunca expuesto | Incorrecto | `UsuarioDto.Id`, `MonitorDto.Id`, `ViajeDto.Id`, etc. | expone en cada DTO | elimnar; solo públicos | P0 |
| Búsqueda por username y ProfileId | Parcial | `UsersController.SearchUsers` | Libre, expone `Id` | `search?by=…` + rate limit | P1 |
| Validaciones | Parcial | `RegisterRequest` | Básicas | reforzar identidad canónica | P1 |

### 10.2 Onboarding, perfil y ficha médica
| Capacidad | Estado | Archivo actual | Brecha | Cambio | Prioridad |
|---|---|---|---|---|---|
| Estado de onboarding (completado/pendiente) | Ausento | — | No se registra | `OnboardingProgress` | P1 |
| Perfil general (propio) | Completo | `GET/PUT /profile` | — | — | — |
| Perfil conductor | Parcial | `PerfilConduccion` | Un solo objeto; placa/color no aprobados | Colección de vehículos | P0 |
| Ficha médica completada/omitida | Parcial | `GET/PUT /profile/medical` | Sólo CRUD | estado + consent | P1 |
| Consentimiento por monitor (ficha off por defecto) | Ausente | `FichaMedica` (problema no sirve) | No hay lectura con consent | permiso relacional | P0 |

### 10.3 Vehículos
| Capacidad | Estado | Actual | Evidencia | Brecha | Cambio | Prioridad |
|---|---|---|---|---|---|---|
| Colección CRUD + baja lógica | Ausente | — | No existe | modelo de colección | Entidad `Vehiculo` + controller | P0 |
| Identificador público | Ausente | — | — | exposición | `publicId` | P0 |
| Vehículo principal y alta asociación | Ausente | `StartTripRequest` | Sin `vehiculoId` | asociación | añadir `vehiculoId` | P1 |
| Cuota individual 1/3/∞ | Ausente | `MonitorService` (mide monitores) | no aplicado a vehículos | campo cuota en plan | P1 |
| Baja lógica / principal | Ausente | — | — | — | soft-delete + `primary` | P1 |

### 10.4 Suscripción familiar
| Capacidad | Estado | Actual | Evidencia | Brecha | Cambio | Prioridad |
|---|---|---|---|---|---|---|
| Tres planes 1/3/6 | Parcial | `PlanSeeder` | límites monitores `Free=0/Basic=2/Premium=6` | cupos familia | redefinir | P0 |
| Propietario + integrantes | Ausencia | `Suscripcion` | sin `Membresia` | entidad | nueva membresía | P0 |
| Pago simulado registrado | Parcial | `PagoDto` | nunca creado en flujo | — | crear Pago en activación | P1 |
| Estados de suscripción canón | Parcial | `Suscripcion.Estado` | `Trial/Activa/Cancelada/Expirada` | `Active/PastDue/Suspended/…` | enumerar | P1 |
| Invitaciones + código manual | Ausente | `InviteCode` claro, `AppInvite` muerto | — | flujo completo | hash, único uso, expiración | P0 |
| Preinvitaciones | Ausencia | `AppInvite` sin repositorio | — | canje | activar `IAppInvite’s | P2 |
| Activación atómica (membresía + relación) | Ausencia | `MonitorService.AcceptInvitationAsync` | sólo estado | atómico | transacc. | P0 |
| Reducción de plan | Incorrecto | `PlanService.ChangePlanAsync` | obstruye | no puede | quitar restricción + ajuste | P1 |

### 10.5 Relaciones de monitoreo
| Capacidad | Estado | Actual | Evidencia | Brecha | Cambio | Prioridad |
|---|---|---|---|---|---|---|
| Dirección explícita | Parcial | `Monitor` | implícita | direccionalidad | refactor | P0 |
| Estados completos | Parcial | `Monitor.Estado` | `Pendiente/Activo/Revocado/Rechazado` | $Blocked/Expired | enum | P1 |
| Permisos granulares + consentimientos | Parcial | `Monitor.Permisos` lista | lista libre | ficha off | schema | P0 |
| Cuota según plan | Incorrecto | `MonitorService` | Free=0/Basic=2/Premium=6 | canon 1/3/6 | ajustar | P0 |
| Prevención de duplicados/bloqueos | Parcial | `ExistsByUsernameAsync` | todo por neta | entre redes | reglas | P1 |
| Consultar «quienes me monitorean» | Parcial | `GET /monitors` | solo propietario | — | nuevos endpoints | P1 |
| Lectura relacionada con permiso | Ausencia | Lecturas simétricas | monitor sin acceso | — | permission service | P0 |

### 10.6 Mensajes rápidos (nuevo, P0)
*(El desglose detallado está en 10.7.)*

| Capacidad | Estado | Evidencia | Brecha | Cambio |
|---|---|---|---|---|
| Plantillas del sistema seed | Ausente | `ChatThread` (muerto) | — | seeder + `GET /templates` |
| Plantillas personalizadas (10/160/owner) | Ausente | — | — | CRUD + validación |
| Envío con relación válida | Ausente | — | — | servicio de validación de relación |
| Historial + copia inmutable | Ausente | — | — | `MensajeRapido` |
| Leído/no leído + contador | Ausente | — | — | `read` + `unread-count` |
| Rate limiting | Ausente | `RateLimiting` | sin políticas de mensajes | `messages-send`, `messages-template` |
| Authz (403) | Ausente | — | — | policy + valid |

### 10.7 Análisis de mensajes rápidos (desglose P0 por requisito canónico)

| Requisito canónico | Estado del backend | Notas de implementación (concepto) |
|---|---|---|
| Plantillas del sistema (8 mensajes iniciales) | **Ausente** | Sembrar `PlantillaMensaje` (`esSistema=true`), `GET /api/v1/messages/templates` |
| Plantillas personalizadas (máx 10 activas) | **Ausencia** | Conteo por `CreadoPorUsuarioId`+activa; validar 10 en backend |
| Longitud máxima 160 | **Ausencia** | Validación de modelo + límite de solicitud |
| No texto libre en formulario — solo plantilla | **Ausencia** | Envío por `templatePublicId` |
| Plantilla pertenece a su creador | **Ausencia** | Auth por `CreadoPor` |
| Envío con relación aceptada/vigente/no bloqueada y permiso | **Ausencia** | Resolución `Monitor` entre `emisor`/`destinatario` + `Permiso.Mensajes` |
| Historial conserva copia inmutable | **Ausencia** | `MensajeRapido.TextoCopia` (snapshot) |
| Contexto opcional de ruta/incidente público | **Ausencia** | `routeIdPublic`/`incidentIdPublic` validación de visibilidad |
| Fecha UTC, estado leído y fecha de lectura | **Ausencia** | `Leido`/`LeidoEn` |
| Contador de no leídos | **Ausencia** | `GET /api/v1/messages/unread-count` |
| Nunca exponer IDs internos | **Ausencia** | mensaje `{id}_public`, no remote |
| Autorización + 403 | **Ausencia** | 403 si autenticado pero no autorizado |
| Rate limiting | **Ausencia** | policies `messages-send-expl`, `messages-template` |
| Web y móvil consumen los mismos contratos | **Ausencia** (no implementado) | el mismo contrato sirve ambos; móvil debe poder consumir |
| Wearable NO usa el módulo | — | se excluye la capacidad `wearable` en envío/plantillado |

### 10.8 ChatThread hoy: qué puede reutilizarse y qué no

- **`ChatThread`** (`ImpactXv1/Core/Domain/ChatThread.cs`): entidad de
  conversación libre (`ContactId`, `UltimoMensaje`, `NoLeidos`, `CreadoEn`).
  **Desalineada** con mensajes rápidos; no debe considerarse válida.
- No existe `ChatMessage`, ni plantillas, ni repositorio, ni endpoints.
  El contenedor Cosmos `ChatThreads` está inutilizado.
- **Reutilizable**: el patrón de contador `NoLeidos`, la paginación
  `PagedResult`/`continuationToken`, el patrón de rate limiting `monitor-*`,
  y el esquema de notificaciones (idempotencia) como referencias.
- **No reutilizable**: la entidad `ChatThread` como ctor del módulo; el nuevo
  modelo la reemplaza por `PlantillaMensaje` y `MensajeRapido`.

### 10.9 Resto de dominios (resumen)
| Capacidad | Estado | Evidencia | Brecha | Cambio | Prioridad |
|---|---|---|---|---|---|
| Viajes iniciar/pausar/resumir/finalizar | Correcto (móvil/wearable) | `ViajeService` | cap web | capability | P0 |
| Viaje asociado a vehículo | Ausencia | `StartTripRequest` | no `vehiculoId` | añadir | P1 |
| Telemetría idemPot por EventId | Completo | `POST {id}/telemetry` | — | — | — |
| Web consulta telemetría por TripId | Completo | `GET {id}/telemetry` | — | — | — |
| Wearable vincular/desvincular/capturar | Parcial | `WearableService` | web puede | restrict | P0 |
| Wearable reportar estado/batería/sync | Parcial | `Wearable.…` | sync no persiste | sinc con `TripId` | P1 |
| Incidentes/evidencia/cancel 10 s | Parcial | `AlertService` | cancel manual | ventana | P1 |
| Notificaciones a redes autorizadas | Parcial | `NotifyAlertMonitorsAsync` | no valida permiso | gran | P1 |
| 401/403/rate/ProblemDetails/correlation/CORS/idempotencia | Correcto | infra | mejoras capacidad | P1 |
| Auditoría de cambios sensibles | Parcial | logs | no tabla | evento | P2 |

---

## 11. Lista exacta de bloqueos para declarar backend completo

Cada elemento debe cumplirse **y probarse**. Todos los P0 son obligatorios para
declarar completo; los P1 son necesarios para el lanzamiento web.

1. **Cancelación de IDs internos en DTOs y URLs** (P0) — solo identificadores
   públicos; `InternalUserId` nunca se expone.
2. **Login por email o username** y username auto/único/modificable con
   reserva de anteriores (P0–P1).
3. **`PublicProfileId`** autogenerado, único e inmutable (P0).
4. **Vehículos**: CRUD de colección, cuota individual por usuario activo
   (Free 1 / Estándar 3 / Premium 1-∞ restringido contra abuso), baja lógica,
   principal, asociación a viaje y sin primary key expuesta (P0).
5. **Suscripción familiar**: propietario + integrantes (membresía separada),
   cupos 1/3/6 (el propietario no consume), invitación interna + código manual
   con hash, un solo uso, expiración, cuerpo JSON seguro, nunca URL/login,
   activación atómica, join/leave/accept/reject/remove, y reducción con
   «pendiente de ajuste» (P0).
6. **Preinvitaciones** funcionales o marcadas como pendientes en canon (P1).
7. **Relación de monitoreo direccional**, con estados
   `Pending/Accepted/Rejected/Revoked/Blocked/Expired`, un solo contador de
   cupos por red, permisos granulares y consentimientos (ficha médica off)
   (P0).
8. **Mensajes rápidos** completos: plantillas del sistema seed, personalizadas
   (máx 10, 160), relación válida + permiso, historial inmutable con contexto,
   leído/no leído + contador, rate limiting (P0).
9. **Capacidades de cliente**: claim `client` en JWT; la web **bloqueada** en
   viajes/telemetría/wearable/sensor/alerta; móvil respaldo registrado y
   wearable activo (P0).
10. **Web read-only** para consulta de estado wearable y telemetría (P1).
11. **Incidentes/alertas**: lectura para monitores con permiso
    «ver incidentes/recibir alertas críticas» y consentimiento de ofto (P1).
12. **Rate limiting** para búsqueda, invitaciones/preinvites y mensajes (P1).
13. **Pruebas `Category=Security` expandidas**: ID no expuesto, capacidad web
    bloqueada, consent médico off, 403/404 indistinguibles (P0).
14. **Gate de PR**: `dotnet build` Release, `dotnet test`, scanner de secretos,
    actionlint, `git diff --check` (P0).

---

## 12. Orden recomendado de implementación — los cuatro PR backend

> Solo backend. Cada PR mantiene CI/CD y Azure vigentes, no mezcla frontend ni
> cambios móvil/wearable, y conserva compatibilidad de contratos del programa
> desplegados lo posible.

| PR | Título | Alcance principal | MVP | Riesgo de despliegue |
|---|---|---|---|---|
| **PR1** | **Identidad, onboarding y superficies internas** | `PublicProfileId`, DTOs sin `Id` interno, email normalizado, login por email/username, username modificable + reserva, onboarding, consentimientos al propietario | P0 | Alto (afecta contratos de respuesta; mitigación: campos new + deprecation) |
| **PR2** | **Vehículos + suscripción familiar y membresías** | Entidad `Vehículo`, cuota 1/3/∞ y `primary`; `Membresia`+`Suscripcion` propietario/integrante; invitación interna + código manual con hash y canje; preinvitación; pago simulado; activación atómica; reducción de plan | P0 | Medio-Alto (dominio nuevo; aditivo) |
| **PR3** | **Relaciones de monitoreo + mensajes rápidos + permisos** | Refactor `Monitor` direccional y estados, cupos 1/3/6, permisos/consent; **mensajes rápidos** (plantillas, historial, no leídos); rate limiting y 403 | P0 | Medio (nuevo; `ChatThread` obsoleto pero no se borra) |
| **PR4** | **Capacidades de cliente y endurecido** | Claim `client` en JWT + policies; restringir viajes/telemetría/wearable/sensor/alert; lector read-only; auditoría y token storage | P0/P1 | Bajo-medio (no rompe lectura) |

PR1 **cambia contratos**→ es obligatorio hacerlo con versionado aditivo
(`publicProfileId` nuevo, `Id` temporal por compatibilidad, migración a la
2.0) y coordinado. PR2 y PR3 son **aditivos**. PR4 refuerza **sin romper**.

Fases de integración sugerida por PR: desarrollo → PR de revisión → CI/CD
verde (PR de CI, `.github/**` sin cambios) → Azure deploy con verificación
post-deploy → aceptación. Nunca mezclar frontend en el mismo PR.

---

## 13. Riesgos de despliegue (resumen)

- **PR1**: Riesgo **alto** de compatibilidad. Mitigar versión de contrato +
  deprecation (ya existe `LegacyDeprecationMiddleware`).
- **PR2**: **medio-alto** por tamaño del dominio; conviene introducirlo como
  PR por partes (sub-PRs) para reducir el ticket de revisión.
- **PR3**: **medio**: el refactor de `Monitor` puede cambiar la forma de la
  respuesta legacy (añade campo, no rompe v1).
- **PR4**: **bajo-medio**: las policies nuevas deben adoptarse en sincronía con
  el frontend web (hasta no publicarse, pueden emitir 403 en la web al
  intentar viajes, que es el comportamiento deseado).
- Ningún PR toca `.github/**`, `infra/`, `Program.cs` (health/OpenApi), ni
  secretos, ni appsettings fixture.

---

## 14. Verificaciones de la auditoría

- `git diff --check` ejecutado (sin warning/error).
- `git status --short` → únicamente `docs/PRODUCT_SCOPE_CANONICAL.md`
  (modificado) y `docs/PLATFORM_GAP_ANALYSIS.md` (nuevo).
- Sin `git add / commit / push / pull / merge / rebase / reset / restore /
  clean / stash` realizados.