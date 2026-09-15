# ATLASNOC --- CIERRE QUIRÚRGICO DE UI, TOPOLOGÍA Y VERDAD FUNCIONAL

## BASE INMUTABLE

Repositorio: `akamike17/AtlasNOC`\
Rama: `atlasnoc`\
Commit base auditado: `8f53172a3d79308a0bfee6096d500c11ce01fc3d`

**NO reescribir, revertir ni "mejorar" el fix de `8f53172` salvo prueba
nueva que demuestre defecto real.**

## OBJETIVO

Cerrar la cadena real **backend → DTO/API → MVC → JavaScript → render →
interacción → navegador**. Caso observado: Dashboard reporta 6
dispositivos y DB OK, pero "Mi red / Topología observada" está vacío.
Tests verdes NO equivalen a producto funcional.

## REGLAS NO NEGOCIABLES

1.  READ → DIAGNOSE → WRITE → VERIFY.
2.  No cambios masivos por regex/sed.
3.  No borrar `[Authorize]`, policies, scopes, roles ni autenticación.
4.  No `[AllowAnonymous]` para hacer pasar UI/tests.
5.  No alterar tests para esconder fallos; SKIP no es PASS.
6.  No inventar dispositivos, enlaces, métricas, sitios ni capacidades.
7.  No crear enlaces visuales sin evidencia persistida.
8.  No usar simulación para rellenar la UI normal.
9.  No `git reset --hard`, `git clean`, force push.
10. NO commit/push hasta autorización.
11. Verificar pantallas en navegador real, no sólo unit/API.
12. No terminar con "parece correcto": entregar evidencia.

# HALLAZGO CONFIRMADO A INVESTIGAR PRIMERO

Leer completos: -
`src/AtlasNOC.Web/Controllers/DashboardController.cs` -
`src/AtlasNOC.Web/Views/Dashboard/Index.cshtml` -
`src/AtlasNOC.Web/wwwroot/js/topology.js` -
`src/AtlasNOC.Application/Dtos/Dtos.cs` -
`src/AtlasNOC.Infrastructure/Services/CoreServices.cs` -
`src/AtlasNOC.Web/Controllers/Api/TopologyApiController.cs` -
`src/AtlasNOC.Web/Controllers/TopologyController.cs` -
`src/AtlasNOC.Web/Views/Topology/Index.cshtml`

`DashboardController` obtiene `ITopologyService.GetGraphAsync(null)` y
la vista serializa `ViewBag.Topology`. El DTO real es:

`TopologyGraphDto(Nodes, Edges, Groups, UnlinkedNodeCount)`

`TopologyNodeDto(Id, Label, Ip, DeviceType, Vendor, Status, SiteId)`

Pero `wwwroot/js/topology.js` consume `graph.nodes`, `graph.edges`, y
para nodos consume `n.hostname` y `n.managementIp`.

**Comprobar con evidencia el JSON real. Hay dos desajustes
potenciales/visibles que NO deben parchearse con datos falsos:** -
serialización Razor directa puede producir `Nodes/Edges` mientras JS
espera `nodes/edges`; - incluso en camelCase el DTO entrega `label/ip`,
no `hostname/managementIp`.

Antes de escribir código capturar cuántos nodos existen en: DB →
`ITopologyService` → JSON incrustado Dashboard → `/api/topology/graph` →
JS → Cytoscape. Localizar exactamente dónde pasan de 6 a 0.

## CRITERIO

Si DB y `GetGraphAsync(null)` tienen 6 dispositivos: - Dashboard debe
mostrar 6 nodos aunque haya 0 enlaces. - 0 enlaces respaldados = 6 nodos
aislados, NO mapa vacío. - Si existen enlaces persistidos válidos,
dibujar sólo esos. - Cada nodo: label/nombre + IP como mínimo. - Nunca
`undefined`, `NaN` ni nodos invisibles. - Grafo realmente vacío: mensaje
explícito, no rectángulo silencioso.

# UNIFICAR PRESENTACIÓN

Actualmente Dashboard usa `wwwroot/js/topology.js` y `/Topology` tiene
otra implementación Cytoscape/ELK embebida. Auditar divergencia y
reutilizar un único contrato/mapeo DTO→Cytoscape cuando sea razonable.
Dashboard y `/Topology` deben representar los mismos nodos para el mismo
filtro.

Cytoscape/ELK se cargan desde CDN. Comprobar errores de carga. Si
AtlasNOC debe operar en LAN sin Internet, preferir assets
locales/versionados o, como mínimo, error visible y dependencia
documentada. No cambiar librería sólo por estética.

# UX MÍNIMA DE TOPOLOGÍA

`/Topology` debe probar: - nodos y enlaces reales; - fit inicial, zoom y
pan; - selección de nodo; - panel/detalle visible: label, IP, estado,
vendor, tipo, sitio; - filtro por sitio; - recarga; - empty state y
error state; - contador `N dispositivos / M enlaces`; - indicador de
nodos sin enlace; - confirmados/no confirmados distinguibles si el
dominio lo expone; - navegación a detalle del dispositivo si existe ruta
real.

# VALIDAR LOS 6 DISPOSITIVOS

Enumerar desde fuente persistida: Id, Hostname, ManagementIp,
DeviceType, Vendor, Status, SiteId, interfaces y LastSeen. Consultar
`NetworkLinks` y `NeighborObservations`. Confirmar cuántos enlaces son
justificables. **No crear enlaces para que el mapa se vea bonito.**

# AUDITORÍA DE TODAS LAS VISTAS

Auditar Dashboard, Devices, Discovery, Topology, Interfaces, Links,
Metrics, Alerts, AlertRules, Incidents, Sites, Integrations,
Credentials, ApiKeys, Subscribers, Operations, System, Users, Audit y
Setup.

Para cada pantalla producir:
`Pantalla | Controller/action | Servicio | Fuente real | API/POST | Permiso | Empty state | Error state | Acción visible | Acción realmente implementada | Prueba | Resultado manual | READY/PARTIAL/BROKEN/UNSUPPORTED`

No marcar READY porque exista `.cshtml`.

# PRODUCT TRUTH

Respetar estados reales del proyecto. No afirmar soporte físico no
probado. No fingir correlación WISP completa, control Ubiquiti genérico,
GPON/DSL, coverage geográfica, restore operativo o lab físico si no
están demostrados. Actualizar matriz de verdad sólo después de pruebas.

# PRUEBAS NUEVAS OBLIGATORIAS

**T1 Dashboard, 6 dispositivos/0 enlaces:** 6 nodos visibles, 0 edges,
labels válidos.\
**T2 Dashboard con enlace real:** source/target corresponden a DeviceId
resueltos desde interfaces.\
**T3 `/Topology`:** nodos visibles, filtro sitio, recarga y
selección/detalle.\
**T4 Empty:** sin dispositivos → mensaje visible, cero excepción JS.\
**T5 Contrato API:** `/api/topology/graph` contiene
`nodes,edges,groups,unlinkedNodeCount` y nodo
`id,label,ip,deviceType,vendor,status,siteId`.\
**T6 Seguridad:** anónimo bloqueado; humano permitido según policy; API
key sin scope falla; con scope funciona.\
**T7 Browser:** cero errores JS, 401/403 inesperados, 404 de assets o
errores Cytoscape/ELK.

# REGRESIÓN

Ejecutar: `dotnet clean` `dotnet restore`
`dotnet build AtlasNOC.sln -c Release`
`dotnet test AtlasNOC.sln -c Release --no-build`

Ejecutar además smoke operacional completo, E2E, Unit, Integration,
Runtime y nuevas pruebas UI/topology. Usar configuración MySQL de prueba
existente; **no incrustar credenciales** en código, tests, docs ni
commit.

Reportar por suite: Passed / Failed / Skipped / Total. Explicar cada
SKIP relevante.

# PRUEBA MANUAL REAL

1.  Login.
2.  Dashboard: comparar contador vs nodos.
3.  `/Topology`: comprobar nodos.
4.  Seleccionar cada nodo y cotejar nombre/IP.
5.  Verificar que ningún enlace sea ficticio.
6.  Discovery sólo en alcance autorizado.
7.  Refrescar y confirmar cambios reales.
8.  DevTools Console/Network: cero errores relevantes.
9.  No declarar cierre si vuelve `6 dispositivos / 0 nodos visibles`.

# CADENA DE VERDAD

`DISCOVERY → PERSISTENCIA → ITopologyService → TopologyGraphDto → MVC/API JSON → JavaScript → CYTOSCAPE → NODO VISIBLE → SELECCIÓN/DETALLE`

Cada flecha requiere evidencia.

# ORDEN PARA CODEX

**Fase A --- sólo lectura:** `git status`, `git diff`, confirmar HEAD;
reproducir; capturar conteos DB/servicio/JSON/API/JS/Cytoscape. **NO
escribir hasta localizar dónde 6 se convierten en 0.**

**Fase B --- corrección mínima:** corregir el primer punto real de
pérdida; no reescribir arquitectura si basta
contrato/serialización/mapeo.

**Fase C --- consolidación:** eliminar divergencia evitable
Dashboard/Topology.

**Fase D --- UX funcional:** estados visible/empty/error/selected.

**Fase E --- pruebas:** agregar pruebas que habrían detectado
exactamente la captura actual.

**Fase F --- auditoría de vistas:** matriz funcional completa; corregir
sólo defectos demostrados.

**Fase G --- regresión y navegador real.**

# STOP CONDITIONS

Parar y reportar si la solución exige debilitar seguridad, inventar
enlaces, borrar pruebas, migración destructiva injustificada,
API/servicio inconsistentes, los 6 dispositivos son
inválidos/duplicados, o una dependencia externa bloquea operación y
cambia el alcance.

# GIT

Durante todo el trabajo: **NO commit / NO push / NO merge / NO force**.
Al final entregar `git status`, `git diff --stat`,
`git diff --name-only`.

# INFORME FINAL OBLIGATORIO

1.  ROOT CAUSE
2.  DATA TRACE: DB → servicio → DTO → JSON → navegador → Cytoscape
3.  FILES CHANGED
4.  UI RESULT: Dashboard + `/Topology`
5.  SECURITY
6.  TEST RESULTS
7.  MANUAL BROWSER VERIFICATION
8.  REMAINING PRODUCT GAPS
9.  GIT STATUS
10. VERDICT: `READY`, `PARTIALLY READY` o `BLOCKED`

# DEFINICIÓN DE TERMINADO

No terminar porque compile, porque API dé 200 o porque tests viejos
estén verdes. Esta fase termina cuando los dispositivos reales que
AtlasNOC dice conocer son **visibles y utilizables** en la topología,
sin inventar relaciones, con seguridad intacta, pruebas que protejan el
flujo y auditoría honesta de las demás vistas.
