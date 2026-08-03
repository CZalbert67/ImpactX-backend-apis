# Modos de inicialización de Cosmos DB

## `Ensure`

Uso: desarrollo y pruebas controladas.

- Puede crear la base o contenedores faltantes.
- Valida partition keys y TTL.
- Puede sembrar planes.
- Es idempotente y nunca borra ni recrea contenedores incompatibles.

## `ValidateOnly`

Uso: producción.

- Comprueba acceso a la base.
- Verifica los 23 contenedores de `CosmosContainerCatalog`.
- Verifica partition key y TTL.
- No crea, modifica, elimina ni siembra recursos.
- Un desajuste deja `/health/ready` en 503, mientras `/health/live` continúa indicando que el proceso está vivo.

Producción usa:

```json
"DatabaseInitialization": {
  "Enabled": true,
  "Mode": "ValidateOnly"
}
```

Toda creación o migración de contenedores se realiza de forma controlada fuera
del proceso productivo y requiere autorización del responsable de Azure.
