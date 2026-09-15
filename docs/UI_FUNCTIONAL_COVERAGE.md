# Cobertura funcional de UI y browser

Esta tabla sólo enumera recorridos que existen en código de prueba. Las verificaciones E2E usan proceso `AtlasNOC.Web` real, Playwright headless, puerto loopback dinámico y una base MySQL LAB aislada. La instrumentación falla si encuentra `console.error`, `pageerror`, request fallida del host o respuestas 401/403/404 inesperadas.

| Módulo | Ruta | Acción probada | Persistencia | Auth | Browser/E2E | Resultado |
|---|---|---|---|---|---|---|
| Setup | `/setup` | Crear WISP/admin | Reload y login posterior | setup policy + antiforgery | `Full_lifecycle_flows_1_through_18` | PASS |
| Login | `/account/login` | Login y remember-me | Sesión cookie | Identity | `E2EFlowsTests` | PASS |
| Dashboard | `/` | Cargar métricas/resumen | Consulta MySQL | usuario autenticado | `All_primary_views_are_navigable_for_admin` | PASS |
| Devices | `/devices` | Navegar y consultar detalle | Inventario LAB | Administrator | `All_primary_views_are_navigable_for_admin` | PASS |
| Interfaces | `/interfaces` | Listar interfaces | EF query | Administrator | `All_primary_views_are_navigable_for_admin` | PASS |
| Links | `/links` | Crear link manual | EF insert y reload | Administrator | `All_primary_views_are_navigable_for_admin` | PASS |
| Discovery | `/discovery` | Abrir/start LAB | DiscoveryRun | Administrator | `Full_lifecycle_flows_1_through_18` | PASS |
| Topology | `/topology` | Render, filtro, refresh, detalle | Graph API; no links inventados | usuario autenticado | `Topology_six_isolated_devices_render_six_nodes_and_zero_edges` + lifecycle | PASS |
| Metrics | `/metrics` | Consultar gráficas | MetricSamples | usuario autenticado | `All_primary_views_are_navigable_for_admin` | PASS |
| Alerts | `/alerts` | Provocar alerta y reconocer | Alert state | Administrator | lifecycle flow | PASS |
| AlertRules | `/alert-rules` | Crear regla | AlertRule row | Administrator | `All_primary_views_are_navigable_for_admin` | PASS |
| Incidents | `/incidents` | Ver/resolver incidente | Incident + evidence | Administrator | lifecycle flow | PASS |
| Sites | `/sites` | Crear sitio | NetworkSite row | Administrator | `All_primary_views_are_navigable_for_admin` | PASS |
| Zones | `/operations` + API | Alta/listado de 4 zonas | NetworkZones | Administrator | `Operational_closure_smoke_uses_real_api_and_mysql` | PASS |
| Coverage | `/operations` + API | Registrar/evaluar cobertura | CoverageCheck | Administrator | operational smoke | PASS |
| Customers | `/operations` + API | Crear cliente | Customer + BillingAccount | Administrator | operational smoke | PASS |
| Services | `/operations` + API | Crear/activar servicio | CustomerService + zone | Administrator | operational smoke | PASS |
| Billing | `/operations` + API | Charge/replay/list, dos cargos simultáneos con la misma clave | Ledger idempotente y único | Administrator | operational smoke + `Billing_concurrent_same_key_creates_one_entry_and_one_replay` | PASS |
| Payments | `/operations` + API | Payment/promise/default | Receipt/promise/ledger | Administrator | operational smoke | PASS |
| Subscribers | `/subscribers` | Crear y borrar subscriber | EF row | Administrator | `All_primary_views_are_navigable_for_admin` | PASS |
| Support | `/operations` + API | Ticket/interacción/visita | Support records | Administrator | operational smoke | PASS |
| Inventory | `/operations` + API | Asset/assign/recover/inspect | Asset lifecycle + RowVersion | Administrator | smoke + Integration concurrency | PASS |
| Integrations | `/integrations` | Cargar configuración | EF query | Administrator | `All_primary_views_are_navigable_for_admin` | PASS |
| Credentials | `/credentials` | Cargar/validar protección | Data Protection fields | Administrator | `RepositoryIntegrationTests` | PASS |
| ApiKeys | `/api-keys` | Crear/revocar/usar scope | hash/revoke/expiry | Administrator | `ApiKeyAuthenticationTests` | PASS |
| System | `/system` | Health/status | health queries | Administrator | `All_primary_views_are_navigable_for_admin` | PASS |
| Users | `/users` | Crear usuario/rol | Identity rows | Administrator | `All_primary_views_are_navigable_for_admin` | PASS |
| Audit | `/audit` | Consultar actividad | Audit rows | Administrator | `All_primary_views_are_navigable_for_admin` | PASS |

## Topology contract

`wwwroot/js/topology.js` acepta las propiedades camelCase y PascalCase entregadas por JSON, filtra edges cuyos extremos no existen, muestra nodos aislados, actualiza conteo/unlinked y genera enlace de detalle sólo con el ID recibido. Bootstrap, Chart.js y Cytoscape se sirven desde `wwwroot/lib`; no hay CDN requerido. La prueba browser específica valida seis nodos y cero edges.

## Limitación real

La cobertura funcional demuestra la aplicación y el simulador LAB. No demuestra que un modelo físico concreto acepte una acción de red; esa capacidad requiere equipo, firmware, credencial y verificación posterior externa.
