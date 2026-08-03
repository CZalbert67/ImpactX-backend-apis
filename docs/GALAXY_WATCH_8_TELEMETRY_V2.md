# Galaxy Watch 8 — contrato operativo y telemetría V2

## Alcance

El prototipo ImpactX admite como wearable objetivo **Samsung Galaxy Watch 8 con
WearOS**. El backend valida el modelo; no depende de que la interfaz oculte la
vinculación de otros dispositivos.

El reloj es el único cliente que puede iniciar, pausar, reanudar y finalizar
viajes y escribir telemetría. La aplicación móvil administra la vinculación,
permisos y desvinculación. La web y el móvil consultan estado, historial y datos.

## Vinculación

1. Móvil solicita `POST /api/v1/wearable/pair` con identificador, modelo,
   versiones y capacidades.
2. Backend genera un código de ocho caracteres con vencimiento de diez minutos y persiste únicamente su hash SHA-256.
3. Móvil confirma en `POST /api/v1/wearable/pair/confirm`.
4. Un código vencido se invalida y el registro queda `Expirado`.

El código existe para el prototipo. Antes de producción debe reemplazarse por un
flujo de vinculación criptográfico con identidad del dispositivo y prueba de
posesión.

## Operación

- `POST /heartbeat`: conexión, batería, carga, versiones, desfase de reloj y
  capacidades.
- `POST /sensors/diagnostics`: estado real de sensores y calidad general.
- `PATCH /battery`: actualización ligera entre heartbeats.
- `POST /calibration`: acelerómetro + giroscopio + GPS son el mínimo para marcar
  el dispositivo como calibrado.

Los timestamps reportados deben ser UTC y no pueden adelantarse más de cinco
minutos respecto del servidor. El backend registra su propia hora de recepción;
no confía en el timestamp del cliente como hora de procesamiento.

## Datos para detección y ML

La telemetría V2 conserva:

- ubicación, precisión GPS, velocidad, altitud y rumbo;
- aceleración XYZ y magnitud canónica;
- giroscopio XYZ y magnitud canónica;
- desaceleración y orientación;
- frecuencia cardiaca, HRV y SpO₂ cuando estén disponibles;
- batería, firmware, versión de app/WearOS, calidad y banderas técnicas;
- viaje, vehículo, secuencias y condición offline.

Las etiquetas de impacto, severidad, versión de reglas y versión de modelo son
campos internos del servidor. El wearable no puede autodeclarar la clasificación
que posteriormente se use para evaluación o entrenamiento.

## Sincronización offline

El reloj genera `eventId`, `batchId`, `batchSequence` y `sequenceNumber` antes de
enviar. Puede almacenar lotes localmente y reenviarlos tras recuperar conexión.
El servidor deduplica por `eventId`, por lo que un acuse perdido no crea datos
duplicados. Un mismo id con contenido diferente se rechaza con `409`.

## Privacidad y retención

La telemetría operativa conserva TTL de 90 días. Biometría y ubicación se
procesan únicamente conforme a los consentimientos de ImpactX. Un dataset
permanente para ML no debe generarse automáticamente a partir del contenedor
operativo; requiere gobernanza, anonimización, consentimiento y trazabilidad de
etiquetas.
