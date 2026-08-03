# ImpactX Backend API

Backend unificado para los clientes web, móvil y Samsung Galaxy Watch 8 de ImpactX.

## Estado del contrato

- API canónica: `/api/v1/*`
- OpenAPI: `/openapi/v1.json`
- Contrato consumible por frontend: `/api/v1/meta/contract`
- Versión congelada: `2026.08.04`
- Clientes JWT soportados: `web`, `mobile`, `wearable`

Las rutas legacy `/api/*` se conservan temporalmente para compatibilidad y devuelven headers de deprecación. El frontend nuevo debe usar únicamente `/api/v1/*`.

## Validación local

```bash
bash scripts/validation/validate_final_backend.sh
```

El script compila en Release, ejecuta toda la suite, corre las regresiones de seguridad, revisa secretos y verifica el diff.

## Configuración local

Usa User Secrets o variables de entorno; no guardes claves en el repositorio.

```bash
dotnet user-secrets --project ImpactXv1 set "Jwt:Secret" "<secret-de-al-menos-32-caracteres>"
dotnet user-secrets --project ImpactXv1 set "AzureCosmosDb:Key" "<cosmos-key>"
```

Para el frontend local, configura `Cors:AllowedOrigins:0=http://localhost:5173`.

## Documentación de entrega

- `docs/FRONTEND_API_HANDOFF.md`
- `docs/BACKEND_FINAL_HANDOFF.md`
- `docs/PRODUCTION_VALIDATION_RUNBOOK.md`
- `docs/API_CLIENT_CAPABILITY_MATRIX.md`

No se requiere Docker para desarrollar, probar o ejecutar este proyecto.
