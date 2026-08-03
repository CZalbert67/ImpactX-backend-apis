# Cosmos DB — operación segura de ImpactX

La API usa la cuenta y base de datos configuradas mediante `AzureCosmosDb`. Las claves nunca deben quedar en archivos versionados.

## Variables recomendadas

```bash
export AzureCosmosDb__Endpoint="https://impactx-db-west-final.documents.azure.com:443/"
export AzureCosmosDb__DatabaseName="ImpactX-Data"
export AzureCosmosDb__Key="<secret>"
export UseCosmosDb="true"
```

## Inicialización

En producción se usa:

```json
{
  "DatabaseInitialization": {
    "Enabled": true,
    "Mode": "ValidateOnly"
  }
}
```

`ValidateOnly` comprueba base de datos, contenedores, partition keys y TTL sin crear, eliminar ni modificar recursos.

## Validación de esquema de solo lectura

```bash
bash scripts/smoke/cosmos_schema_readonly.sh
```

El script usa Azure CLI para consultar el catálogo y compara los 23 contenedores esperados. No ejecuta comandos de creación, actualización o eliminación.

## Retención

- Viajes y telemetría: 90 días.
- Alertas e incidentes: 365 días.
- Notificaciones: 30 días.

La eliminación de cuenta anonimiza identidad y respeta los TTL operativos; no realiza borrados masivos destructivos.
