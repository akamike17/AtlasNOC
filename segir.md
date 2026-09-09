# ATLASNOC — CIERRE QUIRÚRGICO LOCAL PARA DEEPSEEK

## Contexto confirmado

- Repositorio: `akamike17/AtlasNOC`
- Rama objetivo: `deepseek-rebuild`
- Commit base verificado: `c458c47abb85aa288b114ad991ac2c33bcb56db9`
- Rama `main` va atrás en `fa31dfacabbc5496753524033c8025eb016ee84b`
- El trabajo se ejecutará **LOCAL**, en la PC del usuario, con DeepSeek/agente local, Visual Studio y PowerShell.
- NO crear otra solución.
- NO rehacer desde cero.
- NO copiar `main` encima.
- NO declarar terminado solo porque compile.

---

# 1. MODO DE TRABAJO OBLIGATORIO

Para cada cambio:

1. READ
2. TRACE
3. WRITE
4. BUILD
5. TEST
6. RUNTIME
7. E2E
8. COMMIT

Antes de crear algo, buscar si ya existe bajo otro nombre.

No duplicar:
- entidades;
- DTOs;
- servicios;
- repositorios;
- controllers;
- vistas;
- workers.

No dejar:
- TODO funcional;
- NotImplementedException;
- mocks en producción;
- datos inventados;
- endpoints falsos;
- vistas vacías;
- métricas simuladas fuera de LabMode.

---

# 2. ESTADO ACTUAL VERIFICADO

La rama `deepseek-rebuild` ya llegó a Fase 10.

Último commit:

`c458c47abb85aa288b114ad991ac2c33bcb56db9`

Incluye:
- 18 flujos E2E;
- UI de credenciales;
- fix de revocación de API key;
- fix de `ApiKeys/Index.cshtml`;
- AlertWorker 60 s → 15 s;
- liberación de puerto en fixture E2E.

Por lo tanto, **continuar sobre esta rama**.

---

# 3. OBJETIVO DEL PRODUCTO

AtlasNOC debe ser un NOC/WISP multi-vendor real:

Internet / Upstream  
→ Edge Router  
→ Core / Distribution  
→ Site / Tower / POP  
→ Switch  
→ Backhaul  
→ AP / Sector  
→ CPE  
→ Subscriber / Service

La topología debe basarse en evidencia.

Si no existe evidencia suficiente:

`Sin relación confirmada`

Nunca crear links por:
- proximidad visual;
- nombre parecido;
- posición del nodo;
- heurística no corroborada.

---

# 4. CAPACIDADES QUE DEBE CERRAR

## Inventario
- sitios;
- torres;
- POP;
- routers;
- switches;
- radios;
- AP;
- CPE;
- interfaces;
- IP;
- MAC;
- vendor;
- model;
- serial;
- firmware;
- uptime;
- status;
- LastSeen;
- LastPolled.

## Discovery
- CIDR;
- lista IP;
- seed device;
- site;
- ICMP;
- SNMP v2c/v3;
- LLDP;
- CDP;
- ARP auxiliar;
- fingerprint;
- interfaces;
- vecinos;
- upsert idempotente;
- correlación.

## Multi-vendor
Drivers mínimos:
- `GenericSnmpDriver`
- `MikroTikDriver`
- `UbiquitiDriver`
- `CiscoDriver`

No crear controller por fabricante.

## Topología
- Cytoscape.js;
- ELK;
- jerarquía;
- anti-overlap;
- grupos por site;
- filtros;
- búsqueda;
- estado;
- nodos sin relación;
- detalle nodo;
- detalle link;
- evidencia;
- dependencias;
- posiciones persistibles.

## Monitoreo
- ICMP;
- RTT;
- uptime;
- RX/TX;
- errors;
- discards;
- speed;
- utilization;
- métricas dispositivo;
- métricas interfaz;
- series temporales;
- Chart.js;
- rango temporal;
- stale data.

## Alertas
- reglas;
- severidad;
- evaluación;
- acknowledge;
- resolve;
- close;
- deduplicación;
- historial;
- notificación.

## Incidentes
- automático/manual;
- alertas asociadas;
- timeline;
- responsable;
- estado;
- resolución;
- impacto.

## WISP
- Subscriber;
- ServiceEndpoint;
- CPE;
- Site;
- AP/Sector;
- IP/MAC;
- señal cuando exista;
- relación con topología.

## Administración
- setup;
- Identity;
- login;
- roles;
- users;
- credentials cifradas;
- API keys;
- scopes;
- expiración;
- revocación;
- audit;
- system health.

---

# 5. ARQUITECTURA QUE NO SE TOCA

Producción:

1. `AtlasNOC.Domain`
2. `AtlasNOC.Application`
3. `AtlasNOC.Infrastructure`
4. `AtlasNOC.Worker`
5. `AtlasNOC.Web`

Tests:

6. `AtlasNOC.Tests.Unit`
7. `AtlasNOC.Tests.Integration`
8. `AtlasNOC.Tests.Runtime`
9. `AtlasNOC.Tests.E2E`

Reglas:
- Domain no depende de Infrastructure/EF/MVC/vendors.
- Application define casos de uso y contratos.
- Infrastructure implementa persistencia, probes, drivers, seguridad, workers.
- Web compone MVC/UI/API.
- Worker ejecuta background jobs.

---

# 6. SERVICIOS YA EXISTENTES

Actualmente ya están registrados:

## Repositorios
- IUnitOfWork
- IDeviceRepository
- ISiteRepository
- ILinkRepository
- IInterfaceRepository
- IMetricRepository
- IAlertRepository
- IIncidentRepository
- ICredentialRepository
- IApiKeyRepository
- IDiscoveryRunRepository
- INeighborObservationRepository
- IAuditRepository

## Drivers/probes
- IcmpProbe
- SnmpProbe
- SimulatedIcmpProbe
- SimulatedSnmpProbe
- SimulatedNetworkDriver
- MikroTikDriver
- UbiquitiDriver
- GenericSnmpDriver
- DeviceDriverRegistry

## Servicios
- SetupService
- UserAdministrationService
- SiteService
- DeviceService
- LinkService
- TopologyService
- DiscoveryService
- DiscoveryExecutor
- NetworkFingerprintService
- TopologyCorrelationEngine
- PollingService
- MetricWriter
- MetricQueryService
- AlertService
- IncidentService
- AlertEvaluationEngine
- IncidentCorrelationEngine
- AlertRuleService
- NotificationService
- ApiKeyService
- CredentialService
- AuditService
- SystemHealthService

## Workers
- PollingWorker
- DiscoveryWorker
- TopologyCorrelationWorker
- MetricRetentionWorker
- AlertEvaluationWorker
- NotificationWorker

---

# 7. HUECOS DE APPLICATION / INFRASTRUCTURE

Antes de crear, inspeccionar si existe equivalente.

Si no existe:

## InterfaceService
Debe:
- listar interfaces por device;
- detalle de interface;
- métricas;
- links asociados;
- estado admin/oper;
- LastSeen;
- LastPoll.

## SubscriberService
Debe:
- CRUD Subscriber;
- asociar CPE;
- asociar ServiceEndpoint;
- asociar Site;
- estado derivado;
- no inventar métricas.

## ServiceEndpointService
Debe manejar:
- subscriber;
- CPE/device;
- interface;
- IP;
- MAC;
- status;
- metadata.

## WirelessAssociationService
Si el dominio lo soporta:
- AP;
- CPE;
- RSSI;
- SNR;
- channel;
- frequency;
- LastSeen;
- source;
- driver.

## CiscoDriver
Debe usar:
- SNMP;
- CDP;
- LLDP;
- sysObjectID;
- interfaces;
- health;
- metrics.

DTOs neutrales.

---

# 8. CONTROLADORES MVC EXISTENTES

Existen:
- AccountController
- AlertRulesController
- AlertsController
- ApiKeysController
- AuditController
- CredentialsController
- DashboardController
- DevicesController
- DiscoveryController
- HomeController
- IncidentsController
- SetupController
- SitesController
- SystemController
- TopologyController
- UsersController

Faltan cerrar/agregar:

## InterfacesController
- `GET /interfaces/device/{deviceId}`
- `GET /interfaces/{id}`
- `GET /interfaces/{id}/metrics`
- `POST /interfaces/{id}/refresh`

## LinksController
- `GET /links`
- `GET /links/{id}`
- `POST /links/{id}/confirm`
- `POST /links/{id}/reject`
- `GET/POST /links/create-manual`
- `POST /links/{id}/edit-metadata`

## MetricsController
- `GET /metrics`
- `GET /metrics/device/{id}`
- `GET /metrics/interface/{id}`
- `GET /metrics/data`

## SubscribersController
- Index
- Create
- Edit
- Details
- AssociateCpe
- AssociateEndpoint

## IntegrationsController
Landing de:
- credentials;
- API keys;
- notification channels;
- email;
- webhook;
- drivers/config.

No duplicar lógica existente.

---

# 9. API CONTROLLERS

Actualmente aparecen claramente:
- `MetricsApiController`
- `TopologyApiController`

Completar:

- DevicesApiController
- SitesApiController
- TopologyApiController
- DiscoveryApiController
- MetricsApiController
- AlertsApiController
- IncidentsApiController
- SubscribersApiController
- IntegrationsApiController
- SystemApiController

Reglas:
- API key real;
- hash persistido;
- scopes;
- expiración;
- revocación;
- ProblemDetails;
- validación;
- rate limit;
- audit de writes;
- nunca devolver secrets;
- DTOs, nunca EF entities.

---

# 10. VISTAS QUE FALTAN

Actualmente existen carpetas para:
- Account
- AlertRules
- Alerts
- ApiKeys
- Audit
- Credentials
- Dashboard
- Devices
- Discovery
- Home
- Incidents
- Setup
- Shared
- Sites
- System
- Topology
- Users

Crear:

## Interfaces
- Index.cshtml
- Details.cshtml

## Links
- Index.cshtml
- Details.cshtml
- Create.cshtml

## Metrics
- Index.cshtml
- Device.cshtml
- Interface.cshtml

## Subscribers
- Index.cshtml
- Create.cshtml
- Edit.cshtml
- Details.cshtml

## Integrations
- Index.cshtml

---

# 11. DASHBOARD

Debe mostrar datos reales:

- Up;
- Down;
- Unknown;
- Stale;
- Critical alerts;
- Warning alerts;
- Open incidents;
- affected sites;
- last discovery;
- polling status;
- worker status;
- DB health;
- topology summary.

Quick actions:
- Discover network
- Add site
- Add credential
- Open topology
- Open alerts

Sin datos:
mostrar empty state útil.

---

# 12. TOPOLOGY — BLOQUE CRÍTICO

Trazar:

`TopologyController`
→ `ITopologyService`
→ repositories
→ DTOs
→ `TopologyApiController`
→ Cytoscape
→ ELK

Verificar:
- node IDs estables;
- edge IDs estables;
- links solo por evidencia;
- grupos Site;
- zero overlap deliberado;
- layout determinista;
- filtros;
- search;
- panel "Sin relación";
- node click;
- edge click;
- status;
- vendor;
- type;
- IP;
- uptime;
- última métrica;
- alerts;
- evidencia;
- dependencias.

No construir edges en frontend.

---

# 13. DISCOVERY

Pipeline:

1. validar CIDR/IP;
2. crear DiscoveryRun;
3. ICMP con concurrencia limitada;
4. fingerprint;
5. SNMP;
6. sysName;
7. sysObjectID;
8. interfaces;
9. LLDP/CDP;
10. seleccionar driver;
11. adquirir vendor data;
12. upsert Device;
13. upsert Interfaces;
14. guardar NeighborObservation;
15. correlacionar;
16. crear Link solo con evidencia;
17. guardar ambigüedad;
18. resumen final.

UI final:
- found;
- new;
- updated;
- confirmed links;
- ambiguous relationships;
- failures;
- duration.

---

# 14. POLLING

PollingWorker debe:
- aislar fallos por device;
- CancellationToken;
- concurrencia limitada;
- timeout;
- LastSeen;
- LastPolled;
- MetricSample;
- stale;
- error por device;
- continuar ciclo.

Ping OK no equivale a Healthy.

---

# 15. ALERTAS E INCIDENTES

Flujo:

MetricSample  
→ AlertEvaluationWorker  
→ AlertRule  
→ Alert  
→ IncidentCorrelationEngine  
→ Incident  
→ NotificationWorker

Verificar:
- dedupe;
- estados;
- acknowledge;
- resolved;
- closed;
- severity;
- audit;
- timestamps;
- no incidente duplicado en cada poll.

---

# 16. NOTIFICACIONES

Mínimos:
- Email
- Webhook

Cerrar:
- NotificationChannel;
- sender;
- dispatcher;
- retries limitados;
- persistence de fallo;
- test connection;
- UI.

No loggear secrets.

---

# 17. CREDENCIALES

Verificar:
- cifrado en reposo;
- secreto no visible tras guardar;
- edición reemplaza secreto solo si usuario lo cambia;
- tipos:
  - SNMP v2
  - SNMP v3
  - MikroTik
  - Ubiquiti
  - SSH opcional
- site/range si aplica;
- audit.

---

# 18. API KEYS

Verificar:
- creación;
- mostrar una sola vez;
- hash;
- scopes;
- expiration;
- revoke;
- revoked = rechazado;
- expired = rechazado;
- audit.

Nunca login humano con API key.

---

# 19. SITIOS / TORRES

NetworkSite debe soportar:
- Name;
- Code;
- SiteType;
- ParentSiteId;
- Latitude;
- Longitude;
- Address;
- IsActive;
- devices;
- links;
- alerts;
- dependencies.

Details:
- devices;
- health;
- active alerts;
- topology subset;
- upstream/downstream;
- last discovery.

---

# 20. DEVICE DETAILS

Debe tener tabs:

1. Overview
2. Interfaces
3. Metrics
4. Links
5. Neighbors
6. Alerts
7. Wireless
8. Audit

Overview:
- hostname;
- management IP;
- vendor;
- model;
- firmware;
- serial;
- site;
- type;
- driver;
- status;
- uptime;
- LastSeen;
- LastPolled.

---

# 21. INTERFACES

Index por dispositivo:

- IfIndex
- Name
- Description
- MAC
- IP
- Admin
- Oper
- Speed
- RX
- TX
- Errors
- Discards
- Link
- LastPoll

Details:
- metadata;
- counters;
- charts;
- linked device/interface;
- evidence;
- stale status.

---

# 22. LINKS

Index:
- endpoint A;
- endpoint B;
- LinkType;
- DiscoverySource;
- Confidence;
- OperStatus;
- Capacity;
- LastSeen;
- IsConfirmed.

Details:
- evidence;
- source observation;
- interfaces;
- traffic;
- errors;
- capacity;
- affected dependencies.

Manual links deben quedar marcados:

`DiscoverySource = Manual`

---

# 23. METRICS

Tipos mínimos:
- ICMP RTT
- uptime
- RX bps
- TX bps
- utilization
- errors
- discards
- packet loss si existe fuente real
- RSSI/SNR si driver lo aporta

Nunca convertir ausencia de datos en 0.

---

# 24. SUBSCRIBERS / CPE

Subscriber:
- identity;
- contact metadata;
- site;
- ServiceEndpoint;
- CPE;
- interface;
- IP/MAC;
- AP/Sector;
- status.

No convertir AtlasNOC en billing/CRM completo.

Solo inventario y operación de red.

---

# 25. LABMODE

LabMode puede:
- simular probes;
- simular devices;
- simular links;
- producir metrics marcadas como lab.

LabMode NO puede contaminar producción.

Debe existir indicador visible:

`LAB MODE`

---

# 26. WORKER HOST

Revisar `AtlasNOC.Worker/Program.cs`.

Los workers actualmente se registran desde `AddInfrastructure`.

Verificar:
- que realmente arranquen;
- que no se registren dos veces;
- que Web no los ejecute accidentalmente si no corresponde;
- DB correcta;
- config correcta;
- graceful shutdown.

---

# 27. SEGURIDAD

Revisar:
- Identity cookie;
- antiforgery en POST MVC;
- role authorization;
- API key scopes;
- secret masking;
- encrypted credentials;
- input validation;
- no raw SQL vulnerable;
- no command injection;
- no SSRF en drivers HTTP;
- CIDR discovery limitado;
- timeout;
- concurrency limits;
- audit.

---

# 28. CSS / UX

UI tipo NOC, no CRUD genérico.

Debe tener:
- dark-friendly layout;
- status chips;
- side navigation;
- breadcrumb;
- responsive tables;
- filtros;
- search;
- empty states;
- loading states;
- error states;
- badges Up/Down/Stale/Unknown;
- tooltips;
- sin nodos superpuestos.

---

# 29. PRUEBAS

## Unit
- correlation;
- evidence thresholds;
- status derivation;
- alert transitions;
- API key scopes;
- driver selection.

## Integration
- EF/MySQL;
- repositories;
- Identity;
- encryption;
- credentials;
- API keys;
- DiscoveryRun;
- NeighborObservation;
- links;
- metrics.

## Runtime
- servidor real;
- MySQL real;
- workers;
- LabMode;
- discovery;
- polling;
- topology;
- alerts;
- notifications.

## E2E
Conservar los 18 flujos actuales y agregar faltantes para:

1. Interfaces
2. Links
3. Metrics
4. Subscribers
5. Integrations
6. API scopes
7. stale metrics
8. ambiguous neighbor
9. manual link
10. Cisco simulated/fixture
11. notification test
12. topology node/edge detail

---

# 30. CRITERIO DE TERMINADO

NO terminar hasta cumplir:

- `dotnet restore` OK
- `dotnet build -c Release` → 0 errores
- warnings revisados
- unit tests OK
- integration tests OK
- runtime tests OK
- E2E OK
- MySQL migration OK
- aplicación abre
- login funciona
- dashboard muestra datos reales
- discovery funciona
- topology dibuja links reales
- no overlap grave
- metrics aparecen
- alerts cambian de estado
- incidents correlacionan
- credentials están cifradas
- revoked API key falla
- workers sobreviven fallo de un device
- LabMode no invade producción
- no TODO funcional
- no NotImplementedException
- no secrets en logs

---

# 31. ORDEN DE IMPLEMENTACIÓN

Trabajar en este orden:

## Fase 11
Auditoría estructural de huecos reales.

Salida:
`docs/F11_GAP_AUDIT.md`

## Fase 12
Interfaces + Links.

## Fase 13
Metrics UI/API.

## Fase 14
Subscribers + ServiceEndpoint.

## Fase 15
CiscoDriver + CDP/LLDP.

## Fase 16
Integrations + NotificationChannels.

## Fase 17
Topología final + ELK + evidence UI.

## Fase 18
Alert/Incident hardening.

## Fase 19
Security hardening.

## Fase 20
Runtime lab completo.

## Fase 21
E2E final.

## Fase 22
Release audit.

---

# 32. REGLAS PARA NO GASTAR TOKENS LOCALMENTE

DeepSeek debe:

- leer primero solo archivos relacionados;
- no volcar todo el repo al contexto;
- usar búsquedas por símbolo;
- modificar por fase;
- compilar después de cada bloque;
- no repetir explicación larga;
- guardar progreso en commits;
- producir un checkpoint MD al terminar cada fase;
- continuar desde el checkpoint si se reinicia el agente.

Formato checkpoint:

```md
# CHECKPOINT
Branch:
Commit:
Phase:
Completed:
Modified files:
Tests:
Build:
Remaining:
Next exact action:
```

---

# 33. PRIMERA ACCIÓN EXACTA DE DEEPSEEK

1. Confirmar rama:
   `git branch --show-current`

2. Confirmar commit:
   `git rev-parse HEAD`

3. Ejecutar:
   `git status`

4. NO modificar todavía.

5. Buscar:
   - InterfacesController
   - LinksController
   - MetricsController
   - SubscribersController
   - IntegrationsController
   - SubscriberService
   - ServiceEndpointService
   - CiscoDriver

6. Comparar con:
   - Domain entities
   - Application contracts
   - Infrastructure registrations
   - Web views
   - E2E tests

7. Crear:
   `docs/F11_GAP_AUDIT.md`

8. El GAP_AUDIT debe clasificar cada elemento como:
   - EXISTS_COMPLETE
   - EXISTS_PARTIAL
   - MISSING
   - DUPLICATE
   - BROKEN

9. Solo después empezar implementación.

---

# 34. COMANDO DE ARRANQUE LOCAL SUGERIDO

```powershell
git checkout deepseek-rebuild
git pull
git status
git rev-parse HEAD
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
```

Si el repo local tiene cambios sin commit:

**NO hacer reset, clean ni checkout destructivo.**

Primero:
- `git status`
- preservar cambios;
- informar;
- continuar solo cuando sea seguro.

---

# 35. RESULTADO ESPERADO

Al terminar, AtlasNOC debe abrir localmente y permitir:

1. login;
2. crear Site;
3. agregar credentials;
4. ejecutar discovery;
5. descubrir devices/interfaces;
6. correlacionar links;
7. ver topology;
8. abrir device;
9. abrir interface;
10. ver metrics;
11. generar alert;
12. abrir incident;
13. ver subscriber/CPE;
14. administrar API key;
15. revisar audit;
16. comprobar system health.

Ese flujo debe funcionar con datos de laboratorio y, cambiando configuración, con red real.

---

# 36. ÚLTIMA REGLA

No maquillar.

Si una función está incompleta, se termina.

Si está rota, se corrige.

Si ya funciona, se conserva.

Si falta evidencia, no se inventa.

Si un test falla, no se elimina para obtener verde.

Si una vista existe pero no sirve, se conecta al flujo real.

**Meta: AtlasNOC operativo local, no demo falsa.**
