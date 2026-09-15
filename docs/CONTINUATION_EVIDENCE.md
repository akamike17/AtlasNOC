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
| Smoke REST original | alcanza todos los checkpoints; falla sólo aserción histórica 38/39 | host loopback dinámico + MySQL dedicada |

La aserción 38/39 no se usa para cambiar el contrato ni para esconder el 405: el smoke ya demuestra que el POST original se enruta, autentica, persiste cuatro zonas y continúa hasta las operaciones de cierre. Debe tratarse como pendiente de especificación de conteo, separado de la causa raíz ya corregida.

## Reproducibilidad

Las suites reciben `ATLASNOC_TEST_CONNECTION`, derivan una base con sufijo de test y el fixture E2E elige un puerto loopback libre mediante `TcpListener`. El fixture termina el árbol de procesos que él mismo inicia. Los procesos `dotnet` compartidos de MSBuild/Roslyn no se detienen porque no son servidores AtlasNOC ni pertenecen al fixture.

Las ejecuciones deben usar una base cuyo nombre contenga `_test`, `_e2e` o `_integration`; el guard de seguridad impide accidentalmente tocar una base productiva. Las credenciales se inyectan por variable de entorno y no se escriben en documentación ni logs.

## Pendientes separados

- Corregir la expectativa contable del smoke de 38 a la especificación real de 39 sólo mediante una decisión explícita sobre el contrato de checkpoints; no usar una ruta alternativa.
- Completar la auditoría de backup/restore LAB con una ejecución real del binario de backup y restauración en una instancia efímera, registrando checksum, conteo de tablas y migración posterior.
- Añadir una prueba E2E de reinicio que valide sesión, health y workers después de terminar y relanzar el proceso.
