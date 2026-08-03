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

> **Actualización contractual 2026.08.05:** todos los planes operan como un
> grupo unificado. La única invitación de grupo reemplaza, en los clientes
> nuevos, las invitaciones separadas de membresía, monitoreo y contacto SOS.
> Cada integrante controla sus permisos por persona; SOS es una prioridad
> dentro del mismo grupo. Consulte `UNIFIED_GROUP_MODEL_V2.md`.

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
- Chats completos o mensajería en tiempo real.
- Perfiles de otros usuarios.
- Vehículos.
- El módulo de mensajes rápidos.

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
- Consulta y administra plantillas de mensajes rápidos.
- Envía mensajes rápidos predefinidos.
- Consulta historial de mensajes.
- Consume los mismos contratos backend de mensajes rápidos que la web.
- Recibe alertas y notificaciones.
- Sincroniza datos locales con el backend.
- Puede consultar el estado, historial y telemetría de los viajes.
- **No** puede iniciar, pausar, reanudar ni finalizar viajes.
- Administra los vehículos del usuario (ver sección 8).
- Gestiona el **plan y grupo unificado** cuando corresponde:
  plan, pago simulado, unirse mediante una sola invitación, permisos por integrante y abandono voluntario
  (ver sección 15).

El control del ciclo de vida del viaje permanece exclusivamente en el wearable.

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
- Consultar las plantillas de mensajes rápidos disponibles.
- Administrar sus plantillas personalizadas (máximo 10).
- Enviar mensajes rápidos predefinidos.
- Consultar el historial de mensajes rápidos.
- Marcar mensajes como leídos.
- Consultar el contador de no leídos.
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

- En el registro completo lo elige el usuario; el backend lo normaliza y valida.
- Las cuentas legacy pueden recibir uno generado automáticamente por compatibilidad.
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
  - Versión del contrato de registro.
  - Versión y fecha UTC de aceptación de términos.
  - Versión y fecha UTC de aceptación del aviso de privacidad.
- El contrato de registro vigente se consulta en
  `GET /api/v1/auth/registration-contract`.
- El registro completo exige nombre, username, correo, teléfono, contraseña y
  aceptación explícita de términos y privacidad.
- `confirmPassword` es una validación exclusiva del frontend y no se envía ni se
  persiste en el backend.

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
- El canje se realiza mediante `publicContactId` o un código manual de un solo
  uso enviado en el cuerpo JSON. El backend conserva únicamente el hash del
  código, lo invalida al aceptar, rechazar, revocar, bloquear o expirar y nunca
  lo coloca en una URL ni lo registra en logs.
- La preinvitación puede vincularse con una cuenta creada posteriormente cuando
  el correo normalizado coincide; aun así, la relación permanece `Pending`
  hasta que esa persona la acepta explícitamente.
- Los documentos legacy con nombre y teléfono, sin `publicContactId`, se
  consideran `LegacyUnverified`: no aparecen en el contrato V1 y no forman
  parte de la red operativa de emergencia.

---

## 12. Mensajes rápidos ImpactX

**No existe chat libre ni mensajería en tiempo real** en esta versión.

No se requiere decidir entre polling, SignalR, WebSockets u otra tecnología.

Los usuarios envían **únicamente mensajes rápidos predefinidos**.

### Plantillas del sistema

- Incluidas por ImpactX.
- Disponibles para todos los usuarios autorizados.
- No pueden ser modificadas ni eliminadas por usuarios.
- Se administran como **datos sembrados** del backend.

Mensajes iniciales:

- Estoy bien.
- Necesito ayuda.
- Llámame cuando puedas.
- Revisa mi ubicación.
- Voy en camino.
- Tuve un incidente.
- ¿Estás bien?
- Confirma que recibiste la alerta.

### Plantillas personalizadas

Cada usuario puede:

- Crear.
- Consultar.
- Editar.
- Eliminar lógicamente.
- Ordenar.
- Enviar.

Reglas:

- Máximo **10 plantillas personalizadas activas** por usuario.
- Máximo **160 caracteres** por plantilla.
- **No** se permite enviar texto libre desde el formulario de envío.
- Una plantilla personalizada pertenece **exclusivamente a su creador**.
- El **backend valida el límite de 10**.
- Eliminar una plantilla **no** elimina el historial de mensajes enviados.

### Envío

Solo se permite entre usuarios con una relación de monitoreo:

- Aceptada.
- Vigente.
- No revocada.
- No bloqueada.
- Con permiso para mensajes.

El backend valida remitente, destinatario, relación y plantilla.

### Historial

Cada envío conserva:

- Identificador público seguro.
- Remitente interno.
- Destinatario interno.
- Relación interna.
- Plantilla pública utilizada.
- **Copia inmutable** del texto enviado.
- Fecha UTC.
- Estado leído / no leído.
- Fecha de lectura.
- Contexto opcional de ruta mediante identificador público.
- Contexto opcional de incidente mediante identificador público.

Nunca se exponen identificadores internos.

### Plataformas

**Web:**

- Consulta plantillas.
- Administra las 10 plantillas personalizadas.
- Envía mensajes rápidos.
- Consulta historial.
- Marca como leído.
- Consulta no leídos.

**Móvil:**

- Debe poder consumir los mismos contratos backend.
- Nosotros no desarrollamos la aplicación móvil.

**Wearable:**

- No utiliza el módulo completo de mensajes rápidos.

---

## 13. Viajes, rutas y telemetría

El backend conserva operaciones de:

- Iniciar viaje.
- Pausar viaje.
- Reanudar viaje.
- Finalizar viaje.
- Ingesta de telemetría.

Estas operaciones son **invocadas exclusivamente por el wearable**.

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

## 15. Plan, grupo unificado y límites

### Naturaleza del plan

Todos los planes de ImpactX funcionan como un **grupo unificado**.

Todos los planes permiten **al menos dos personas totales** para que el
monitoreo tenga sentido: un **propietario** y al menos un **integrante
invitado aceptado**.

El propietario **sí cuenta** dentro de la capacidad total publicada del plan.

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
- Recuperan automáticamente un plan Gratuito personal al salir o ser eliminados.

Cómo las personas se organizan externamente para dividir el costo no forma
parte del sistema.

Al aceptar la invitación, todos los integrantes quedan conectados entre sí.
Cada propietario de datos define permisos por integrante. La ficha médica
requiere consentimiento explícito. Un contacto SOS es una prioridad asignada
a un integrante del grupo y no una relación separada.

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
| Estándar | 2 | 3 |
| Premium | 5 | 6 |

El **propietario cuenta dentro de la capacidad total publicada**.

Por tanto:

- **Gratuito**: propietario + 1 integrante aceptado = hasta 2 personas.
- **Estándar**: propietario + 2 integrantes aceptados = hasta 3 personas.
- **Premium**: propietario + 5 integrantes aceptados = hasta 6 personas.

Las invitaciones pendientes **reservan cupo** para evitar generar más
invitaciones de las que el plan puede aceptar.

El cupo queda ocupado de forma definitiva al **aceptar la membresía** y se
libera cuando la invitación expira, se rechaza o se revoca.

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
| Enviar telemetría | No | No | Sí | Sí | Sí | Escritura exclusiva del wearable. Web y móvil solo consultan estado e historial. Siempre asociada a `TripId`. |
| Detección inicial de accidentes | No | No | Captura señales | Sí | — | El wearable captura; el backend calcula magnitudes, aplica reglas versionadas y genera alertas internas. |
| Flujo integral del incidente | — | — | No | Sí | Sí | Backend: recibe, valida, registra, conserva evidencia, aplica reglas, crea alertas, notifica y mantiene trazabilidad. |
| Iniciar viaje | No | No | Sí | Sí | Sí | Control exclusivo del wearable; el móvil es lectura. |
| Pausar viaje | No | No | Sí | Sí | Sí | Control exclusivo del wearable; el móvil es lectura. |
| Reanudar viaje | No | No | Sí | Sí | Sí | Control exclusivo del wearable; el móvil es lectura. |
| Finalizar viaje | No | No | Sí | Sí | Sí | Control exclusivo del wearable; el móvil es lectura. |
| Consultar rutas sincronizadas | Sí | Sí | No requerido | Sí | Sí | Lectura. |
| Consultar detalle de rutas | Sí | Sí | No requerido | Sí | Sí | Lectura. |
| Consultar telemetría por `TripId` | Sí | Sí | No requerido | Sí | Sí | Lectura. |
| Crear invitación o preinvitación | Sí | Sí | No | Sí | Sí | Invita a un usuario existente o a una persona sin cuenta. |
| Aceptar o rechazar solicitudes | Sí | Sí | No | Sí | Sí | Estados de la relación. |
| Gestionar relaciones de monitoreo | Sí | Sí | No | Sí | Sí | Red del propietario; cupo según plan. |
| Consultar plantillas de mensajes | Sí | Sí | No | Sí | Sí | Plantillas del sistema + personalizadas del usuario. |
| Administrar plantillas personalizadas | Sí | Sí | No | Sí | Sí | Máximo 10 activas; 160 caracteres; pertenencia exclusiva. |
| Enviar mensaje rápido | Sí | Sí | No | Sí | Sí | Relación aceptada, vigente, no revocada/bloqueada y con permiso de mensajes. |
| Consultar historial de mensajes | Sí | Sí | No | Sí | Sí | Copia inmutable; contexto opcional de ruta/incidente. |
| Marcar leído | Sí | Sí | No | Sí | Sí | Estado leído / no leído sobre el historial. |
| Consultar no leídos | Sí | Sí | No | Sí | Sí | Contador de no leídos. |
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
- Política de **periodo de gracia** tras un pago simulado vencido.
- **Comportamiento final** de integrantes tras cancelar o expirar una
  suscripción.
- **Mecanismo final del pago simulado** (sigue siendo simulado, pero se
  confirma la forma de generación del flujo).
- Subconjunto exacto de la ficha médica visible con consentimiento.
- Umbrales exactos de rate limiting.

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
- Corrección de alcance: se sustituye el chat dinámico por el modelo definitivo
  de **mensajes rápidos predefinidos** (plantillas del sistema, plantillas
  personalizadas, historial con copia inmutable, relación vigilada y sin
  decisión de tecnología de tiempo real).
## Consolidación operativa de monitoreo

- `MonitoringRelationships` es la fuente canónica para autorización, mensajes
  y destinatarios de alertas.
- `Monitores` queda como almacenamiento legacy no operativo; no se elimina de
  forma automática.
- Una alerta solo se envía al monitor de una relación `Accepted` que conserve
  los permisos `ReceiveCriticalAlerts` y `ReceiveNotifications`.
- Los permisos técnicos web/móvil son independientes de los permisos que una
  persona monitoreada concede a su monitor.
- Las invitaciones pendientes se persisten como `Expired` al detectarse su
  vencimiento de siete días.

## Contrato Galaxy Watch 8 y telemetría V2

- Wearable objetivo: Samsung Galaxy Watch 8 con WearOS.
- La vinculación, confirmación, permisos y desvinculación se administran desde
  móvil; el backend rechaza otros modelos.
- El heartbeat registra batería, carga, versiones, desfase del reloj y
  capacidades. El diagnóstico registra sensores disponibles, no disponibles y
  calidad real.
- El esquema de telemetría 2 es el contrato operativo para nuevos clientes.
  Conserva idempotencia por EventId y admite sincronización offline mediante
  batch y secuencias.
- Acelerómetro, giroscopio, GPS y calidad son obligatorios en V2. HR, HRV, SpO₂
  y orientación son opcionales según disponibilidad y permisos.
- Las magnitudes derivadas se calculan en servidor. Las etiquetas de detección y
  severidad solo pueden ser escritas por procesos internos.
- La versión 1 permanece disponible para compatibilidad; no se eliminan ni se
  reescriben documentos históricos de Cosmos.


## Motor inicial de impacto y sincronización móvil

- El endpoint `GET /api/v1/mobile/sync/bootstrap` entrega al cliente móvil un
  snapshot de lectura con perfil, permisos, wearable, viaje activo, vehículos,
  contactos, relaciones de monitoreo, mensajes rápidos y contadores.
- El snapshot no concede al móvil escritura de telemetría ni control del viaje.
- La idempotencia offline de telemetría permanece por `eventId`; mismo contenido
  es duplicado seguro y contenido distinto responde 409.
- El motor `impact-rules-v1` etiqueta cada evento nuevo en servidor. Los clientes
  no pueden enviar `impactCandidate`, `severityLabel`, `ruleVersion` ni puntaje.
- Señales `severe`/`critical` generan alerta interna inmediata. Señales
  `bump`/`moderate` generan una alerta pendiente con 10 segundos para cancelar.
- No existe llamada automática a 911, SMS o WhatsApp. La salida se limita a
  notificaciones internas/Firebase dirigidas a relaciones aceptadas y autorizadas.

## Resoluciones funcionales V8 — sustituyen decisiones pendientes anteriores

- Ciclo simulado: mensual por defecto y anual opcional.
- Periodo de gracia: tres días para suscripción individual y familiar.
- Expiración: el usuario y los integrantes afectados vuelven a Free; las
  invitaciones pendientes expiran y las membresías dejan de otorgar beneficios.
- Pago: registro simulado aprobado, sin proveedor financiero real.
- Plan público Estándar: se mantiene compatibilidad de almacenamiento con Basic.
- Sincronización móvil: bootstrap, changes, push y ack con idempotencia por
  operationId; no concede control de viaje ni escritura de telemetría.
- Incidentes: uno por alerta, actualizado de forma idempotente y retenido 365 días.
- Cuenta: exportación, revocación de consentimientos, anonimización y retención.
- El subconjunto médico visible a monitores continúa sujeto a consentimiento
  explícito; no se amplía automáticamente.
- Los umbrales finales de rate limiting se calibrarán en el cierre V9 sin cambiar
  el contrato funcional de endpoints.

## Cierre V9 — contrato definitivo para clientes

- El contrato API V1 queda congelado con versión `2026.08.05`.
- `/api/v1/meta/contract` enumera el contrato efectivo y
  `/api/v1/meta/clients/{client}` publica capacidades de web, móvil y wearable.
- OpenAPI V1 documenta el cliente permitido en operaciones restringidas.
- Todas las respuestas publican versión de API y contrato.
- Las rutas legacy se conservan temporalmente, marcadas como deprecadas con
  sunset `2027-02-02T00:00:00Z`; ningún frontend nuevo debe consumirlas.
- Los límites de rate limiting de producción quedan definidos sin cambiar
  cuerpos, rutas ni reglas funcionales.
- Cosmos se valida en modo `ValidateOnly`; V9 no crea ni elimina recursos.
- La comprobación final de Cosmos, Firebase y Azure se ejecuta mediante el
  runbook de producción. Esas comprobaciones no agregan endpoints ni cambian el
  contrato funcional.
- Tras aprobar la suite V9, el backend queda listo para handoff y el trabajo
  pendiente corresponde a completar los clientes sobre este contrato.
