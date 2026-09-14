# AtlasNOC — matriz de verdad del producto

Estado al 2026-09-14. `READY` sólo se usa cuando existe código y validación; `PARTIAL` indica que hay piezas, pero falta una integración o prueba completa.

| Área | Estado | Evidencia |
|---|---|---|
| Discovery | PARTIAL | Pipeline ICMP/SNMP/ARP y persistencia existentes |
| Topology | PARTIAL | Grafo persistido y correlación con evidencia |
| Monitoring | PARTIAL | Polling y métricas existentes |
| WISP correlation | PARTIAL | Observaciones WISP y workflow de CPE pendiente/autorizada/rechazada; falta driver radio probado |
| Billing / Payments | PARTIAL | Cuenta corriente, cargos, pagos manuales, recibos, convenios y reconexión validados por pruebas de dominio/API |
| Support / Routes | PARTIAL | Tickets, interacciones, incidentes raíz, visitas, SLA, créditos y planificador con ETA implementados; falta optimización geográfica real |
| Inventory / Backups | PARTIAL | Assets con asignación/recuperación/inspección y revisiones de configuración con hash; restore compatible aún requiere ejecución operativa |
| Coverage | PARTIAL | Evaluación por infraestructura cercana, tecnología, distancia, capacidad y evidencia; mapa geográfico y capacidad avanzada pendientes |
| MikroTik control | PARTIAL | Driver REST real con capacidades y auditoría; ejecución física no probada |
| Ubiquiti control | UNSUPPORTED | No afirmar soporte genérico |
| GPON / DSL | UNSUPPORTED | Sin driver probado |
| Physical lab | NOT PROVEN | Las pruebas de laboratorio requieren entorno opt-in |

El endpoint `/api/operations/snapshot` expone únicamente estado persistido y marca el nivel de verdad de cada dispositivo; no simula capacidades ni acciones.
