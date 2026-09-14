# AtlasNOC — matriz de verdad del producto

Estado al 2026-09-14. `READY` sólo se usa cuando existe código y validación; `PARTIAL` indica que hay piezas, pero falta una integración o prueba completa.

| Área | Estado | Evidencia |
|---|---|---|
| Discovery | PARTIAL | Pipeline ICMP/SNMP/ARP y persistencia existentes |
| Topology | PARTIAL | Grafo persistido y correlación con evidencia |
| Monitoring | PARTIAL | Polling y métricas existentes |
| WISP correlation | PARTIAL | Conectores/observaciones existentes; falta flujo completo cliente-servicio |
| Billing / Payments | UNSUPPORTED | No hay cuenta corriente ni proveedor neutral completo |
| Support / Routes | UNSUPPORTED | No hay workflow completo verificable |
| Inventory / Backups | PARTIAL | Inventario de dispositivos; falta versionado de configuración |
| Coverage | UNSUPPORTED | No hay resolución de cobertura operativa |
| MikroTik control | PARTIAL | Lectura/conectores; control mutante no probado como contrato completo |
| Ubiquiti control | UNSUPPORTED | No afirmar soporte genérico |
| GPON / DSL | UNSUPPORTED | Sin driver probado |
| Physical lab | NOT PROVEN | Las pruebas de laboratorio requieren entorno opt-in |

El endpoint `/api/operations/snapshot` expone únicamente estado persistido y marca el nivel de verdad de cada dispositivo; no simula capacidades ni acciones.
