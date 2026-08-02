# Product Scope Canonical — ImpactX Backend APIs

> Fuente de verdad funcional y arquitectónica del proyecto.
>
> Este documento define el alcance de producto confirmado para la plataforma
> backend compartida por los tres clientes de ImpactX: **aplicación web**,
> **aplicación móvil** y **aplicación para wearable**.
>
> No inventa funciones, campos, límites ni endpoints. Todo lo descrito aquí
> corresponde a decisiones confirmadas o a estados explícitamente pendientes.
> No declara concluida la Fase 2.

---

## 1. Naturaleza de la plataforma

- El backend es compartido por **web**, **móvil** y **wearable**.
- Los tres clientes tienen **responsabilidades diferentes**.
- Ocultar botones en el frontend **no sustituye** la autorización del backend.
- Cada operación debe validarse por:
  - Usuario.
  - Propiedad del recurso.
  - Relación.
  - Consentimiento.
  - Dispositivo.
  - Capacidad del cliente.

---

## 2. Responsabilidades del wearable

El wearable es el **cliente principal** para:

- Iniciar viajes.
- Pausar viajes.
- Reanudar viajes.
- Finalizar viajes.
- Capturar y enviar telemetría.
- Enviar acelerómetro, giroscopio, frecuencia cardiaca y demás sensores.
- Detectar posibles accidentes.
- Ejecutar el flujo de cancelación de 10 segundos cuando corresponda.
- Reportar batería, conexión y sincronización.
- Trabajar temporalmente sin conexión y sincronizar posteriormente.

El wearable **no administra**:

- Cuentas.
- Suscripciones.
- Relaciones de monitoreo.
- Chats completos.
- Perfiles de otros usuarios.
- Vehículos.

---

## 3. Responsabilidades de la aplicación móvil

La aplicación móvil:

- Funciona como **cliente principal del usuario monitoreado**.
- Realiza el onboarding.
- Gestiona el perfil del conductor.
- Gestiona la ficha médica opcional.
- Vincula y configura el wearable.
- Gestiona los permisos de sensores.
- Consulta rutas e incidentes.
- Gestiona las relaciones monitor–monitoreado.
- Acepta o rechaza solicitudes.
- Incluye chat.
- Recibe alertas y notificaciones.
- Sincroniza datos locales con el backend.
- Puede iniciar, pausar, reanudar o finalizar un viaje **únicamente como
  mecanismo de respaldo** cuando el wearable falla.
- Administra los vehículos del usuario (ver sección 8).
- Gestiona la **membresía de suscripción familiar** cuando corresponde:
  plan, pago simulado, unirse a un plan mediante código y abandonar el plan
  (ver sección 15).

Debe **registrarse y auditarse** cuándo el móvil toma control de respaldo.

---

## 4. Responsabilidades de la aplicación web

La web funciona como **centro de consulta, comunicación, administración de
cuenta y monitoreo**.

La web **puede**:

- Crear cuenta.
- Iniciar sesión.
- Completar onboarding.
- Gestionar el perfil.
- Gestionar la ficha médica opcional.
- Gestionar los datos del conductor.
- Registrar, consultar, editar y eliminar vehículos (ver sección 8).
- Consultar relaciones de monitoreo.
- Enviar y recibir solicitudes internas.
- Consultar personas que monitorea.
- Consultar quiénes lo monitorean.
- Utilizar el chat interno.
- Consultar rutas sincronizadas.
- Consultar el detalle de rutas.
- Consultar telemetría asociada a `TripId`.
- Consultar incidentes y alertas.
- Consultar notificaciones.
- Administrar privacidad y consentimientos.
- Consultar el estado básico del wearable.
- Administrar la suscripción.

La aplicación web **NO puede**:

- Iniciar viajes.
- Pausar viajes.
- Reanudar viajes.
- Finalizar viajes.
- Enviar telemetría.
- Configurar sensores directamente.
- Vincular o administrar técnicamente el wearable.
- Controlar dispositivos como si fuera la aplicación móvil.

---

## 5. Identidad

Cada cuenta tendrá los siguientes componentes de identidad:

### `InternalUserId`

- Primary key interna.
- **Nunca** se expone.
- **Nunca** se usa para búsquedas públicas.
- **Nunca** aparece en URLs, respuestas públicas o interfaces.

### `PublicProfileId`

- Se genera automáticamente.
- Es único.
- Es inmutable.
- Es **diferente** de la primary key interna.
- Es seguro para compartir.
- Se utiliza para búsqueda e invitaciones.
- **No** se utiliza para iniciar sesión.

### `Username`

- Se genera automáticamente al registrar la cuenta.
- Único sin distinguir mayúsculas y minúsculas.
- Modificable por el propietario.
- Los usernames anteriores quedan _reservados_ para evitar suplantación.
- No pueden reutilizarse inmediatamente por otra cuenta.

### `Email`

- Único.
- Normalizado.
- Validado.
- No puede existir más de una cuenta con el mismo correo.

### Inicio de sesión

- Acepta: **correo electrónico** y **Username**.
- **NO acepta** `PublicProfileId`.

---

## 6. Onboarding

El onboarding puede dividirse en pasos:

1. Cuenta básica.
2. Identidad pública.
3. Datos personales.
4. Perfil del conductor.
5. Datos de los vehículos (colección de vehículos del usuario).
6. Ficha médica opcional.
7. Privacidad y consentimientos.
8. Configuración inicial de la red de monitoreo.

- La ficha médica puede omitirse **sin bloquear** el onboarding.
- Los vehículos son una **colección propiedad del usuario**, no un único
  objeto embebido e inmutable dentro de su perfil (ver sección 8 y sección 15).
- Debe poder registrarse explícitamente:
  - Ficha médica completada.
  - Ficha médica omitida.
  - Onboarding completado.
  - Onboarding pendiente.

---

## 7. Perfil médico

La ficha médica es **opcional** y puede contener:

- Tipo de sangre.
- Padecimientos o condiciones médicas.
- Alergias.
- Medicamentos actuales.
- Notas adicionales para una emergencia.

Para el propietario:

- Puede consultarla.
- Puede modificarla.

Para un monitor:

- **No** puede verla por defecto.
- Solo puede verla con **consentimiento explícito**.
- El permiso viene **desactivado** de manera predeterminada.
- El backend debe devolver **únicamente los campos autorizados**.
- La interfaz **no sustituye** la autorización del servidor.

---

## 8. Perfil del conductor y vehículos

### Perfil del conductor

El prototipo aprobado contempla:

- Nombre completo del conductor.
- Permiso para usar la ubicación en incidentes.
- Permiso para utilizar patrones de conducción para mejorar la detección de
  incidentes.

Estos datos deben estar disponibles para contextualizar:

- Rutas.
- Telemetría.
- Incidentes.
- Severidad.
- Reportes.
- Futuro entrenamiento del sistema.

### Vehículos

Los vehículos son una **colección propiedad del usuario**, no un único objeto
embebido e inmutable dentro de su perfil.

Cada usuario puede registrar **varios vehículos**.

#### Cuota individual por cada usuario activo

Los límites de vehículos **no se comparten** entre todos los integrantes del
plan familiar.

Cada usuario activo —propietario o integrante— recibe **individualmente** la
cuota de vehículos del plan al que pertenece:

- **Gratuito**: 1 vehículo por cada usuario activo.
- **Estándar**: 3 vehículos por cada usuario activo.
- **Premium**: sin límite comercial fijo por cada usuario activo, con
  protecciones técnicas contra el abuso.

Por ejemplo, en un plan Estándar con propietario y tres integrantes, cada una
de las cuatro cuentas puede registrar hasta **tres vehículos propios**.

Los vehículos:

- Pertenecen **siempre** a una cuenta concreta.
- **No** pasan a ser propiedad del titular del plan.
- **No** se comparten automáticamente entre integrantes.
- Solo pueden asociarse a viajes del **propietario del vehículo** o cuando una
  regla autorizada lo permita.
- Mantienen **identificadores públicos seguros**.
- **Nunca** exponen primary keys internas.

La aplicación web y la aplicación móvil pueden:

- Registrar vehículos.
- Consultar vehículos.
- Editar vehículos.
- Eliminar vehículos (baja lógica).
- Seleccionar el vehículo principal.
- Asociar un vehículo a una ruta o viaje.

El wearable:

- **No** administra vehículos.
- Puede recibir o utilizar el vehículo seleccionado para el viaje.

Cada vehículo contiene:

- Identificador público seguro.
- Tipo de vehículo de cuatro ruedas.
- Marca.
- Modelo exacto.
- Año.
- Velocidad promedio.
- Uso principal:
  - Ciudad.
  - Carretera.
  - Mixto.
- Estado activo o eliminado lógicamente cuando corresponda.
- Indicador de vehículo principal.

**No** agregar placa, color, VIN, número de motor ni otros campos **no
aprobados**.

**Nunca** se expone la primary key interna del vehículo en la API.

No confundir los datos de los vehículos con la administración técnica de
dispositivos o wearables.

---

## 9. Monitor, monitoreado y red de monitoreo

Cada usuario puede ser propietario de su propia **red de monitoreo**.

Dentro de su red:

- El propietario actúa como **monitor principal**.
- El propietario puede invitar a otras personas para **monitorearlas**.
- Una persona solo se convierte en **miembro monitoreado** cuando crea su
  cuenta, cuando sea necesario, y acepta explícitamente la relación.
- Cada relación aceptada consume un **cupo del plan del propietario** de la
  red.
- No existen cuotas separadas de relaciones entrantes y salientes.
- Existe **un único contador de relaciones aceptadas** dentro de cada red.

Una misma cuenta puede:

- Ser propietaria de su propia red.
- Monitorear a los integrantes de su red.
- **Pertenecer como monitoreada** a redes de otros usuarios.
- Cumplir ambos papeles simultáneamente.

No existen roles de cuenta mutuamente excluyentes.

Pertenecer a la red de otro usuario:

- Consume un cupo del plan del propietario de **esa otra red**.
- **No** consume un cupo del plan propio.

La relación sigue teniendo dirección interna:

- `monitorUserId` interno: la cuenta que monitorea.
- `monitoredUserId` interno: la cuenta que es monitoreada.

Los identificadores internos **nunca** se exponen en la API. La dirección se
determina de forma independiente en cada relación.

La relación de monitoreo se modela como una **entidad explícita** con al
menos:

- Identificador público seguro.
- `MonitorUserId` interno.
- `MonitoredUserId` interno.
- Estado.
- Fecha de solicitud.
- Fecha de aceptación.
- Fecha de revocación.
- Permisos.
- Usuario que inició la solicitud.
- Dirección de la solicitud.

### Estados conceptuales

| Estado | Descripción |
|---|---|
| `Pending` | Solicitud creada, a la espera de acción del receptor. |
| `Accepted` | Relación activa. |
| `Rejected` | Rechazada por el receptor. |
| `Revoked` | Revocada por alguna de las partes. |
| `Blocked` | Bloqueada. |
| `Expired` | Expirada. |

---

## 10. Permisos de una relación

Cada relación **aceptada y vigente** dentro de una red puede controlar de
manera granular:

- Ver rutas.
- Ver ubicación.
- Ver ubicación durante una emergencia.
- Ver incidentes.
- Recibir alertas críticas.
- Ver ficha médica.
- Enviar mensajes.
- Ver telemetría.
- Recibir notificaciones.

La ficha médica debe venir **desactivada por defecto**.

---

## 11. Solicitudes e invitaciones

Los contactos de emergencia y monitores operativos deben ser **usuarios
internos** de ImpactX.

**No** existen contactos externos mediante:

- SMS.
- WhatsApp.
- Llamadas como relación principal.
- Correo como canal principal de emergencia.

Debe contemplarse que una invitación pueda dirigirse a:

- Un usuario existente de ImpactX.
- Una persona que todavía no tiene cuenta (preinvitación).

En todos los casos, una persona solo se convierte en **miembro monitoreado**
de la red del propietario cuando crea su cuenta, cuando sea necesario, y
**acepta explícitamente** la relación.

La relación **solo se activa** y comienza a consumir cupo del plan del
propietario cuando la persona receptora **acepta**.

Los tokens de invitación o aceptación sensibles:

- **No** deben exponerse en query parameters.
- **No** deben registrarse en logs.
- Deben enviarse en cuerpos JSON seguros cuando corresponda.
- Deben **expirar**.
- Deben ser de **un solo uso**.

### Invitar a una persona sin cuenta

ImpactX permite invitar a una persona que **todavía no tiene cuenta**.

Flujo conceptual:

- Un usuario crea una **preinvitación**.
- La persona invitada recibe una invitación para **registrarse**.
- La persona crea su cuenta.
- La preinvitación se vincula de forma segura con la nueva cuenta.
- La persona **acepta o rechaza** la relación.
- La relación solo se activa **después de la aceptación**, y recién entonces
  comienza a consumir un cupo del plan del propietario de la red.

Reglas:

- La persona invitada **no es un contacto operativo** ni un miembro monitoreado
  hasta que crea su cuenta y acepta.
- Después de registrarse, debe ser **usuario interno** de ImpactX.
- El correo solo se usa como **canal de invitación al registro**.
- El correo **no** convierte a la persona en contacto externo de emergencia.
- Las preinvitaciones **no consumen cupo** del plan del propietario.
- Deben **expirar**.
- Deben ser de **un solo uso**.
- Deben impedir **duplicados** y **spam**.
- Deben respetar **bloqueos**.
- Los tokens sensibles **no se colocan en URLs** ni query parameters.
- Los tokens sensibles **no se registran en logs**.
- Los tokens se transmiten mediante **cuerpos JSON seguros** cuando sea
  necesario.
- La implementación exacta del mecanismo de canje de la preinvitación queda
  **pendiente** (ver «Decisiones todavía pendientes»).

---

## 12. Chat

El chat está disponible en:

- Web.
- Móvil.

**No** está disponible como chat completo en el wearable.

El chat solo puede utilizarse entre usuarios que tengan una relación de
monitoreo **aceptada y vigente**.

Debe soportar conceptualmente:

- Conversaciones.
- Mensajes enviados y recibidos.
- Estado leído / no leído.
- Contador de no leídos.
- Paginación.
- Contexto de ruta.
- Contexto de incidente.
- Bloqueo después de revocar o bloquear la relación.

**No se decide todavía** entre polling, SignalR u otra tecnología.

---

## 13. Viajes, rutas y telemetría

El backend conserva operaciones de:

- Iniciar viaje.
- Pausar viaje.
- Reanudar viaje.
- Finalizar viaje.
- Ingesta de telemetría.

Estas operaciones son **invocadas por el wearable** y, como respaldo, por el
móvil cuando corresponde.

La web solo usa **lectura**:

- Listar rutas o viajes sincronizados.
- Consultar el detalle.
- Consultar telemetría por `TripId`.
- Consultar incidentes asociados.

Requisitos:

- La telemetría **siempre** debe asociarse a `TripId`.
- El viaje puede ligarse a un vehículo de la colección del usuario cuando
  corresponda.
- La web **no** presenta controles para cambiar el estado de un viaje.

---

## 14. Dispositivos y wearable

La web puede visualizar:

- Modelo.
- Estado de vinculación.
- Estado de conexión.
- Batería.
- Última sincronización.
- Estado general.

La aplicación móvil administra:

- Vinculación.
- Desvinculación.
- Permisos.
- Configuración.
- Sincronización.
- Recuperación cuando el wearable falla.

---

## 15. Suscripción familiar, planes y límites

### Naturaleza del plan

La suscripción de ImpactX funciona como un **plan familiar**.

Todos los planes permiten **al menos dos personas totales** para que el
monitoreo tenga sentido: un **propietario** y al menos un **integrante
invitado aceptado**.

El propietario **no cuenta** dentro del límite de integrantes invitados.

Existe:

- Un **propietario** de la suscripción.
- Al menos un **cupo** para integrantes invitados; los aceptados lo consumen.
- Un **plan activo**.
- Un **estado de suscripción**.
- **Límites** asociados al plan.

El propietario:

- Selecciona el plan.
- Realiza el **pago simulado**.
- Invita integrantes.
- Administra integrantes.
- Cambia, renueva o cancela el plan.

Los integrantes:

- **No pagan** individualmente dentro de ImpactX.
- Heredan los beneficios del plan mientras su membresía esté activa.
- No deben ver el flujo de pago como pantalla principal.
- Pueden **abandonar el plan**.
- Pueden contratar su propio plan después de salir.

Cómo las personas se organizan externamente para dividir el costo no forma
parte del sistema.

### Pago simulado

El **pago es lo único simulado**.

Todo lo demás debe ser funcional y persistente:

- Plan.
- Suscripción.
- Propietario.
- Miembros.
- Invitaciones.
- Códigos.
- Estados.
- Límites.
- Renovación.
- Cancelación.
- Cambio de plan.

El pago simulado debe registrar al menos:

- Resultado.
- Fecha.
- Plan.
- Referencia pública segura.
- Importe simulado cuando corresponda.

**No** se almacenan datos reales de tarjetas.

### Tres planes

El plan **Gratuito no es una cuenta individual sin red**: incluye al menos un
integrante invitado aceptado.

| Plan | Integrantes invitados aceptados | Personas totales máximas |
|---|---|---|
| Gratuito | 1 | 2 |
| Estándar | 3 | 4 |
| Premium | 6 | 7 |

El **propietario no cuenta dentro del límite de integrantes invitados**.

Por tanto:

- **Gratuito**: propietario + 1 integrante aceptado = hasta 2 personas.
- **Estándar**: propietario + 3 integrantes aceptados = hasta 4 personas.
- **Premium**: propietario + 6 integrantes aceptados = hasta 7 personas.

Las invitaciones pendientes **no consumen cupo**.

El cupo se consume al **aceptar la membresía**.

La prueba gratuita utiliza los límites del plan Gratuito y **no constituye un
cuarto plan**.

La cuota de **vehículos es individual por cada usuario activo** y no se
comparte entre integrantes del plan (ver sección 8).

### Entidades conceptuales separadas

#### Suscripción

- Propietario.
- Plan.
- Estado.
- Período.
- Pago simulado.
- Límites.

#### Membresía de suscripción

- Suscripción.
- Usuario.
- Rol `Owner` o `Member`.
- Estado.
- Fecha de invitación.
- Fecha de aceptación.
- Fecha de salida o eliminación.

#### Relación de monitoreo

- Monitor.
- Monitoreado.
- Estado.
- Permisos.
- Consentimientos.

La **membresía de suscripción** y la **relación de monitoreo** son conceptos
**separados**.

Aceptar una invitación puede crear de forma **atómica**:

- Membresía activa.
- Relación de monitoreo aceptada.

Sin embargo, deben persistirse como entidades **diferentes**.

Los **permisos iniciales** deben ser seguros.

La **ficha médica** debe permanecer **desactivada por defecto**.

### Formas de unirse

#### Invitación interna

Para usuarios registrados:

- Buscar por `Username` o `PublicProfileId`.
- Enviar invitación.
- Aceptar o rechazar.

#### Código manual

Debe existir en _web_ y _móvil_ un apartado **«Unirme a un plan»**.

El **código**:

- Es de **un solo uso**.
- **Expira**.
- Se asocia a una **invitación concreta**.
- No aparece en query parameters.
- No aparece en logs.
- Se envía en un cuerpo JSON seguro.
- Se almacena de forma segura, preferentemente mediante **hash**.
- No activa la membresía sin aceptación.

#### Persona sin cuenta

- Recibe una preinvitación.
- Crea su cuenta.
- Inicia sesión.
- Introduce o **canjea el código** de forma segura.
- Acepta o rechaza.
- Solo al aceptar se activa su membresía y consume cupo.

### Comportamiento de interfaz

Web y móvil **detectan la membresía al iniciar sesión** y muestran la
experiencia correspondiente.

#### Sin suscripción ni membresía

Web y móvil muestran:

- Planes disponibles.
- Activar plan.
- Pago simulado.
- Unirme a un plan mediante código.

#### Propietario activo

Web y móvil muestran:

- Plan actual.
- Estado.
- Integrantes.
- Cupos usados y disponibles.
- Invitar.
- Cambiar plan.
- Renovar.
- Cancelar.
- Información del pago simulado.

No mostrar nuevamente el flujo de pago como pantalla principal.

#### Integrante activo

Web y móvil muestran:

- Plan al que pertenece.
- Propietario.
- Beneficios.
- Estado de membresía.
- Opción de abandonar el plan.

No mostrar el proceso de pago mientras su membresía esté activa.

#### Invitación pendiente

Mostrar:

- Propietario que invita.
- Plan.
- Aceptar.
- Rechazar.

### Estados conceptuales

#### Suscripción

- `Active`.
- `PastDue`.
- `Suspended`.
- `Cancelled`.
- `Expired`.

#### Membresía

- `Pending`.
- `Active`.
- `Rejected`.
- `Left`.
- `Removed`.
- `Expired`.

#### Invitación

- `Pending`.
- `Accepted`.
- `Rejected`.
- `Expired`.
- `Revoked`.
- `Consumed`.

No diseñar todavía endpoints concretos.

### Cambio o reducción de plan

Cuando el propietario baja a un plan con menos cupos y excede el nuevo
límite:

- No eliminar integrantes automáticamente.
- Bloquear nuevas invitaciones y aceptaciones.
- Marcar la suscripción como **pendiente de ajuste**.
- Solicitar al propietario seleccionar qué integrantes permanecen.
- Mantener trazabilidad.
- No borrar relaciones históricas.

Cuando una membresía aceptada se elimina o abandona, **libera un cupo**.

### Ajustes y reglas de capacidades

- Las cuotas se validan en **backend**, no solo en el frontend.
- Las solicitudes e invitaciones pendientes **no consumen cupo**.
- Si la red ya alcanzó su límite, **no puede activarse** una membresía o
  relación nueva.
- Remover o eliminar una membresía o relación aceptada **libera el cupo**.
- Las protecciones contra spam y el **rate limiting** se aplican
  independientemente del plan.
- La prueba gratuita no elimina las funciones esenciales de seguridad.

---

## 16. Incidentes, alertas y notificaciones

Debe distinguirse:

- **Incidente**.
- **Alerta crítica**.
- **Notificación**.
- **Mensaje**.

Los usuarios autorizados pueden consultar:

- Incidente.
- Severidad.
- Ubicación permitida.
- Telemetría permitida.
- Estado de atención.
- Contexto compartido.
- Ficha médica únicamente con **consentimiento**.

---

## 17. Reglas de seguridad

- Nunca exponer IDs internos de base de datos.
- Nunca confiar únicamente en el frontend.
- Validar propiedad del recurso.
- Validar relación aceptada.
- Validar permisos granulares.
- Validar capacidad del cliente.
- Validar consentimiento.
- Aplicar **403** cuando el usuario esté autenticado pero no autorizado.
- Aplicar **rate limiting** a búsquedas, solicitudes, invitaciones,
  preinvitaciones y mensajes.
- Auditar cambios sensibles.
- No registrar tokens ni datos médicos completos en logs.
- Mantener **idempotencia** en telemetría y sincronización.

### Suscripción familiar

- Los códigos de invitación se almacenan de forma segura, preferentemente
  mediante **hash**.
- Los códigos **no** aparecen en query parameters.
- Los códigos **no** se registran en logs.
- Los códigos son de **un solo uso** y **expiran**.
- El cupo del plan se valida al **aceptar** la membresía.
- No se almacenan datos reales de tarjetas.

---

## 18. Matriz de responsabilidades

| Función | Web | Móvil | Wearable | Backend requerido | Autorización requerida | Observaciones |
|---|---|---|---|---|---|---|
| Crear cuenta | Sí | Sí | No | Sí | No | Pública; identidad pública generada automáticamente. |
| Iniciar sesión | Sí | Sí | No | Sí | No | Por Email o Username. Nunca por `PublicProfileId`. |
| Completar onboarding | Sí | Sí | No | Sí | Sí | Registro explícito de estado (completado / pendiente). |
| Administrar perfil | Sí | Sí | No | Sí | Sí | Propiedad del usuario. |
| Administrar ficha médica (propia) | Sí | Sí | No | Sí | Sí | Opcional; permite omitirse sin bloquear. |
| Ver ficha médica (monitor) | Sí | Sí | No | Sí | Sí | Solo con consentimiento explícito. |
| Administrar perfil del conductor | Sí | Sí | No | Sí | Sí | Datos aprobados del prototipo. |
| CRUD de vehículos | Sí | Sí | No | Sí | Sí | Colección del usuario; cuota **individual por cada usuario activo** según su plan. |
| Cuota de vehículos (plan familiar) | Sí | Sí | No | Sí | Sí | Individual por usuario activo (no compartida): 1 / 3 / sin límite comercial fijo. |
| Seleccionar vehículo principal | Sí | Sí | No | Sí | Sí | Indicador principal. |
| Seleccionar o asociar vehículo para un viaje | No | Sí | No | Sí | Sí | La web no opera viajes; el wearable utiliza el seleccionado. |
| Vincular, desvincular y configurar wearable | No | Sí | Sí | Sí | Sí | El móvil administra; el wearable participa o aplica configuración. |
| Consultar estado básico del wearable | Sí | Sí | Sí | Sí | Sí | Web y móvil consultan; el wearable aplica o reporta su estado. |
| Configurar sensores directamente | No | Sí | No | Sí | Sí | El móvil administra permisos y configuración. |
| Capturar datos de sensores | No | No | Sí | No | No | El wearable captura de forma local. |
| Aplicar la configuración en el dispositivo | No | No | Sí | No | No | El wearable aplica la configuración. |
| Enviar telemetría | No | Solo sincronización o respaldo autorizado | Sí | Sí | Sí | Móvil únicamente sincronización o respaldo técnicamente autorizado cuando corresponda. Siempre asociada a `TripId`. |
| Detección inicial de accidentes | No | No | Sí | No | — | Detección local inicial en el wearable; el backend no realiza la detección local inicial. |
| Flujo integral del incidente | — | — | No | Sí | Sí | Backend: recibe, valida, registra, conserva evidencia, aplica reglas, crea alertas, notifica y mantiene trazabilidad. |
| Iniciar viaje | No | Respaldo | Sí | Sí | Sí | Móvil solo como respaldo cuando falla el wearable. |
| Pausar viaje | No | Respaldo | Sí | Sí | Sí | Móvil solo como respaldo cuando falla el wearable. |
| Reanudar viaje | No | Respaldo | Sí | Sí | Sí | Móvil solo como respaldo cuando falla el wearable. |
| Finalizar viaje | No | Respaldo | Sí | Sí | Sí | Móvil solo como respaldo cuando falla el wearable. |
| Consultar rutas sincronizadas | Sí | Sí | No requerido | Sí | Sí | Lectura. |
| Consultar detalle de rutas | Sí | Sí | No requerido | Sí | Sí | Lectura. |
| Consultar telemetría por `TripId` | Sí | Sí | No requerido | Sí | Sí | Lectura. |
| Crear invitación o preinvitación | Sí | Sí | No | Sí | Sí | Invita a un usuario existente o a una persona sin cuenta. |
| Aceptar o rechazar solicitudes | Sí | Sí | No | Sí | Sí | Estados de la relación. |
| Gestionar relaciones de monitoreo | Sí | Sí | No | Sí | Sí | Red del propietario; cupo según plan. |
| Chat completo | Sí | Sí | No | Sí | Sí | Requiere relación aceptada y vigente. |
| Consultar plan | Sí | Sí | No | Sí | Sí | Ver plan actual, estado e integrantes. |
| Seleccionar plan | Sí | Sí | No | Sí | Sí | Propietario elige el plan. |
| Ejecutar pago simulado | Sí | Sí | No | Sí | Sí | Propietario; registrado como simulado. |
| Invitar integrantes a la suscripción | Sí | Sí | No | Sí | Sí | Invitación interna, código manual y preinvitación. |
| Administrar integrantes de la suscripción | Sí | Sí | No | Sí | Sí | Propietario; cupo según plan. |
| Unirse a un plan mediante código | Sí | Sí | No | Sí | Sí | Código de un solo uso en cuerpo JSON seguro. |
| Abandonar el plan | Sí | Sí | No | Sí | Sí | Libera cupo; el usuario puede contratar su propio plan. |
| Recibir alertas y notificaciones | Sí | Sí | No | Sí | Sí | Incidente / alerta / notificación / mensaje. |
| Sincronizar datos locales | No | Sí | Sí | Sí | Sí | Trabajo sin conexión y sincronización posterior. |

---

## 19. Funciones fuera del alcance actual

- Machine learning de Fase 3.
- Control de viajes desde la web.
- Administración técnica del wearable desde la web.
- Contactos externos.
- SMS, WhatsApp o correo como red principal de emergencia.
- Exposición de IDs internos.
- CRUD web completo de hardware.

---

## 20. Criterios de aceptación

El documento debe:

- Ser coherente.
- No inventar endpoints.
- No declarar concluida la Fase 2.
- Servir como fuente de verdad para la auditoría posterior.
- Distinguir claramente web, móvil, wearable y backend.
- Incluir la sección final **«Decisiones todavía pendientes»**.
- No hacer recomendaciones contradictorias con las decisiones confirmadas.

---

## Decisiones todavía pendientes

- Duración del **ciclo de suscripción simulado** (mensual, anual u otro).
- Duración o **expiración exacta** de códigos de invitación y de
  preinvitaciones.
- Política de **periodo de gracia** tras un pago simulado vencido.
- **Comportamiento final** de integrantes tras cancelar o expirar una
  suscripción.
- **Mecanismo final del pago simulado** (sigue siendo simulado, pero se
  confirma la forma de generación del flujo).
- Subconjunto exacto de la ficha médica visible con consentimiento.
- Umbrales exactos de rate limiting.
- Tecnología de tiempo real del chat: polling, SignalR u otra, según se
  confirme.
- Semántica de sincronización offline y de resolución de conflictos en la
  telemetría.

---

## Notas de trazabilidad

- Este documento es **únicamente documental**. No autoriza a modificar
  código, pruebas, configuración, workflows, OpenAPI ni archivos existentes.
- Rama de origen: `docs/platform-alignment-audit`.
- No se han ejecutado operaciones de control de versiones (add, commit, push,
  merge, rebase) como parte de la creación de este documento.
- Última revisión: red de monitoreo propietaria, tres planes (Gratuito,
  Estándar, Premium), invitaciones y pertenencia a una red, matriz de
  responsabilidades actualizada y modelo de **suscripción familiar** (propietario
  e integrantes, pago simulado, entidades separadas, estados, formas de unirse,
  reducción de plan y seguridad). Correcciones finales: mínimo de dos personas
  por plan, cuota de vehículos individual por cada usuario activo y matriz
  coherente para consultar, seleccionar, pagar, administrar, unirse y abandonar.