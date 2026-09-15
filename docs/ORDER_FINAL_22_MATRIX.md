# Matriz estricta de verificación de la Orden Final (1–22)

Cada fila exige código y evidencia; no se considera prueba una afirmación documental.

| Punto | Código/prueba/evidencia verificable |
|---:|---|
| 1 | Migraciones EF versionadas y snapshot. Fresh usa `MigrateAsync`; upgrade LAB ejecutado desde `20260915145841_AttachZonesToServices` hasta `20260915154954_AddInventoryAssetConcurrency`. |
| 2 | Ledger serializable/idempotente y asset con `RowVersion`; `Concurrent_asset_assignment_has_one_winner` pasa con dos contextos MySQL. |
| 3 | `All_primary_views_are_navigable_for_admin` recorre las vistas primarias y `OperationsApiController` expone operaciones autenticadas. |
| 4 | Runtime prueba 61 nodos, 60 enlaces derivados, ausencia de duplicados e idempotencia de rescan. |
| 5 | `Four_zones_and_one_hundred_customers_preserve_business_invariants` prueba cuatro zonas con distribución 25/25/25/25, superando el mínimo 4×5. |
| 6 | `NetworkZone` implementa estados/capacidad/reservas; activación, cancelación y cambio de plan actualizan reservas. |
| 7 | Smoke recorre Customer→Plan→Service→activación→CPE→Asset y conserva `Unsupported` si no existe capability de hardware. |
| 8 | Smoke ejecuta charge/replay, consulta, promesa, payment, default y crédito; E2E concurrente demuestra un solo cargo y un replay para la misma clave; billing usa idempotencia y transacción serializable. |
| 9 | Smoke crea ticket/interacción/incidente/visita/crédito; crédito valida evidencia de afectación del cliente. |
| 10 | Tests E2E de login/lockout/API keys; `Program.cs` configura antiforgery, rate limit, HTTPS fuera de Testing y errores API sanitizados. |
| 11 | Health, workers, shutdown/restart y script LAB de backup/restore están implementados; restart E2E pasa 1/1. |
| 12 | Documentación técnica y operativa está versionada bajo `docs/`; cada documento se revisa contra las rutas reales. |
| 13 | Innovation brief declara propuesta, evidencia y límites de hardware sin presentar simulación como control físico. |
| 14 | Commercial readiness referencia build, suites, migraciones, recuperación y operación reproducible. |
| 15 | NuGet está centralizado en `Directory.Packages.props`; JS local vive en `wwwroot/lib`, sin CDN obligatorio. Licencias requieren revisión antes de redistribuir. |
| 16 | Guardas de conexión y script de backup rechazan DBs sin marcador LAB/test; no se reseteó una base real. |
| 17 | Ejecución LAB posterior a la migración: Unit 178/178, Integration 10/10, Runtime 9/9, E2E 19/19; build sin errores/warnings. |
| 18 | Fixture Playwright usa puerto loopback dinámico y captura page errors, consola, requests fallidas y respuestas 4xx/5xx inesperadas. |
| 19 | La causa EF/retry fue reproducida, instrumentada, corregida con `CreateExecutionStrategy` y repetida en integración/E2E. |
| 20 | Drivers LAB distinguen `SIMULATED`/`UNSUPPORTED`; no se afirma control de hardware no conectado. |
| 21 | No se usó force/reset/clean destructivo; commits lógicos publicados y artefactos de prueba fuera del índice. |
| 22 | La entrega se valida por las filas 1–21 y sus comandos; no se infiere cumplimiento por el número de tests. |

## Ejecución LAB

```powershell
$env:ATLASNOC_TEST_CONNECTION = $env:LAB_CONNECTION_STRING
dotnet build .\AtlasNOC.sln -c Release --no-restore
dotnet test .\AtlasNOC.sln -c Release --no-build
.\scripts\Invoke-AtlasNocLabBackupRestore.ps1 -Server 127.0.0.1 -User Admin -Password $env:MYSQL_LAB_PASSWORD -SourceDatabase atlasnoc_test -RestoreDatabase atlasnoc_lab_restore_test
```

Las credenciales se inyectan desde el entorno y no se versionan.
