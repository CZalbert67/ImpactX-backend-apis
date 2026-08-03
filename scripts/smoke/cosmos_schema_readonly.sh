#!/usr/bin/env bash
set -euo pipefail

RESOURCE_GROUP="${IMPACTX_RESOURCE_GROUP:-ImpactX-West-RG}"
ACCOUNT="${IMPACTX_COSMOS_ACCOUNT:-impactx-db-west-final}"
DATABASE="${IMPACTX_COSMOS_DATABASE:-ImpactX-Data}"
TMP_FILE="$(mktemp)"
trap 'rm -f "$TMP_FILE"' EXIT

command -v az >/dev/null 2>&1 || {
  echo "ERROR: Azure CLI no está instalado."
  exit 1
}

az cosmosdb sql container list \
  --resource-group "$RESOURCE_GROUP" \
  --account-name "$ACCOUNT" \
  --database-name "$DATABASE" \
  --output json > "$TMP_FILE"

python3 - "$TMP_FILE" <<'PY'
import json
import sys

expected = {
    "Notificaciones": ("/usuarioId", 2592000),
    "Rutas": ("/usuarioId", -1),
    "ChatThreads": ("/usuarioId", -1),
    "Monitores": ("/usuarioId", -1),
    "Alertas": ("/usuarioId", 31536000),
    "PasswordResetTokens": ("/usuarioId", 3600),
    "Wearables": ("/usuarioId", -1),
    "Pagos": ("/usuarioId", -1),
    "Usuarios": ("/id", -1),
    "Dispositivos": ("/usuarioId", -1),
    "ContactosEmergencia": ("/usuarioId", -1),
    "TelemetriaViaje": ("/viajeId", 7776000),
    "Incidentes": ("/usuarioId", -1),
    "Planes": ("/id", -1),
    "AppInvites": ("/usuarioId", 2592000),
    "RefreshTokens": ("/usuarioId", 604800),
    "Viajes": ("/usuarioId", 7776000),
    "Suscripciones": ("/usuarioId", -1),
    "Vehicles": ("/ownerUserId", -1),
    "FamilySubscriptions": ("/ownerUserId", -1),
    "MonitoringRelationships": ("/monitorUserId", -1),
    "QuickMessages": ("/recipientUserId", -1),
    "QuickMessageTemplates": ("/ownerKey", -1),
}

with open(sys.argv[1], encoding="utf-8") as handle:
    raw = json.load(handle)

actual = {}
for item in raw:
    resource = item.get("resource") or item
    name = item.get("name") or resource.get("id")
    paths = ((resource.get("partitionKey") or {}).get("paths") or [])
    partition_key = paths[0] if paths else None
    ttl = resource.get("defaultTtl", -1)
    actual[name] = (partition_key, ttl)

errors = []
for name, contract in expected.items():
    if name not in actual:
        errors.append(f"Falta contenedor: {name}")
    elif actual[name] != contract:
        errors.append(f"{name}: esperado {contract}, actual {actual[name]}")

unexpected = sorted(set(actual) - set(expected))
if unexpected:
    print("Aviso: contenedores adicionales:", ", ".join(unexpected))

if errors:
    print("ImpactX Cosmos schema validation: FAILED")
    for error in errors:
        print("-", error)
    sys.exit(1)

print(f"ImpactX Cosmos schema validation: OK ({len(expected)} contenedores)")
print("Operación de solo lectura; no se modificó Cosmos DB.")
PY
