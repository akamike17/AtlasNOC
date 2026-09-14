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

## Matriz requisito por requisito post-05670d4

| Requisito del cierre | Estado | Evidencia exacta | Prueba / límite |
|---|---|---|---|
| Credencial scoped por dispositivo | IMPLEMENTADO | `NetworkActionService.ResolveCredentialAsync`; `DeviceCredential.ScopeTo` | `NetworkDriverTests` y `CredentialScopeTests` |
| PATCH MikroTik sin propiedades colaterales | IMPLEMENTADO | `MikroTikControlDriver.ExecuteAsync` | `NetworkDriverTests`; físico NO PROBADO |
| Preview/riesgo/confirmación | PARCIAL | `NetworkActionPreview`; `OperationsApiController.PreviewAction` | Preview existe; ejecución Medium/High aún no enlaza token verificable |
| Impacto multihop/downstream | PARCIAL | `NetworkImpactService.PreviewAsync` | Tests de impacto; ramas sin evidencia y servicios ligados requieren ampliar cobertura |
| Diagnóstico por camino | PARCIAL | `NetworkDiagnosticsService.DiagnoseCustomerAsync` | Aísla alertas ajenas; no modela aún toda la cadena CPE→core |
| Ledger due date/gracia | IMPLEMENTADO | `BillingEntry.IsOverdue`; `SuspendIfOverdue` | Tests de billing; gracia fija actualmente en 3 días |
| Promesas y pagos parciales | IMPLEMENTADO | `OperationsApiController.AddLedger` | Asignación por remanente; falta ledger de asignación individual |
| Estados de pago manual | NO IMPLEMENTADO | `ManualPaymentProvider.CaptureAsync` | Aún devuelve `PaymentResult` booleano sin Pending/Confirmed/Rejected |
| Prepago con vigencia/bonus real | PARCIAL | `PrepaymentPolicy`; `RegisterPrepayment` | Bonus queda en descripción/ledger; no existe periodo de cobertura persistido |
| PayAndReconnect y fee | PARCIAL | `OperationsApiController.PayAndReconnect` | Reconecta por saldo; falta proveedor confirmado, fee configurable y provisioning |
| Change-plan capacidad/prorrateo | PARCIAL | `OperationsApiController.ChangePlan` | Confirmación y diferencia mensual; no hay capacidad de ruta ni prorrateo |
| Business state separado de provisioning | IMPLEMENTADO | `CustomerService.Provisioning`; `CpeAuthorizationCase.Provisioning` | `ProvisioningState` migration; endpoints reportan `Unsupported` honestamente |
| CPE decision vs enforcement | IMPLEMENTADO | `DecideCpeCase` | Test/API confirma `Unsupported`; no se afirma bloqueo físico |
| Crédito derivado de evidencia | NO IMPLEMENTADO | `CreateCredit` acepta `SuggestedAmount` libre | No existe `OutageCreditPreview` |
| Coverage baseline con evidencia | PARCIAL | `EvaluateCoverage` | Persistencia de estado/capacidad; faltan coordenadas, zona e infraestructura normalizada |
| Route planner geográfico | PARCIAL | `TechnicianRoutePlanner.PlanAsync` | Orden/ETA/buffer; sin lat/lon ni recálculo por retraso |
| WISP correlación cliente→AP→sector | NO IMPLEMENTADO | `IWispOperationsService.ListOperationalClientsAsync` | Devuelve snapshots de dispositivos, no DTO correlacionado completo |
| Fraud evidence engine | NO IMPLEMENTADO | `CpeAuthorizationStatus.FraudReview` | Estado existe; no hay reglas ni evidencia de activación |
| Backup/compare/restore | PARCIAL | `ConfigurationRevision` y endpoints de revisiones | Hash/known-good; no existe restore operativo |
| Simulator failure/timeout/reboot/description | SIMULADO | `SimulatedDeviceControlDriver` | `NetworkDriverTests`; sólo simula, no prueba hardware |
| Auditoría completa de control | PARCIAL | `NetworkActionService` y `AuditEvent` | Hay acción/actor/device; faltan pre/post-state, correlation y credential reference completos |
| UI grafo→preview→confirmación | NO IMPLEMENTADO | `Views/Operations/Index.cshtml` | Cabina existente, pero no demuestra flujo completo sin cliente API |
| Smoke cierre 30 pasos API+MySQL | NO PROBADO | `OperationalClosureSmokeTests` | Test añadido; actualmente falla en POST `/api/operations/customers` con HTTP 405 |
| MySQL migration clean/upgrade | IMPLEMENTADO | `20260914173000_BillingDueDates`, `20260914180000_ProvisioningState` | `database update`: up to date; restore/rollback no probado |
| Lab físico opt-in | NOT PROBADO | `LabControlApiController` y variables `ATLAS_REAL_CONTROL_*` | No ejecutado por instrucción |
