# Auditoría Master End-to-End — AtlasNOC

Fecha: 2026-09-15  
Especificación auditada: `ATLASNOC_ESPECIFICACION_TOTAL_END_TO_END.md` (lectura completa, 48 secciones)

## Método

Se aplica el contrato rector `READ → TRACE → REPRODUCE → WRITE → BUILD → TEST → RUNTIME → E2E → BROWSER → REAUDIT`. La auditoría separa evidencia existente de capacidades únicamente declaradas. No se promueve un área a READY por compilación o HTTP 200.

## Evidencia base antes de esta auditoría

- Commit/push previo: `68038ce` en `origin/atlasnoc`.
- Build Release: PASS, 0 errores.
- Unit: 174 PASS.
- Integration: 8 PASS.
- Runtime: 8 PASS.
- E2E: 15 PASS, 0 skipped.
- La E2E cubre navegación primaria, topología de 6 nodos aislados, persistencia de interfaz/detalle y altas temporales de alert rule, subscriber, link manual, API key y user.
- Existen workers de discovery, polling, correlación, alertas, notificaciones y retención.

## Hallazgos Master — estado inicial

### A. Red, evidencia y topología — PARTIAL / evidencia fuerte

Existe modelo de Device, Interface, Link, NeighborObservation, DiscoveryRun, drivers, probes, correlación, polling y topología Cytoscape. Hay cobertura LAB-01 de 61 nodos, idempotencia y nodos aislados. Falta probar integralmente en E2E la cadena de usuario `Descubrir mi red` desde detección local hasta refresh de inventario y evidenciar todas las capacidades de detalle/acciones.

### B. Seguridad y bootstrap — PARTIAL

Identity, roles, cookies, API keys con hash/scopes, antiforgery y protección de credenciales existen. Debe auditarse configuración efectiva de rate limiting/CSP/HTTPS, cierre de `/setup`, no exposición de secretos en logs/respuestas y health real de Web/Worker/DB. Las credenciales entregadas en el chat no se incorporan al repositorio ni a archivos.

### C. Dominio comercial — PARTIAL / modelo base presente

El modelo base sí contiene Customer, CustomerService, ServicePlan, BillingAccount/BillingEntry, Prospect, CoverageCheck, InventoryAsset, TechnicianVisit, ServiceContract, InstallationOrder, CPE cases, promises, receipts, tickets e interacciones. También hay endpoints operacionales. El gap real es que varias invariantes end-to-end no están garantizadas: idempotencia de cargos/pagos, transacciones multi-entidad, claves únicas de negocio, validación de propiedad cruzada y un fixture E2E reproducible de 100 clientes + 4 zonas.

### D. Soporte, visitas, capacidad y fraude — PARTIAL

Hay servicios de soporte/diagnóstico, tickets, visitas, planner y CPE cases, pero no hay evidencia equivalente de capacity planner, coverage evidence geográfica, root incident comercial, fraude/CPE no autorizado y reemplazo de equipo con historial completo.

### E. Billing, pagos y suspensión — PARTIAL / riesgos de integridad

Hay persistencia, endpoints y reglas básicas, pero el flujo necesita idempotency key/periodo único, transacción explícita y separación estricta entre pago capturado, validado y aplicado. `PayAndReconnect` y `AddLedger` deben impedir doble aplicación concurrente y no emitir comprobante confirmado para pago pendiente.

### F. Simulador y escala — PARTIAL

El simulador de red y control existe y LAB-01 está probado. Falta fixture reproducible de 100 clientes + 4 zonas con invariantes de identidad, capacidad, billing, pagos, inventario, incidentes, restart y UI aceptable.

### G. UI y observabilidad — PARTIAL

Las vistas primarias son navegables y tienen mejoras de estados vacíos. Falta demostrar que cada operación tiene loading/empty/error/unauthorized/validation/success reales, y que System Health no declara OK cuando Worker/DB/jobs fallan. También falta una auditoría de botones/enlaces muertos por vista.

## Prioridades de defectos reproducibles

- **P1 Integridad de billing:** cargo mensual repetible sin clave idempotente; pago concurrente puede aplicar dos veces; `PaymentReceipt` se crea aunque el estado de captura sea pendiente.
- **P1 Propiedad y autorización de datos:** varios endpoints operacionales listan datos globales y las operaciones comerciales no verifican siempre el vínculo Customer→Service→Asset/Incident antes de mutar.
- **P1 Invariantes de esquema:** confirmar índices únicos para `Customer.ServiceCode`, asset tag/serial/MAC, códigos geográficos y evitar doble asignación de assets.
- **P2 Escala reproducible:** falta prueba/fixture de aceptación de 100 clientes y cuatro zonas, con restart, rescan, billing e incident impact.
- **P2 Cobertura/geografía:** `CoverageCheck` sólo persiste domicilio/estado/capacidad; no representa zona, coordenadas, infraestructura, tecnología ni evidencia suficiente para activar una zona.
- **P2 UI operativa:** la API está más completa que la navegación principal; falta demostrar operaciones comerciales desde UI con estados explícitos y sin botones muertos.

## Orden obligatorio de parcheo

1. Confirmar el mapa real de entidades/repositorios/servicios antes de crear equivalentes.
2. Cerrar el dominio comercial mínimo con persistencia y invariantes transaccionales.
3. Añadir flujo E2E de negocio y fixture reproducible de escala.
4. Completar seguridad/health/observabilidad y estados UI.
5. Ejecutar build, unit, integration, runtime, E2E y reauditoría; actualizar aquí cada hallazgo con evidencia.

## Regla de trazabilidad

Cada parche debe indicar: requisito/sección, archivo(s), comportamiento real, prueba que lo demuestra y clasificación `READY`, `PARTIAL`, `UNSUPPORTED` o `NOT PROVEN`. Este documento se mantiene junto con los cambios y se incluye en el push de revisión correspondiente.

## Ciclo 1 — Desarrollador Senior: billing idempotente

- Requisitos: secciones 20, 21, 22, 38 y 48.
- Cambios: `BillingEntry.IdempotencyKey`; índice único por cuenta/tipo/clave; transacción explícita en `AddLedger`; replay seguro cuando llega la misma clave; prueba unitaria de preservación de clave; migración `20260915133611_BillingEntryIdempotency`.
- Evidencia: build de los proyectos afectados PASS (0 errores); `OperationalDomainTests` 12/12 PASS.
- Clasificación: **PARTIAL**. El contrato de retry está implementado en API/persistencia, pero falta E2E concurrente contra MySQL y completar la política de pago validado antes de reconexión.

## Ciclo 2 — Desarrollador Senior: asignación segura de inventario

- Requisitos: secciones 17, 18, 24, 37 y 38.
- Cambio: `AssignAsset` ahora rechaza con `409 Conflict` la reasignación directa de un equipo que pertenece a otro servicio; exige recuperación antes del cambio de propietario lógico.
- Evidencia: pendiente de añadir prueba E2E de doble asignación y de validar el flujo `Recovered → PendingInspection → Tested → Stock → Assigned`.
- Clasificación: **PARTIAL**.

## Reauditoría de configuración y seguridad — evidencia

- `Polling`, `Discovery` y `Notifications` viven en configuración tipada; no se detectaron intervalos operativos dispersos en el mapa auditado.
- Hay rate limiting para API/setup, health live/ready con chequeo de DB, HSTS/HTTPS fuera de Testing y cabeceras CSP/nosniff/frame/referrer.
- La advertencia restante `CS8620` es de nulabilidad en un converter EF opcional; no rompe build, pero queda como deuda de calidad antes de `Definition of Done`.
- Worker separado en producción y embebido sólo bajo configuración de Testing/Development; no se encontró evidencia de health detallado de cada job individual.
- Clasificación: **PARTIAL** hasta probar health degradado, secretos/logs y cierre de setup con runtime.
