# Operations runbook
Validar configuración, migraciones, health live/ready, logs sin secretos, workers y último discovery/poll. Ante fallo, aislar el item, revisar evidencia y conservar historial.

Arranque: revisar entorno, aplicar migraciones, verificar `/health/live` y `/health/ready`, comprobar versión/schema y abrir `/setup` sólo durante instalación inicial. Mantener Web y Worker separados en Production.

Operación: revisar leases de DiscoveryRun, métricas recientes, alertas e incidentes. Topology debe mostrar nodos aislados cuando no hay observación; nunca añadir un link manual como confirmado.

Recuperación LAB: usar el script de backup/restore con base marcada; registrar SHA-256, conteo de tablas y health posterior. No resetear ni truncar bases reales.
