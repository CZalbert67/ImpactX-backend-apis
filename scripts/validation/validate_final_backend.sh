#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

LOG_FILE="${1:-$HOME/Documentos/ImpactX-backend-final-v9-validation.txt}"
mkdir -p "$(dirname "$LOG_FILE")"

set -o pipefail
(
  set -euo pipefail

  echo "========== IMPACTX BACKEND FINAL V9 =========="
  echo "utc=$(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "branch=$(git branch --show-current 2>/dev/null || echo unknown)"
  git status -sb 2>/dev/null || true

  echo
  echo "========== CLEAN =========="
  dotnet clean ImpactX.slnx --configuration Release --verbosity quiet

  echo
  echo "========== RESTORE =========="
  dotnet restore ImpactX.slnx

  echo
  echo "========== BUILD RELEASE =========="
  dotnet build ImpactX.slnx --configuration Release --no-restore

  echo
  echo "========== TEST SUITE =========="
  dotnet test ImpactX.slnx \
    --configuration Release \
    --no-build \
    --logger "console;verbosity=minimal" \
    --blame-hang \
    --blame-hang-timeout 60s

  echo
  echo "========== SECURITY REGRESSION =========="
  dotnet test ImpactX.slnx \
    --configuration Release \
    --no-build \
    --filter "Category=Security" \
    --logger "console;verbosity=minimal"

  echo
  echo "========== SECRET SCAN =========="
  python3 scripts/security/check_hardcoded_secrets.py

  echo
  echo "========== CONTRACT STATIC CHECK =========="
  python3 scripts/validation/verify_contract_files.py

  echo
  echo "========== DIFF CHECK =========="
  git diff --check

  echo
  echo "========== RESUMEN =========="
  git diff --stat
) 2>&1 | tee "$LOG_FILE"

echo
echo "Validación guardada en: $LOG_FILE"
