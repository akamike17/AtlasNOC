# Backup and restore

El procedimiento LAB está automatizado en `scripts/Invoke-AtlasNocLabBackupRestore.ps1`. Usa `mysqldump --single-transaction --routines --triggers --events`, calcula SHA-256, restaura en una base aislada y verifica el número de tablas.

El script rechaza nombres que no contengan `test`, `lab`, `e2e`, `integration` o `runtime`; no almacena credenciales en el repositorio. La operación demostrada más reciente fue `atlasnoc_test_remate_upgrade_20260915` → `atlasnoc_lab_restore_remate_20260915` con MySQL local.

Producción requiere backup externo, ventana aprobada, validación de checksum y plan de rollback; este script no puede apuntar a una base productiva.

Uso LAB: `./scripts/Invoke-AtlasNocLabBackupRestore.ps1 -Server 127.0.0.1 -User $env:MYSQL_LAB_USER -Password $env:MYSQL_LAB_PASSWORD -SourceDatabase atlasnoc_test -RestoreDatabase atlasnoc_lab_restore_test`. Ambos nombres pasan el guard de seguridad y la contraseña sólo existe en el proceso.

El resultado esperado informa archivo, SHA-256, base destino y número de tablas; una restauración con cero tablas o un código MySQL distinto de cero es fallo. El script reescribe únicamente los selectores `CREATE/USE` del dump para que el `SOURCE` cargue en la base destino aislada.

La ejecución final registrada restauró 52 tablas en `atlasnoc_lab_restore_remate_20260915`, con SHA-256 `12E2C17B400C4831D34D8582C9175DFC11F40AC15BB5C075118D26D733C42CDE`. Después de restaurar se debe ejecutar health, `dotnet ef migrations list` y una consulta de integridad; el script no es un sustituto de retención externa, cifrado en reposo ni prueba de recuperación ante desastre.
