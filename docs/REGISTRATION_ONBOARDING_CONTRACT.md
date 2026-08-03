# Contrato de registro y onboarding — ImpactX API V1

## Objetivo

El registro oficial de las aplicaciones web y móvil usa un contrato versionado.
El backend conserva temporalmente la versión legacy para no romper clientes ni
pruebas existentes, pero el frontend nuevo debe usar siempre la versión actual
publicada por la API.

## Descubrir el contrato vigente

```http
GET /api/v1/auth/registration-contract
```

La respuesta es pública e incluye:

- `contractVersion` vigente;
- versión de términos de uso;
- versión del aviso de privacidad;
- clientes que pueden crear cuentas;
- campos obligatorios;
- reglas de username y contraseña;
- confirmación de que `confirmPassword` se valida solo en el cliente y nunca se
  almacena ni se envía a la API.

## Registro completo

```http
POST /api/v1/auth/register
Content-Type: application/json
```

Ejemplo:

```json
{
  "registrationVersion": 2,
  "nombre": "Nombre completo",
  "username": "nombre.usuario",
  "correo": "usuario@example.com",
  "telefono": "+52 773 123 4567",
  "password": "Password123!",
  "termsAccepted": true,
  "privacyAccepted": true,
  "locationIncidentConsent": false,
  "drivingPatternConsent": false,
  "client": "web"
}
```

### Reglas

- Solo `web` y `mobile` pueden crear cuentas con el contrato completo.
- El wearable no crea cuentas; se vincula después mediante el flujo de pairing.
- El username es elegido por el usuario, se normaliza a minúsculas y debe ser
  único incluyendo el historial reservado.
- Teléfono: entre 7 y 15 dígitos; se permiten `+`, espacios, paréntesis y guiones.
- La contraseña debe contener mayúscula, minúscula, número y carácter especial.
- Términos y aviso de privacidad deben aceptarse explícitamente.
- Las versiones legales y las fechas UTC se determinan y guardan en el servidor.
- Las casillas del frontend nunca deben aparecer seleccionadas por defecto.
- `confirmPassword` es exclusivamente una validación del frontend.

## Compatibilidad legacy

Las solicitudes con `registrationVersion=1` o sin esa propiedad mantienen el
registro histórico: el backend puede generar el username y no exige teléfono ni
aceptación legal en la misma petición. Esta compatibilidad es transitoria y no
debe utilizarse en el frontend nuevo.

Una cuenta legacy puede aceptar el contrato vigente mediante:

```http
POST /api/v1/profile/onboarding/legal-acceptance
Authorization: Bearer <token>
Content-Type: application/json
```

```json
{
  "contractVersion": 2,
  "termsAccepted": true,
  "privacyAccepted": true
}
```

La operación es idempotente. Si la versión legal cambia, el servidor actualiza
la versión y registra una nueva fecha de aceptación.

## Estado inicial del onboarding

Una cuenta creada con el contrato completo inicia con:

- `registrationContractVersion=2`;
- `currentStep=3` porque cuenta, identidad pública y datos personales ya están
  completos;
- términos y privacidad aceptados con versión y timestamp UTC;
- ficha médica en `Pending`;
- estado general `Pending`.

La ficha médica sigue siendo opcional: puede marcarse `Completed` o `Skipped`.
El onboarding se completa al llegar al paso 8, tener ficha médica completada u
omitida y contar con la aceptación legal requerida para la versión del contrato.

## Revocación y consentimientos opcionales

La aceptación histórica del aviso de privacidad no se borra con
`PUT /api/v1/profile/onboarding`. Los consentimientos opcionales de ubicación y
procesamiento de patrones de conducción sí pueden habilitarse o deshabilitarse
independientemente. La eliminación de cuenta sigue disponible para ejercer la
baja completa del servicio.
