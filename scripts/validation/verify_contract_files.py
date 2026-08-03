#!/usr/bin/env python3
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[2]
required = {
    "ImpactXv1/Core/ApiContract/ApiContractDefinition.cs": [
        'ContractVersion = "2026.08.03"',
        'ContractStatus = "frozen"',
        'SupportedClients = ["web", "mobile", "wearable"]',
    ],
    "ImpactXv1/Controllers/ApiContractController.cs": [
        '[Route("api/v1/meta")]',
        '[HttpGet("contract")]',
        '[HttpGet("clients/{client}")]',
    ],
    "ImpactXv1/Program.cs": [
        "app.MapOpenApi();",
        "app.UseMiddleware<ApiContractHeadersMiddleware>();",
        "app.UseMiddleware<LegacyDeprecationMiddleware>();",
    ],
    "docs/FRONTEND_API_HANDOFF.md": [
        "/api/v1/meta/contract",
        "/openapi/v1.json",
    ],
}

errors: list[str] = []
for relative, markers in required.items():
    path = ROOT / relative
    if not path.is_file():
        errors.append(f"Falta archivo requerido: {relative}")
        continue
    content = path.read_text(encoding="utf-8")
    for marker in markers:
        if marker not in content:
            errors.append(f"Falta marcador en {relative}: {marker}")

controllers = ROOT / "ImpactXv1" / "Controllers"
route_pattern = re.compile(r'\[Route\("([^"]+)"\)\]')
v1_routes: list[str] = []
for path in sorted(controllers.glob("*.cs")):
    content = path.read_text(encoding="utf-8")
    v1_routes.extend(route for route in route_pattern.findall(content) if "api/v1" in route)

if not v1_routes:
    errors.append("No se detectaron rutas api/v1 en controladores.")

if errors:
    print("ImpactX contract verification: FAILED")
    for error in errors:
        print(f"- {error}")
    sys.exit(1)

print("ImpactX contract verification: OK")
print(f"Detected v1 controller route attributes: {len(v1_routes)}")
print("Contract version: 2026.08.03")
