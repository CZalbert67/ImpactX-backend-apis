# ImpactX Backend — cierre funcional V9

## Alcance terminado

El backend dispone de contratos canónicos para web, móvil y Samsung Galaxy Watch 8. La API V1 incluye identidad, onboarding, perfil médico, vehículos, planes, suscripciones individuales y familiares, contactos de emergencia, monitoreo, mensajes rápidos, notificaciones, viajes, telemetría V2, motor inicial de impactos, alertas, incidentes, sincronización móvil offline, exportación y eliminación de cuenta.

La versión de contrato queda congelada como `2026.08.05`. Los nuevos cambios que alteren rutas, cuerpos o respuestas deberán publicarse como una nueva versión y no modificar silenciosamente V1.

## Garantías del cierre

- Rutas canónicas bajo `/api/v1/*`.
- JWT con claim obligatoria `client=web|mobile|wearable` para operaciones restringidas.
- OpenAPI limitado a V1.
- Contrato JSON autoenumerado en `/api/v1/meta/contract`.
- Headers de versión en todas las respuestas.
- Rutas legacy marcadas con deprecación y fecha de sunset.
- Problem Details uniforme.
- Correlation ID, security headers y rate limiting.
- Inicialización Cosmos `ValidateOnly` en producción.
- Escaneo de secretos y suite de seguridad.
- Sin llamadas automáticas al 911, SMS o WhatsApp.
- Sin cambios destructivos automáticos en Cosmos DB.

## Estado de datos

Se reutilizan los 23 contenedores existentes. No se agregan contenedores ni se cambian partition keys, throughput o TTL durante V9.

Retención:

- Viajes y telemetría: 90 días.
- Alertas e incidentes: 365 días.
- Notificaciones: 30 días.

## Dependencias externas

La compilación y las pruebas locales validan la lógica del backend, pero no sustituyen las comprobaciones de infraestructura. Antes de publicar se deben ejecutar:

```bash
bash scripts/smoke/cosmos_schema_readonly.sh
python3 scripts/smoke/firebase_configuration_check.py
bash scripts/smoke/azure_api_smoke.sh
```

La validación Cosmos es de solo lectura. La comprobación Firebase valida la service account sin enviar mensajes. El envío real se prueba después con una cuenta y dispositivo de prueba mediante el flujo normal de ImpactX.

## Entrega al frontend

El desarrollo web debe basarse en:

- `docs/FRONTEND_API_HANDOFF.md`
- `/openapi/v1.json`
- `/api/v1/meta/contract`

No deben implementarse llamadas a `/api/contacts`, `/api/monitors` ni otras rutas legacy en código nuevo.
