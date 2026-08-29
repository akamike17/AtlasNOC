# AtlasNOC Rebuild — Especificación maestra v0.1

> Estado: propuesta para revisión antes de escribir código.
> Regla principal: este proyecto se construye desde cero en una carpeta nueva. El código actual sirve únicamente como referencia de requisitos y de errores a no repetir. No se copia arquitectura rota ni se maquilla la UI existente.

## 1. Objetivo del producto

AtlasNOC será un NOC/WISP multi-vendor para descubrir, inventariar, relacionar, visualizar y monitorear redes reales.

Debe permitir que un operador vea una red como red y no como una lista de equipos:

Internet / upstream
→ routers de borde
→ core/distribución
→ torres/sitios
→ switches
→ enlaces/backhauls
→ AP/sectores
→ CPE/clientes

Cada nodo y enlace debe tener estado, procedencia, métricas y dependencias. Si el sistema no puede demostrar una relación entre dos equipos, NO debe inventarla: el dispositivo se mostrará como “sin relación confirmada”.

## 2. Principios no negociables

1. Login humano con usuario y contraseña. Las API keys NO son login de operadores.
2. API keys únicamente para integraciones externas y automatización.
3. Sitios/torres son entidades de primer nivel.
4. Dispositivos, interfaces y enlaces son entidades separadas.
5. La topología se construye con relaciones persistidas, no con proximidad visual.
6. Cada enlace debe guardar su evidencia de descubrimiento.
7. Ningún nodo puede dibujarse deliberadamente encima de otro.
8. Métricas deben provenir de probes reales o de un simulador explícitamente marcado como laboratorio.
9. Un equipo “Up” sin métrica reciente no equivale a “saludable”.
10. Toda acción administrativa queda auditada.
11. Credenciales de equipos se guardan cifradas.
12. Descubrimiento y polling son trabajos en segundo plano; la UI no se bloquea esperando la red.
13. Las fallas de un dispositivo no deben detener el ciclo completo de polling.
14. Los adaptadores por fabricante deben estar detrás de interfaces; la lógica de negocio no conoce comandos MikroTik/Ubiquiti.
15. No se declara “producción” por compilar o por pasar unit tests: debe superar laboratorio runtime y E2E.

---

# 3. Stack técnico

## Backend
- ASP.NET Core MVC / .NET 8.
- C#.
- Entity Framework Core 8.
- MySQL 8 para configuración, inventario, relaciones, usuarios, eventos y métricas v1.
- ASP.NET Core Identity para usuarios, contraseñas, cookies y roles.
- HostedServices para trabajos locales en segundo plano.

## Frontend
- Razor MVC.
- Bootstrap 5 solamente para estructura/formularios.
- CSS propio para diseño NOC.
- Cytoscape.js para grafo interactivo.
- cytoscape-elk / ELK para layout jerárquico y anti-solapamiento.
- Chart.js para gráficas temporales.

## Protocolos / adquisición
- ICMP: disponibilidad y RTT.
- SNMP v2c/v3: sysName, sysObjectID, uptime, interfaces, counters, errores, descartes, LLDP y tablas estándar.
- LLDP/CDP cuando el equipo los exponga.
- MikroTik: RouterOS API/REST donde esté disponible; SNMP como fallback.
- Ubiquiti: controlador/API compatible cuando exista; SNMP como fallback.
- SSH: únicamente adaptadores read-only explícitos y opt-in; nunca como requisito general.

---

# 4. Solución nueva

Nombre de carpeta sugerido:

`C:\AtlasNOC-Rebuild`

Solución:

`AtlasNOC.sln`

## Proyectos de producción — 5

### 4.1 AtlasNOC.Domain
No depende de infraestructura, EF, MVC ni fabricantes.

Contiene:
- entidades;
- enums;
- value objects;
- reglas puras;
- eventos de dominio;
- contratos mínimos del dominio cuando sean estrictamente necesarios.

### 4.2 AtlasNOC.Application
Casos de uso.

Contiene:
- DTOs;
- comandos/queries;
- servicios de aplicación;
- validaciones;
- interfaces de repositorios;
- interfaces de probes/adaptadores;
- políticas de descubrimiento y correlación.

### 4.3 AtlasNOC.Infrastructure
Implementaciones externas.

Contiene:
- EF Core;
- MySQL;
- repositorios;
- Identity;
- cifrado de credenciales;
- ICMP;
- SNMP;
- MikroTik;
- Ubiquiti;
- notificaciones;
- persistencia de métricas.

### 4.4 AtlasNOC.Worker
Procesamiento en segundo plano.

Workers:
- DiscoveryWorker;
- PollingWorker;
- TopologyCorrelationWorker;
- AlertEvaluationWorker;
- NotificationWorker;
- MetricRetentionWorker.

### 4.5 AtlasNOC.Web
MVC/Razor.

Contiene:
- controllers MVC;
- API controllers;
- views;
- autorización;
- composición de UI;
- endpoints SignalR opcionales para actualización live.

## Proyectos de prueba — 4

### 4.6 AtlasNOC.Tests.Unit
Dominio y aplicación.

### 4.7 AtlasNOC.Tests.Integration
MySQL, EF, repositorios, Identity y adaptadores simulados.

### 4.8 AtlasNOC.Tests.Runtime
Servidor real + MySQL + workers + red simulada.

### 4.9 AtlasNOC.Tests.E2E
Playwright: navegador real, navegación, login, formularios, topología y métricas.

Total: 9 proyectos.

---

# 5. Modelo de datos inicial

## Seguridad / administración
1. ApplicationUser
2. ApplicationRole
3. ApiKey
4. AuditEvent
5. NotificationChannel

## Organización WISP
6. WispOrganization
7. NetworkSite
8. Subscriber
9. ServiceEndpoint

## Red
10. Device
11. DeviceInterface
12. NetworkLink
13. RadioSector
14. WirelessAssociation
15. DeviceCredential
16. DeviceCapability
17. NeighborObservation
18. DiscoveryRun

## Monitoreo
19. PollingProfile
20. MetricSample
21. DeviceStateEvent
22. AlertRule
23. Alert
24. Incident

Total inicial: 24 entidades persistentes.

### NetworkSite
Representa:
- POP;
- torre;
- datacenter;
- gabinete;
- nodo remoto;
- oficina.

Campos esenciales:
- Id
- Name
- Code
- SiteType
- ParentSiteId opcional
- Latitude/Longitude opcional
- Address
- IsActive

### Device
Campos esenciales:
- Id
- SiteId
- Hostname
- ManagementIp
- DeviceType
- Vendor
- Model
- SerialNumber
- FirmwareVersion
- Status
- LastSeenAt
- LastPolledAt
- DriverKey
- IsManaged

### DeviceInterface
Campos:
- DeviceId
- IfIndex
- Name
- Description
- MacAddress
- IpAddress
- AdminStatus
- OperStatus
- SpeedBps
- InterfaceType
- LastSeenAt

### NetworkLink
La relación real entre dos extremos.

Campos:
- AInterfaceId
- BInterfaceId
- LinkType
- DiscoverySource
- Confidence
- AdminStatus
- OperStatus
- CapacityBps
- LastSeenAt
- IsConfirmed

DiscoverySource:
- Manual
- LLDP
- CDP
- MikroTikNeighbor
- Ubiquiti
- WirelessAssociation
- Imported

### NeighborObservation
Evidencia cruda antes de convertir una observación en enlace.

Campos:
- LocalDeviceId
- LocalInterfaceId
- RemoteIdentity
- RemotePortIdentity
- Protocol
- ObservedAt
- RawEvidenceHash

Una observación no se convierte automáticamente en enlace si la correlación es ambigua.

---

# 6. Usuarios y seguridad

## Primer arranque

Ruta:
`/setup`

Solo existe mientras no haya usuarios administradores.

Solicita:
- nombre del WISP;
- usuario administrador;
- nombre;
- contraseña;
- confirmación.

Después se deshabilita.

## Login normal

Ruta:
`/account/login`

Campos:
- usuario;
- contraseña;
- recordar sesión.

Nada de API key para humanos.

## Roles

### Administrator
Control total.

### NocOperator
Opera red, alertas, incidentes y descubrimiento, sin gestionar seguridad crítica.

### ReadOnly
Consulta.

## API keys

Se administran después de iniciar sesión:

`Administración → Integraciones → API keys`

Al crear una key:
- se muestra UNA sola vez;
- se almacena hash;
- tiene owner, descripción, scopes, expiración y estado;
- se puede revocar.

---

# 7. Vistas — 20 pantallas principales

## Acceso
1. Setup inicial
2. Login

## Operación
3. Dashboard
4. Topología
5. Sitios/Torres
6. Detalle de sitio
7. Dispositivos
8. Detalle de dispositivo
9. Interfaces y puertos
10. Enlaces
11. Descubrimiento
12. Métricas
13. Alertas
14. Detalle de alerta
15. Incidentes
16. Detalle de incidente
17. Suscriptores/CPE

## Administración
18. Usuarios y roles
19. Credenciales e integraciones
20. Auditoría y salud del sistema

Además habrá parciales/modales para:
- alta/edición de dispositivo;
- alta de sitio;
- credencial;
- API key;
- regla de alerta;
- confirmar enlace;
- resolver incidente;
- selector de rango temporal.

---

# 8. Controllers MVC — 16

1. SetupController
2. AccountController
3. DashboardController
4. TopologyController
5. SitesController
6. DevicesController
7. InterfacesController
8. LinksController
9. DiscoveryController
10. MetricsController
11. AlertsController
12. IncidentsController
13. SubscribersController
14. UsersController
15. IntegrationsController
16. SystemController

No contienen lógica de negocio; solo:
request → validación → Application service → ViewModel/response.

---

# 9. API Controllers — 10

1. ApiDevicesController
2. ApiSitesController
3. ApiTopologyController
4. ApiDiscoveryController
5. ApiMetricsController
6. ApiAlertsController
7. ApiIncidentsController
8. ApiSubscribersController
9. ApiIntegrationsController
10. ApiSystemController

La API usa API key/scopes o autenticación apropiada. La UI humana usa cookie Identity.

Total controllers: 26.

---

# 10. Servicios de aplicación — 18

1. SetupService
2. UserAdministrationService
3. SiteService
4. DeviceService
5. InterfaceService
6. LinkService
7. TopologyService
8. DiscoveryService
9. PollingService
10. MetricQueryService
11. AlertRuleService
12. AlertService
13. IncidentService
14. SubscriberService
15. CredentialService
16. ApiKeyService
17. AuditService
18. SystemHealthService

---

# 11. Servicios/adaptadores de infraestructura — 15

1. EfUnitOfWork
2. CredentialProtector
3. IcmpProbe
4. GenericSnmpAdapter
5. LldpSnmpReader
6. CdpSnmpReader
7. MikroTikAdapter
8. UbiquitiAdapter
9. DeviceDriverRegistry
10. NetworkFingerprintService
11. TopologyCorrelationEngine
12. MetricWriter
13. NotificationDispatcher
14. EmailNotificationSender
15. WebhookNotificationSender

---

# 12. Workers — 6

1. DiscoveryWorker
2. PollingWorker
3. TopologyCorrelationWorker
4. AlertEvaluationWorker
5. NotificationWorker
6. MetricRetentionWorker

Servicios + adaptadores + workers explícitos: 39 componentes de servicio.

---

# 13. Device Drivers

Contrato central:

`IDeviceDriver`

Responsabilidades:
- CanHandle(fingerprint)
- GetIdentityAsync
- GetInterfacesAsync
- GetNeighborsAsync
- GetHealthAsync
- GetMetricsAsync
- GetWirelessAssociationsAsync cuando aplique

Drivers iniciales:

1. GenericSnmpDriver
2. MikroTikDriver
3. UbiquitiDriver

No se crea un controller por fabricante.

El driver devuelve DTOs neutrales. El dominio nunca recibe objetos RouterOS/UniFi/SNMP específicos.

---

# 14. Flujo de trabajo del producto

## Flujo A — Primer arranque

1. Arranca aplicación.
2. Ejecuta migraciones controladas.
3. Comprueba si existe administrador.
4. Si no existe: `/setup`.
5. Crea WISP + admin.
6. Redirige a login.
7. Login con usuario/contraseña.
8. Dashboard vacío con asistente “Agregar sitio / Descubrir red”.

## Flujo B — Preparar WISP

1. Crear sitios/torres.
2. Crear perfiles de credenciales:
   - SNMP v2;
   - SNMP v3;
   - MikroTik;
   - Ubiquiti.
3. Asociar perfiles a rangos/sitios sin mostrar secretos.

## Flujo C — Descubrimiento

Entrada:
- CIDR;
- lista de IPs;
- seed device;
- sitio objetivo;
- credencial/perfil.

Pipeline:

1. Validar alcance.
2. Crear DiscoveryRun.
3. ICMP concurrente con límite.
4. Para hosts vivos:
   - SNMP fingerprint;
   - sysName;
   - sysObjectID;
   - interfaces;
   - LLDP/CDP;
   - vendor fingerprint.
5. Seleccionar IDeviceDriver.
6. Ejecutar adquisición específica.
7. Upsert Device.
8. Upsert DeviceInterface.
9. Persistir NeighborObservation.
10. Correlacionar vecinos.
11. Crear/actualizar NetworkLink únicamente con evidencia suficiente.
12. Registrar conflictos/ambigüedades.
13. Actualizar topología.
14. Mostrar resumen:
   - encontrados;
   - nuevos;
   - actualizados;
   - enlaces confirmados;
   - relaciones pendientes;
   - fallos.

## Flujo D — Topología

Backend entrega:
- nodes;
- edges;
- groups/sites;
- status;
- link evidence;
- affected dependencies.

Frontend Cytoscape:
1. agrupa por NetworkSite;
2. usa ELK;
3. upstream arriba;
4. core/distribución debajo;
5. acceso/AP/CPE aguas abajo;
6. guarda posición manual opcional;
7. evita overlap;
8. etiquetas tienen collision strategy;
9. edge muestra dirección/estado;
10. nodos sin enlace quedan en panel “Sin relación”.

Filtros:
- sitio;
- estado;
- tipo;
- vendor;
- búsqueda;
- ocultar CPE;
- solo afectados.

Click nodo:
- resumen;
- IP;
- modelo;
- uptime;
- última métrica;
- interfaces;
- alertas;
- abrir detalle.

Click enlace:
- extremos;
- interfaces;
- capacidad;
- tráfico;
- errores;
- pérdida/latencia si aplica;
- evidencia que creó el enlace.

## Flujo E — Polling

Scheduler toma dispositivos administrados.

Por dispositivo:
1. ICMP.
2. Driver.GetHealth.
3. Driver.GetMetrics.
4. interfaces.
5. asociaciones inalámbricas cuando aplique.
6. escribe MetricSample.
7. actualiza LastSeen.
8. genera DeviceStateEvent si cambia estado.
9. encola evaluación de reglas.

Política inicial:
- ICMP: 30 s.
- estado SNMP/vendor: 60 s.
- interfaces/counters: 60 s.
- inventario profundo: 15 min.
- vecinos/topología: 5 min.

Configurable por PollingProfile.

## Flujo F — Métricas

Dashboard y detalle consultan MetricQueryService.

Métricas mínimas:
- availability;
- RTT;
- packet loss;
- CPU;
- RAM;
- uptime;
- interface in/out bps;
- interface utilization %;
- errors;
- discards.

Wireless cuando esté disponible:
- signal;
- noise;
- SNR;
- CCQ/quality;
- Tx/Rx rate;
- client count.

Nunca renderizar gráfica vacía sin explicación:
- “sin muestras”;
- “credencial inválida”;
- “métrica no soportada”;
- “polling detenido”;
- “última muestra hace X”.

## Flujo G — Alertas

AlertEvaluationWorker consume estado/métricas.

Ejemplos:
- device down 2 de 3 polls;
- latency > umbral;
- packet loss;
- interface down;
- utilization alta;
- signal bajo;
- temperatura/CPU cuando soporte.

Estados:
Open → Acknowledged → Resolved.

Cada alerta guarda:
- regla;
- recurso;
- métrica;
- valor;
- umbral;
- first seen;
- last seen;
- evidence.

## Flujo H — Incidentes y dependencia

Una alerta puede abrir/incorporarse a incidente.

Si cae un enlace upstream:
1. TopologyService calcula descendientes.
2. Se marca root cause candidate.
3. Equipos downstream pueden quedar “Unreachable due to upstream”.
4. No generar 200 incidentes independientes por el mismo corte sin correlación.

---

# 15. Topología: reglas técnicas obligatorias

1. Nodes tienen ID estable del Device.
2. Edges tienen ID estable del NetworkLink.
3. Un edge nunca se deduce solo porque dos IPs estén en la misma subred.
4. LLDP/CDP tiene prioridad alta.
5. Asociación wireless confirmada crea relación AP/CPE.
6. Enlace manual queda protegido de eliminación automática.
7. Si el vecino desaparece, se marca stale antes de eliminar.
8. Todo edge conoce interfaces de ambos extremos cuando sea posible.
9. No dibujar etiquetas completas permanentemente si chocan; usar label corta + tooltip.
10. El layout se calcula después de conocer edges, no antes.
11. Sitios se visualizan como grupos/containers.
12. Se puede congelar layout.
13. Se puede reconstruir desde base sin cambiar IDs.
14. Un grafo sin edges muestra explícitamente “0 relaciones confirmadas”; no amontona nodos.

---

# 16. Métricas y almacenamiento

V1 MySQL:

`MetricSamples`
- Id bigint
- ResourceType
- ResourceId
- MetricName
- TimestampUtc
- ValueDouble
- Unit
- Quality

Índice:
(ResourceType, ResourceId, MetricName, TimestampUtc)

Retención:
- muestras crudas: 30 días por defecto;
- rollup 5 min: 180 días;
- rollup 1 hora: 2 años.

La capa Application no depende de MySQL; posteriormente MetricWriter puede reemplazarse por Timescale/Influx sin reescribir UI ni polling.

---

# 17. UX mínima aceptable

## Dashboard
Debe mostrar:
- dispositivos total/up/down/unknown;
- sitios afectados;
- alertas abiertas por severidad;
- incidentes activos;
- disponibilidad;
- tráfico agregado;
- top 5 interfaces saturadas;
- top 5 enlaces con pérdida/latencia;
- últimas actividades.

## Topología
No aceptamos:
- nodos uno encima de otro;
- texto encimado;
- enlaces invisibles;
- “círculos” sin significado;
- colores sin leyenda;
- estado sin timestamp.

## Device Detail
Tabs:
1. Overview
2. Interfaces
3. Metrics
4. Neighbors
5. Wireless
6. Alerts
7. Audit

---

# 18. Laboratorio obligatorio

Se construirá `SimulatedNetworkDriver`.

Topología LAB-01:

Internet
→ EdgeRouter-01
→ CoreSwitch-01
→ Tower-A Backhaul
→ Tower-A Switch
→ AP-A1 / AP-A2 / AP-A3
→ 10 CPE por AP

y:

CoreSwitch-01
→ Tower-B Backhaul
→ Tower-B Switch
→ AP-B1 / AP-B2
→ 10 CPE por AP

Total aproximado:
- 2 sitios;
- 1 edge router;
- 3 switches;
- 2 backhauls;
- 5 AP;
- 50 CPE;
- 61+ nodos;
- relaciones reales deterministas.

Pruebas:
1. ningún nodo se pierde;
2. número esperado de edges;
3. no hay IDs duplicados;
4. layout produce posiciones diferentes;
5. bajar Tower-A Backhaul marca descendientes afectados;
6. métricas aparecen;
7. alerta se crea;
8. incidente correlaciona;
9. reinicio conserva inventario/topología;
10. nueva discovery no duplica nodos/links.

LAB-02:
200 nodos para rendimiento.

---

# 19. E2E obligatorias

1. Setup inicial.
2. Login/logout.
3. Crear sitio.
4. Crear credencial.
5. Ejecutar discovery.
6. Ver dispositivos encontrados.
7. Abrir topología y comprobar edges.
8. Abrir device detail.
9. Ver métricas con muestras.
10. Provocar caída simulada.
11. Ver alerta.
12. Reconocer alerta.
13. Abrir/correlacionar incidente.
14. Recuperar dispositivo.
15. Resolver alerta/incidente.
16. Crear API key desde Integraciones.
17. Revocarla.
18. Comprobar auditoría.

---

# 20. Orden de construcción

## Fase 0 — Skeleton
- crear carpeta y solución;
- 9 proyectos;
- referencias correctas;
- configuración;
- CI;
- build limpio.

Salida: solución vacía compila.

## Fase 1 — Identity + Setup
- Identity;
- usuarios;
- roles;
- setup;
- login;
- logout;
- autorización;
- auditoría básica.

Salida: humano entra sin API key.

## Fase 2 — Modelo WISP
- WispOrganization;
- NetworkSite;
- Device;
- DeviceInterface;
- NetworkLink;
- credenciales.

Salida: CRUD consistente.

## Fase 3 — Discovery Core
- ICMP;
- SNMP;
- fingerprint;
- interfaces;
- observations;
- correlation.

Salida: descubre inventario y relaciones.

## Fase 4 — Topología
- API graph;
- Cytoscape;
- ELK;
- sites;
- nodes;
- edges;
- filtros;
- detalle.

Salida: LAB-01 visible y legible.

## Fase 5 — Drivers
- Generic SNMP;
- MikroTik;
- Ubiquiti.

Salida: capacidades reales sin contaminar dominio.

## Fase 6 — Polling y métricas
- scheduler;
- polling;
- MetricSamples;
- gráficas;
- retention.

Salida: datos cambian en tiempo real/intervalos.

## Fase 7 — Alertas e incidentes
- reglas;
- evaluador;
- dependencias;
- correlación;
- notificaciones.

## Fase 8 — Administración
- usuarios;
- integraciones/API keys;
- credenciales;
- auditoría;
- system health.

## Fase 9 — Hardening
- rate limiting;
- antiforgery;
- CSP;
- secret handling;
- timeout;
- retries;
- cancellation;
- concurrency;
- logs;
- health checks.

## Fase 10 — Runtime/E2E
No se corrige “a ojo”.
Cada fallo:
read → reproduce → fix → verify → build → test → runtime.

---

# 21. Definition of Done

AtlasNOC NO está terminado hasta que:

- build Release: 0 errores;
- warnings críticos: 0;
- unit tests: 100%;
- integration tests: 100%;
- runtime lab: 100%;
- E2E: 100%;
- topología LAB-01 legible;
- 61+ nodos sin overlap destructivo;
- enlaces presentes;
- métricas presentes;
- caída upstream correlacionada;
- usuarios humanos funcionan;
- API keys están separadas del login;
- reinicio no destruye estado;
- discovery idempotente;
- credenciales cifradas;
- auditoría demuestra acciones críticas.

---

# 22. Lo que NO se hará

- No copiar el proyecto viejo entero.
- No crear otra topología con “círculos bonitos” sin enlaces.
- No usar API key como login humano.
- No fabricar datos de métricas fuera del modo LAB.
- No meter consultas EF dentro de Razor.
- No meter SNMP/MikroTik/Ubiquiti dentro de controllers.
- No crear un mega-Service con todo.
- No declarar éxito por “compila”.
- No ocultar excepciones para que la pantalla cargue.
- No agregar dependencias sin justificar su función.
- No implementar configuración destructiva de routers en la primera versión: AtlasNOC v1 observa y monitorea; acciones de configuración vendrán en módulo separado con controles adicionales.

---

# 23. Conteo congelado para arrancar

- Proyectos producción: 5
- Proyectos pruebas: 4
- Entidades iniciales: 24
- Pantallas principales: 20
- MVC Controllers: 16
- API Controllers: 10
- Controllers totales: 26
- Servicios aplicación: 18
- Adaptadores/infra explícitos: 15
- Workers: 6
- Drivers iniciales: 3
- Laboratorios: 2
- E2E base: 18

Estos números pueden cambiar únicamente si una necesidad funcional concreta lo exige; no para inflar arquitectura.
