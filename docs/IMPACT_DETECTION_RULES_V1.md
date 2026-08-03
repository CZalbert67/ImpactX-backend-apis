# Impact detection rules v1

## Alcance

`impact-rules-v1` es un motor determinista y explicable que clasifica telemetría
canonicalizada del Galaxy Watch 8. No es Machine Learning ni un diagnóstico
médico. Su objetivo es abrir una alerta interna con trazabilidad y evitar que el
cliente pueda falsificar etiquetas.

## Entradas

El motor usa, cuando estén disponibles:

- magnitud de aceleración en m/s² calculada por el servidor;
- desaceleración en m/s²;
- magnitud de giroscopio en rad/s calculada por el servidor;
- velocidad, pitch/roll, frecuencia cardiaca y SpO₂;
- calidad del sensor y banderas técnicas.

Aceleración o desaceleración conforman la señal primaria. Giroscopio, velocidad
o cambio extremo de orientación corroboran la señal. Calidad alta suma
confianza; calidad baja o sensores críticos degradados la reducen.

## Resultados

Cada evento persistido puede recibir:

- `impactCandidate`;
- `detectionLabel`;
- `severityLabel` (`none`, `bump`, `moderate`, `severe`, `critical`);
- `ruleVersion=impact-rules-v1`;
- `detectionScore`;
- `labeledAtUtc`.

Los campos no existen en el DTO de escritura y son exclusivos del servidor.

## Alertas

El orquestador selecciona el candidato más severo de cada lote:

- `severe`/`critical`: estado `Enviada`, bypass crítico y notificación interna
  inmediata;
- `bump`/`moderate`: estado `Pendiente` y `autoSendAtUtc` diez segundos después;
- una señal severa posterior puede promover una alerta pendiente;
- eventos repetidos no duplican alertas gracias a `sourceTelemetryEventId`;
- señales correlacionadas dentro de 60 segundos y el mismo viaje se consolidan.

El worker de despacho procesa alertas pendientes vencidas. En pruebas de
integración se deshabilita para que el tiempo no haga los casos inestables.

## Seguridad

No existe integración automática con 911, SMS ni WhatsApp. La notificación usa
el sistema interno/Firebase y únicamente relaciones de monitoreo aceptadas con
permisos de alertas y notificaciones.
