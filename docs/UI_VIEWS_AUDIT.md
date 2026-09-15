# Auditoría funcional de vistas AtlasNOC

Fecha de ejecución: 2026-09-15. `READY` requiere evidencia de código y prueba
navegable; `PARTIAL` indica que existe implementación, pero falta prueba de
flujo o cobertura completa.

| Vista | Controller/action | Servicio/fuente real | Acciones y auth | Empty/error | Test navegador | Resultado |
|---|---|---|---|---|---|---|
| Dashboard | Dashboard/Index | `ISystemHealthService`, `ITopologyService`, DB | GET; `[Authorize]` | Topología empty/error | E2E login/dashboard indirecto | PARTIAL |
| Devices | Devices/Index, Detail, Create | `IDeviceService`, `Devices` | CRUD; `[Authorize]` | Lista vacía/model not found | E2E crea/consulta tras discovery | READY |
| Discovery | Discovery/Index, Start, Detail | `IDiscoveryService`, `DiscoveryRuns` | POST discovery; `[Authorize]` | Estados de ejecución | E2E discovery LAB | READY |
| Topology | Topology/Index | `ITopologyService`, API `/api/topology/graph` | filtro/recarga/selección; `[Authorize]` | empty/error visibles | E2E API→Cytoscape→selección | READY |
| Interfaces | Interfaces/Index, Detail | `IInterfaceService`, `DeviceInterfaces` | GET; `[Authorize]` | Lista vacía/not found | No prueba dedicada | PARTIAL |
| Links | Links/Index, Detail, CreateManual | `ILinkService`, `IInterfaceService`, `NetworkLinks` | confirmar/rechazar/crear; `[Authorize]` | Lista/not found | E2E topología indirecta | PARTIAL |
| Metrics | Metrics/Index | `IMetricQueryService`, `MetricSamples` | GET; `[Authorize]` | Sin muestras | E2E consulta métricas | READY |
| Alerts | Alerts/Index, Detail | `IAlertService`, `Alerts` | reconocer/resolver; `[Authorize]` | Sin alertas/not found | E2E outage/alert | READY |
| AlertRules | AlertRules/Index, Create | `IAlertRuleService`, `AlertRules` | CRUD; `[Authorize]` | Lista/validación | E2E crea regla indirectamente | PARTIAL |
| Incidents | Incidents/Index, Detail | `IIncidentService`, `Incidents` | resolver; `[Authorize]` | Sin incidentes/not found | E2E incidente | READY |
| Sites | Sites/Index, Create | `ISiteService`, `Sites` | crear; `[Authorize]` | Lista/validación | E2E crea sitio | READY |
| Integrations | Integrations/Index | registry WISP, `Integrations` | GET; `[Authorize]` | Sin integraciones | E2E navegación indirecta | PARTIAL |
| Credentials | Credentials/Index, Create, Edit | `ICredentialService`, `Credentials` | CRUD; `[Authorize]` | Lista/validación | E2E crea credencial | READY |
| ApiKeys | ApiKeys/Index, Create | `IApiKeyService`, `ApiKeys` | crear/revocar; `[Authorize]` | Lista/one-time secret | E2E auth key y revocación | READY |
| Subscribers | Subscribers/Index, Detail, Create | `ISubscriberService`, endpoints | CRUD; `[Authorize]` | Lista/not found | No prueba dedicada | PARTIAL |
| Operations | Operations/* | `IOperationsSnapshotService`, operaciones | GET/POST; `[Authorize]`; preview de acciones y crédito | Estados vacíos/error visibles | E2E snapshot, preview de acción y smoke operativo MySQL | READY |
| System | System/Index | `ISystemHealthService`, DB | GET; `[Authorize]` | Health DB | No prueba dedicada | PARTIAL |
| Users | Users/Index, Create, Edit, roles/password | `IUserAdministrationService`, Identity | administración; `[Authorize]` | Lista/validación | E2E login indirecto | PARTIAL |
| Audit | Audit/Index | `AtlasNOCDbContext.AuditEvents` | GET; `[Authorize]` | Sin eventos | E2E comprueba Auth | READY |
| Setup | Setup/Index | `ISetupService`, Identity/DB | setup inicial; ruta pública controlada | Setup completo/errores | E2E setup/login | READY |

## Límites

- Las filas `READY` tienen cobertura de flujo en la suite E2E sólo cuando se
  indica explícitamente; el resto se basa en prueba de código más cobertura
  indirecta.
- No se afirma soporte físico, restore operativo, Ubiquiti genérico, GPON/DSL
  ni correlación WISP completa sin evidencia adicional.
- La matriz no convierte funciones no probadas en capacidades soportadas.
