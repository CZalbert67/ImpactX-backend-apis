# ImpactX — runbook de validación final

## 1. Validación local completa

```bash
cd ~/Documentos/ImpactX-backend-apis
bash scripts/validation/validate_final_backend.sh
```

Resultado requerido:

- Build Release con 0 errores.
- Suite completa sin fallas ni omitidas.
- Regresión `Category=Security` aprobada.
- Escaneo de secretos con 0 violaciones.
- Verificación estática del contrato correcta.
- `git diff --check` limpio.

## 2. Variables de Azure App Service

Comprobar sin imprimir valores secretos:

```bash
az webapp config appsettings list \
  --resource-group ImpactX-West-RG \
  --name impactx-api-backend \
  --query "[?name=='UseCosmosDb' || name=='AzureCosmosDb__DatabaseName' || name=='DatabaseInitialization__Enabled' || name=='DatabaseInitialization__Mode' || name=='Cors__AllowedOrigins__0'].name" \
  --output table
```

Se requieren:

```text
UseCosmosDb=true
AzureCosmosDb__DatabaseName=ImpactX-Data
DatabaseInitialization__Enabled=true
DatabaseInitialization__Mode=ValidateOnly
Cors__AllowedOrigins__0=https://<frontend-final>
```

Las claves JWT, Cosmos y Firebase deben permanecer en App Settings o Key Vault, nunca en Git.

## 3. Cosmos DB de solo lectura

```bash
az login
bash scripts/smoke/cosmos_schema_readonly.sh
```

Debe reportar 23 contenedores correctos. El script no modifica recursos.

## 4. Firebase

Con JSON en variable de entorno:

```bash
export FIREBASE_CREDENTIALS='<service-account-json>'
python3 scripts/smoke/firebase_configuration_check.py
```

O mediante ruta local ignorada por Git:

```bash
export FIREBASE_CREDENTIALS_PATH="$HOME/.config/impactx/firebase-service-account.json"
python3 scripts/smoke/firebase_configuration_check.py
```

## 5. Smoke de Azure

Pruebas públicas:

```bash
bash scripts/smoke/azure_api_smoke.sh
```

Pruebas autenticadas opcionales:

```bash
export IMPACTX_SMOKE_EMAIL='usuario-prueba@example.com'
export IMPACTX_SMOKE_PASSWORD='<password>'
export IMPACTX_SMOKE_CLIENT='web'
bash scripts/smoke/azure_api_smoke.sh
```

No uses una cuenta real con información médica durante el smoke test.

## 6. Verificación de contrato

```bash
curl -fsS https://impactx-api-backend-h0eyf9c4fxd8dsbc.westus-01.azurewebsites.net/api/v1/meta/contract \
  | python3 -m json.tool
```

Debe devolver:

```text
apiVersion: v1
contractVersion: 2026.08.03
status: frozen
```

## 7. Commit y push

Solo después de completar todos los pasos anteriores:

```bash
git add -A
git commit -m "feat: completar backend ImpactX y congelar contrato API v1"
git push origin feat/backend-complete-v1
```

El merge a `main` se realiza después de revisar pipelines y evidencias.
