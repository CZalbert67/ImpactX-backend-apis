#!/usr/bin/env bash
set -euo pipefail

BASE_URL="${IMPACTX_BASE_URL:-https://impactx-api-backend-h0eyf9c4fxd8dsbc.westus-01.azurewebsites.net}"
BASE_URL="${BASE_URL%/}"
EMAIL="${IMPACTX_SMOKE_EMAIL:-}"
PASSWORD="${IMPACTX_SMOKE_PASSWORD:-}"
CLIENT="${IMPACTX_SMOKE_CLIENT:-web}"

request() {
  local method="$1"
  local path="$2"
  shift 2
  echo "[$method] $path"
  curl --silent --show-error --fail-with-body \
    --request "$method" \
    --header "Accept: application/json" \
    "$@" \
    "$BASE_URL$path"
  echo
}

request GET "/health/live"
request GET "/health/ready"
request GET "/openapi/v1.json" >/dev/null
request GET "/api/v1/auth/registration-contract"
request GET "/api/v1/meta/contract" >/dev/null
request GET "/api/v1/meta/clients/web"

if [[ -n "$EMAIL" && -n "$PASSWORD" ]]; then
  LOGIN_PAYLOAD="$(python3 - "$EMAIL" "$PASSWORD" "$CLIENT" <<'PY'
import json, sys
print(json.dumps({"identifier": sys.argv[1], "password": sys.argv[2], "client": sys.argv[3]}))
PY
)"

  LOGIN_JSON="$(curl --silent --show-error --fail-with-body \
    --request POST \
    --header "Content-Type: application/json" \
    --data "$LOGIN_PAYLOAD" \
    "$BASE_URL/api/v1/auth/login")"

  TOKEN="$(python3 -c 'import json,sys; print(json.load(sys.stdin)["token"])' <<<"$LOGIN_JSON")"
  request GET "/api/v1/profile" --header "Authorization: Bearer $TOKEN" >/dev/null
  request GET "/api/v1/subscriptions/effective" --header "Authorization: Bearer $TOKEN" >/dev/null
  request GET "/api/v1/vehicles" --header "Authorization: Bearer $TOKEN" >/dev/null
  request GET "/api/v1/incidents/active" --header "Authorization: Bearer $TOKEN" >/dev/null
fi

echo "ImpactX Azure API smoke: OK"
