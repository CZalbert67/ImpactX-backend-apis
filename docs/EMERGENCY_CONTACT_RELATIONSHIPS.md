# Relaciones internas de contactos de emergencia

## Objetivo

El contrato V1 reemplaza el contacto externo de nombre/teléfono por una relación
explícita entre cuentas ImpactX. Familia, monitoreo y contacto de emergencia son
entidades independientes: aceptar una no crea automáticamente las otras.

## Persistencia

Se reutiliza el contenedor existente `ContactosEmergencia` con partition key
`/usuarioId`; no se crea, borra ni migra destructivamente ningún contenedor.

Los documentos V1 se distinguen por `publicContactId` y contienen:

- propietario interno (`usuarioId`, nunca expuesto);
- usuario contacto interno cuando ya existe (`contactUserId`, nunca expuesto);
- identificadores públicos del destinatario;
- correo normalizado solo para preinvitaciones;
- hash del código de invitación;
- estado y fechas UTC;
- parentesco, prioridad e indicador principal.

Un documento histórico sin `publicContactId` se interpreta como
`LegacyUnverified`. El endpoint V1 no lo devuelve y ninguna operación V1 lo
convierte silenciosamente en una relación aceptada.

## Estados

- `Pending`: invitación vigente.
- `Accepted`: relación operativa y contabilizada.
- `Rejected`: rechazo del destinatario.
- `Revoked`: terminada por cualquiera de las partes.
- `Blocked`: terminada y no permite nuevas invitaciones entre las cuentas.
- `Expired`: invitación no utilizada durante siete días.
- `LegacyUnverified`: documento anterior sin aceptación interna.

Solo `Accepted` puede marcarse como principal y contar como contacto activo.
El dashboard y las reglas de seguridad ignoran documentos `LegacyUnverified`.

## Seguridad

- Solo web y mobile pueden administrar estas relaciones.
- Se acepta exactamente uno de: username, `PublicProfileId` o email.
- El código manual tiene siete días de vigencia y se entrega una sola vez.
- Cosmos conserva solo `SHA-256` del código mediante `InvitationCodeHasher`.
- Aceptar y rechazar recibe el código en JSON, nunca en URL o query string.
- Un tercero recibe 404 y no puede confirmar si la invitación existe.
- La respuesta pública no contiene GUID internos, teléfono, email completo ni
  hash del código.
- Bloqueos se verifican en ambas direcciones.
- Invitaciones pendientes no consumen cuota; la cuota se valida al aceptar.

## Compatibilidad

`/api/contacts` continúa como ruta legacy para evitar romper clientes antiguos
durante el desarrollo. `/api/v1/contacts` usa exclusivamente el contrato interno
aceptado. La retirada definitiva de la ruta legacy se hará después de migrar el
frontend y los clientes móviles.
