# Commercial Readiness

La base es instalable con .NET 8/MySQL, tiene migraciones, health endpoints, Identity, API scopes, workers, simulador y documentación operativa.

Reliability: Unit 178/178, Integration 10/10, Runtime 9/9 y E2E 16/16 pasan con una conexión LAB aislada. El flujo E2E valida setup, login, vistas, REST original, billing, soporte y reinicio.

Recovery: el script de backup/restore LAB usa checksum SHA-256 y guardas de nombre. Upgrade se ejecutó desde la migración anterior a HEAD sin reset productivo.

Billing y assets tienen idempotencia y concurrencia comprobadas en MySQL. Hardware real permanece `NOT PROVEN — EXTERNAL DEPENDENCY` porque requiere equipo, credenciales y target autorizados.
