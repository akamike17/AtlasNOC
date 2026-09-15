# Troubleshooting
DB: revisar `/health/ready` y migraciones. Discovery: revisar alcance/credenciales. Topología: revisar observations. Billing: revisar account, ledger e idempotency key. No resetear DB real.

Si un POST devuelve 405, revisar el log correlacionado y la excepción raíz antes de cambiar rutas: `UseExceptionHandler` puede ocultar una excepción interna. El endpoint original de zonas es `/api/operations/zones`.

Si falla una transacción MySQL con retry strategy, envolver la unidad completa en `Database.CreateExecutionStrategy().ExecuteAsync`, incluyendo queries, transacción y `SaveChanges`. No quitar retries como atajo.

Si falla asignación concurrente de asset, revisar `RowVersion`: el segundo operador debe recibir conflicto controlado y recuperar el asset antes de reasignarlo.
