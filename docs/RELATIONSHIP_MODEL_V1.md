# Modelo de relaciones de ImpactX

Contrato API: `2026.08.04`.

ImpactX mantiene tres relaciones independientes. Una misma persona puede ocupar más de un rol, pero un rol no concede automáticamente los permisos de otro.

## Miembro del plan familiar

- Hereda el plan, la cuota de vehículos y los beneficios comerciales del titular.
- Cuenta dentro de la capacidad total del plan: Gratuito 2 personas, Estándar 3 y Premium 6, incluyendo al titular.
- No obtiene por sí mismo acceso a viajes, ubicación, alertas, telemetría o ficha médica de otra persona.
- Solo el titular puede cambiar, renovar o cancelar el plan y administrar sus miembros.

## Monitor

- Es una relación direccional entre `MonitorUserId` y `MonitoredUserId`.
- El monitor puede consultar únicamente los recursos autorizados por la persona monitoreada.
- La ficha médica requiere permiso y consentimiento explícitos.
- La persona monitoreada administra los permisos y puede bloquear o revocar la relación.
- Al crear una relación se debe indicar si el usuario solicita que otra persona lo monitoree o si solicita monitorear a otra persona.

## Contacto de emergencia

- Es una persona priorizada para apoyo durante una emergencia.
- Puede definirse como contacto principal y mantener una relación aceptada, rechazada o bloqueada.
- No recibe acceso continuo a viajes, ubicación, telemetría o ficha médica solo por ser contacto.
- Puede existir también como monitor cuando ambas relaciones se crean y aceptan por separado.

## Integración con el plan familiar

Cuando el titular marca `CreateMonitoringRelationship` en una invitación familiar, la persona invitada se convierte en monitor del titular al aceptar. El backend repara relaciones antiguas creadas en sentido inverso y conserva los permisos previamente autorizados.

## Mensajes rápidos

Los mensajes solo están permitidos entre personas con una relación de monitoreo aceptada y permiso de mensajería. La API permite marcar como leídos todos los mensajes entrantes de una conversación al abrirla.
