#!/usr/bin/env python3
import json
import os
from pathlib import Path
import sys

raw = os.getenv("FIREBASE_CREDENTIALS", "").strip()
path = os.getenv("FIREBASE_CREDENTIALS_PATH", "").strip()

if raw:
    try:
        data = json.loads(raw)
    except json.JSONDecodeError as exc:
        print(f"ERROR: FIREBASE_CREDENTIALS no contiene JSON válido: {exc}")
        sys.exit(1)
elif path:
    credential_path = Path(path).expanduser()
    if not credential_path.is_file():
        print(f"ERROR: no existe {credential_path}")
        sys.exit(1)
    data = json.loads(credential_path.read_text(encoding="utf-8"))
else:
    print("ERROR: define FIREBASE_CREDENTIALS o FIREBASE_CREDENTIALS_PATH.")
    sys.exit(1)

required = ("type", "project_id", "client_email", "private_key")
missing = [key for key in required if not data.get(key)]
if missing:
    print("ERROR: faltan campos de service account:", ", ".join(missing))
    sys.exit(1)

if data.get("type") != "service_account":
    print("ERROR: las credenciales no son de tipo service_account.")
    sys.exit(1)

print("ImpactX Firebase configuration: OK")
print("project_id:", data["project_id"])
print("client_email:", data["client_email"])
print("No se envió ninguna notificación durante esta comprobación.")
