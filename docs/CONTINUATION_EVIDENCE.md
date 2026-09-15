# Evidencia técnica de continuidad

## Alcance de esta iteración

Esta iteración mantiene el contrato REST original de operaciones: la creación de zonas sigue siendo `POST /api/operations/zones`. No se añadió `zones/create`, no se modificó el smoke para cambiar el endpoint y no se sustituyó el diagnóstico por una respuesta 405 artificial.

## Causa raíz y corrección

El host limpio devolvía 405 durante setup y, posteriormente, 500 en operaciones transaccionales. La causa no era routing: `UseExceptionHandler` reejecutaba el error y ocultaba la excepción original. Pomelo MySQL tenía `EnableRetryOnFailure` activo, pero EF Core rechaza una transacción iniciada por la aplicación fuera de `IExecutionStrategy.ExecuteAsync`.

Se corrigieron todas las transacciones identificadas en el flujo crítico:

1. `SetupService.SetupAsync`: lock MySQL, validación, roles, administrador, organización y `SaveChanges` se ejecutan como una unidad dentro de la estrategia de ejecución.
2. `OperationsApiController.CreateCustomer`: cliente y `BillingAccount` conservan atomicidad y ahora se reintentan como una unidad.
3. `AddLedger`: idempotencia, saldo, entrada contable, aplicación a promesas y recibo quedan dentro de la misma unidad reintentable serializable.
4. `UserAdministrationService`: operaciones protegidas de administración usan la misma envoltura antes de abrir la transacción.

El middleware de API registra endpoint, excepción raíz, `InnerException`, `DbUpdateException` y ruta SQL disponible, pero devuelve al cliente sólo un detalle correlacionable y no secretos. Así, un fallo interno vuelve a ser 500 observable y no un falso 405.

## Verificaciones ejecutadas

| Verificación | Resultado | Aislamiento |
|---|---:|---|
| Build Release solución completa | 0 errores, 0 warnings | repositorio local |
| Unit | 178/178 | sin DB |
| Integration | 8/8 | MySQL `atlasnoc_test_continuation_integration` |
| Runtime | 9/9 | MySQL `atlasnoc_test_continuation_runtime` |
| E2E lifecycle completo | 1/1 | host loopback dinámico + MySQL `*_e2e` |
| Auditoría de vistas aislada | 1/1 | host loopback dinámico + MySQL dedicada |
| Smoke REST original | 1/1; 39 checkpoints, contrato original `/api/operations/zones` | host loopback dinámico + MySQL dedicada |

El contrato correcto es 39: cuatro altas de zona, una consulta de listado y treinta checkpoints de operaciones de cierre. El POST original se enruta, autentica y persiste las cuatro zonas sin ruta alternativa.

## Reproducibilidad

Las suites reciben `ATLASNOC_TEST_CONNECTION`, derivan una base con sufijo de test y el fixture E2E elige un puerto loopback libre mediante `TcpListener`. El fixture termina el árbol de procesos que él mismo inicia. Los procesos `dotnet` compartidos de MSBuild/Roslyn no se detienen porque no son servidores AtlasNOC ni pertenecen al fixture.

Las ejecuciones deben usar una base cuyo nombre contenga `_test`, `_e2e` o `_integration`; el guard de seguridad impide accidentalmente tocar una base productiva. Las credenciales se inyectan por variable de entorno y no se escriben en documentación ni logs.

## Evidencia adicional cerrada

El script `scripts/Invoke-AtlasNocLabBackupRestore.ps1` ejecuta `mysqldump --single-transaction`, recrea únicamente una base cuyo nombre contiene `test/lab/e2e/integration/runtime`, restaura el SQL, calcula SHA-256 y verifica el número de tablas. Se ejecutó contra `atlasnoc_test` hacia `atlasnoc_lab_restore_test` con resultado exitoso.

`Restart_preserves_schema_and_admin_login` arranca el Web en un puerto loopback libre, configura el administrador, termina sólo el árbol del proceso del fixture, relanza el mismo host, comprueba `/health/live` y valida login posterior. Resultado: 1/1.
