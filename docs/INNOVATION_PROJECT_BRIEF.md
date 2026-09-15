# Innovation Project Brief

AtlasNOC integra discovery evidence-based, grafo operacional y procesos ISP/WISP en una sola cabina.

El diferenciador es conservar evidencia y separar observado, monitoreado, gestionable y controlado; además conecta infraestructura con clientes, servicios, billing, soporte, impacto y auditoría.

La demo LAB crea cuatro zonas y una distribución de 100 clientes (25 por zona), superando el mínimo 4×5, y valida códigos, servicios, capacidad y cuentas. Runtime demuestra 61 nodos/60 links y E2E demuestra el flujo comercial y el reinicio.

No se declaran IA, certificaciones, hardware probado ni ahorros no medidos. Los drivers de simulación están marcados `SIMULATED`/`UNSUPPORTED` y el control físico requiere dependencia externa autorizada.

El flujo técnico conecta alcance de red, evidencia de discovery, topología verdadera y estado operativo con la relación Customer → Service → Zone → Asset → Billing → Support. Cada transición conserva identidad, actor, auditoría y un resultado verificable; la topología mantiene nodos aislados cuando no existe evidencia de enlace.

La arquitectura modular separa dominio, persistencia MySQL, Web y Worker, con retries EF ejecutados de forma compatible con transacciones. La demostración LAB incluye 4 zonas × 100 clientes, 61 nodos/60 enlaces, concurrencia de assets, idempotencia de billing, backup/restore y reinicio.

El potencial comercial es una cabina auditable para ISP/WISP/NOC, no una promesa de control universal. Antes de vender se requieren legal/licencias, seguridad externa, SLA/carga, operación de soporte, HA/backup externo y validación de modelos/firmware físicos.
