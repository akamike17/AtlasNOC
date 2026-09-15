# Backup and restore

El procedimiento LAB está automatizado en `scripts/Invoke-AtlasNocLabBackupRestore.ps1`. Usa `mysqldump --single-transaction --routines --triggers --events`, calcula SHA-256, restaura en una base aislada y verifica el número de tablas.

El script rechaza nombres que no contengan `test`, `lab`, `e2e`, `integration` o `runtime`; no almacena credenciales en el repositorio. La operación demostrada fue `atlasnoc_test` → `atlasnoc_lab_restore_test` con MySQL local.

Producción requiere backup externo, ventana aprobada, validación de checksum y plan de rollback; este script no puede apuntar a una base productiva.
