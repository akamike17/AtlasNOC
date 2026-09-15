# Product Truth Matrix

| Área | Estado | Evidencia |
|---|---|---|
| Discovery/topología | VERIFIED IN LAB | Runtime LAB: 61 nodos, 60 links, rescan idempotente y restart de persistencia |
| Billing idempotente | VERIFIED | Smoke REST: cargo/replay; transacción serializable y pruebas MySQL de integración |
| Zonas | VERIFIED IN LAB | Runtime: 4 zonas y 100 servicios distribuidos 25 por zona; API original probado |
| Control físico | NOT PROVEN — EXTERNAL DEPENDENCY | Requiere equipo de laboratorio explícitamente autorizado |
| Comercial end-to-end | VERIFIED IN LAB | Playwright y smoke recorren Customer, Service, Asset, Billing, Support, Incident y Credit |

La matriz sólo afirma `VERIFIED` cuando hay código y ejecución reproducible. `NOT PROVEN — EXTERNAL DEPENDENCY` se reserva para hardware físico no conectado y revisión de licencias de redistribución.
