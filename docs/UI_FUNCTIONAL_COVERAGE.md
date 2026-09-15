# Cobertura funcional de UI y browser

Esta matriz enumera únicamente recorridos existentes en código de prueba. Las pruebas E2E arrancan el proceso real `AtlasNOC.Web`, usan Playwright headless, puerto loopback dinámico y una base MySQL LAB derivada con sufijo `_e2e`; al terminar destruyen sólo esa base. Cada PASS tiene un método reproducible en la columna `Evidencia`.

| Módulo | Ruta | Acción probada | Persistencia | Auth | Browser/E2E | Resultado | Evidencia |
|---|---|---|---|---|---|---|---|
| Setup | `/setup` | Crear WISP y administrador | Reload y login posterior | Política setup + antiforgery | Sí | PASS | `E2EFlowsTests.Full_lifecycle_flows_1_through_18` |
| Login | `/account/login` | Login y remember-me | Cookie de sesión | Identity | Sí | PASS | `E2EFlowsTests.Full_lifecycle_flows_1_through_18` |
| Dashboard | `/` | Cargar resumen, health y navegación | Consulta MySQL | Usuario autenticado | Sí | PASS | `E2EFlowsTests.All_primary_views_are_navigable_for_admin` |
| Devices | `/devices` | Navegar y consultar detalle | Inventario LAB | Administrator | Sí | PASS | `E2EFlowsTests.All_primary_views_are_navigable_for_admin` |
| Interfaces | `/interfaces` | Listar interfaces | Consulta EF | Administrator | Sí | PASS | `E2EFlowsTests.All_primary_views_are_navigable_for_admin` |
| Links | `/links` | Crear/consultar link manual | Insert EF y reload | Administrator | Sí | PASS | `E2EFlowsTests.All_primary_views_are_navigable_for_admin` |
| Discovery | `/discovery` | Abrir e iniciar discovery LAB | `DiscoveryRun` | Administrator | Sí | PASS | `E2EFlowsTests.Full_lifecycle_flows_1_through_18` |
| Topology | `/topology` | Render, filtro, refresh, búsqueda y detalle | Graph API; no links inventados | Usuario autenticado | Sí | PASS | `E2EFlowsTests.Topology_six_isolated_devices_render_six_nodes_and_zero_edges` |
| Metrics | `/metrics` | Consultar gráficas | `MetricSamples` | Usuario autenticado | Sí | PASS | `E2EFlowsTests.All_primary_views_are_navigable_for_admin` |
| Alerts | `/alerts` | Provocar, consultar y reconocer alerta | Estado de alerta | Administrator | Sí | PASS | `E2EFlowsTests.Full_lifecycle_flows_1_through_18` |
| AlertRules | `/alert-rules` | Crear regla | Fila `AlertRule` y reload | Administrator | Sí | PASS | `E2EFlowsTests.All_primary_views_are_navigable_for_admin` |
| Incidents | `/incidents` | Consultar y resolver incidente | Incidente y evidencia | Administrator | Sí | PASS | `E2EFlowsTests.Full_lifecycle_flows_1_through_18` |
| Sites | `/sites` | Crear/consultar sitio | Fila `NetworkSite` | Administrator | Sí | PASS | `E2EFlowsTests.All_primary_views_are_navigable_for_admin` |
| Zones | `/operations` + API | Alta y listado de cuatro zonas | `NetworkZones` | Administrator | Sí | PASS | `OperationalClosureSmokeTests.Operational_closure_smoke_uses_real_api_and_mysql` |
| Coverage | `/operations` + API | Registrar y evaluar cobertura | `CoverageCheck` | Administrator | Sí | PASS | `OperationalClosureSmokeTests.Operational_closure_smoke_uses_real_api_and_mysql` |
| Customers | `/operations` + API | Crear cliente | Customer + BillingAccount | Administrator | Sí | PASS | `OperationalClosureSmokeTests.Operational_closure_smoke_uses_real_api_and_mysql` |
| Services | `/operations` + API | Crear y activar servicio | `CustomerService` + zona | Administrator | Sí | PASS | `OperationalClosureSmokeTests.Operational_closure_smoke_uses_real_api_and_mysql` |
| Billing | `/operations` + API | Charge/replay y dos cargos simultáneos con misma clave | Ledger único/idempotente | Administrator | Sí | PASS | `OperationalClosureSmokeTests.Billing_concurrent_same_key_creates_one_entry_and_one_replay` |
| Payments | `/operations` + API | Payment, promise y default | Receipt/promise/ledger | Administrator | Sí | PASS | `OperationalClosureSmokeTests.Operational_closure_smoke_uses_real_api_and_mysql` |
| Subscribers | `/subscribers` | Crear y borrar subscriber | Fila EF | Administrator | Sí | PASS | `E2EFlowsTests.All_primary_views_are_navigable_for_admin` |
| Support | `/operations` + API | Ticket ligado a `CustomerService`, interacción y visita | Ticket conserva `CustomerServiceId`; interacción conserva `TicketId` | Administrator | Sí + MySQL | PASS | `OperationalClosureSmokeTests.Operational_closure_smoke_uses_real_api_and_mysql` (assert de `customerServiceId` y persistencia del smoke) |
| Inventory | `/operations` + API | Asset, assign, recover e inspect | Lifecycle + `RowVersion` | Administrator | Sí + MySQL | PASS | `RepositoryIntegrationTests.Concurrent_asset_assignment_has_one_winner` |
| Integrations | `/integrations` | Cargar integración y estado | Consulta EF | Administrator | Sí | PASS | `E2EFlowsTests.All_primary_views_are_navigable_for_admin` |
| Credentials | `/credentials` | Crear/consultar credencial protegida | Secretos no plaintext | Administrator | Sí + MySQL | PASS | `RepositoryIntegrationTests.Credential_stores_protected_secrets_not_plaintext` |
| ApiKeys | `/api-keys` + API | Crear, usar, revocar y expirar scope | Hash, scope y revoke | Administrator/API key | Sí | PASS | `ApiKeyAuthenticationTests.Valid_key_with_correct_scope_returns_200` + `Revoked_key_returns_401` |
| Operations | `/operations` + API | Snapshot, preview y acciones | Operaciones auditables | Administrator | Sí + REST | PASS | `OperationalClosureSmokeTests.Operational_closure_smoke_uses_real_api_and_mysql` |
| System | `/system` | Health/status y contadores | Consultas DB | Administrator | Sí | PASS | `E2EFlowsTests.All_primary_views_are_navigable_for_admin` |
| Users | `/users` | Crear usuario y cambiar rol | Identity rows | Administrator | Sí | PASS | `E2EFlowsTests.All_primary_views_are_navigable_for_admin` |
| Audit | `/audit` | Consultar actividad | `AuditEvents` | Administrator | Sí | PASS | `E2EFlowsTests.Full_lifecycle_flows_1_through_18` |

## Instrumentación de la prueba

`All_primary_views_are_navigable_for_admin` registra `pageerror`, `console.error`, `requestfailed` y respuestas host 401/403/404/5xx; cualquier registro falla la prueba. Los recorridos mutables comprueban la respuesta JSON, IDs, estados y recarga cuando el flujo lo requiere. El smoke REST conserva el endpoint original `POST /api/operations/zones`, valida 39 respuestas y no usa una ruta espejo para ocultar errores.

## Topology contract

`wwwroot/js/topology.js` acepta las propiedades camelCase y PascalCase serializadas por ASP.NET, filtra edges cuyos extremos no existen, muestra nodos aislados, actualiza conteo/unlinked y crea el enlace de detalle sólo con el ID recibido. El caso obligatorio es `6 devices + 0 links = 6 nodes + 0 edges`; `Topology_six_isolated_devices_render_six_nodes_and_zero_edges` lo valida en Cytoscape sin fabricar relaciones por hostname, IP o proximidad.

Bootstrap, Chart.js, el adaptador de fechas y Cytoscape se sirven desde `wwwroot/lib`; no hay CDN obligatorio. Las pruebas demuestran software y simulador LAB, no que un modelo físico concreto acepte una acción de red; eso requiere equipo, firmware, credencial y verificación posterior autorizada.
