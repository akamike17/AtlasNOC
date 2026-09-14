# ATLASNOC — RECONVERSIÓN QUIRÚRGICA A PRODUCTO DE OPERACIÓN REAL
**Baseline auditado:** `fa31dfacabbc5496753524033c8025eb016ee84b`  
**Objetivo:** dejar AtlasNOC como producto utilizable por un operador no técnico, con pocas pantallas, descubrimiento real, topología basada en evidencia y pruebas E2E que demuestren el flujo completo.

---

## 0. REGLA PRINCIPAL

No agregar módulos nuevos por agregar. No rehacer el dominio. No romper API existente salvo donde se indique.  
La reconversión es de **flujo de producto**, **orquestación** y **presentación**.

AtlasNOC debe contestar una sola pregunta al operador:

> “¿Qué equipos tengo, cómo están conectados y cuál tiene problema?”

El operador NO debe entender CIDR, LLDP, CDP, ARP, OIDs, `CredentialId`, concurrencia, ni saber que existe `RebuildTopologyAsync`.

---

# 1. QUÉ HAY HOY Y POR QUÉ SE SIENTE ROTO

El backend útil ya existe:

- `DiscoveryService` hace ping, SNMP, interfaces, LLDP, CDP y ARP.
- `TopologyService` ya evita inventar enlaces ambiguos.
- Los IDs de enlace son deterministas.
- Hay persistencia de `DiscoveryRun`.
- Hay servicios de dispositivos, métricas, alertas, incidentes y CVE.
- Hay UI MVC/Razor.
- Cytoscape ya dibuja nodos/enlaces.
- Hay pruebas que demuestran enlace LLDP determinista y ausencia de enlaces ambiguos.

El problema principal NO es que “no haya backend”. El problema es el flujo:

```text
usuario
  ↓
Descubrimiento
  ↓
elige CIDR manualmente
  ↓
elige credenciales manualmente
  ↓
elige LLDP/CDP/ARP
  ↓
espera
  ↓
ve resultados
  ↓
promueve DISPOSITIVO POR DISPOSITIVO
  ↓
entra a Topología
  ↓
pulsa Reconstruir
  ↓
recién entonces puede aparecer algo útil
```

Ese flujo es demasiado técnico y fragmentado.

Además, `TopologyService.RebuildTopologyAsync()` sólo crea nodos desde:

```csharp
_deviceRepository.GetAllAsync(...)
```

es decir: **sólo los dispositivos ya promovidos a monitoreo**.

Los dispositivos encontrados por discovery no aparecen como nodos automáticamente.

Por eso AtlasNOC puede encontrar equipos correctamente y aun así el mapa verse vacío o incompleto.

---

# 2. FLUJO NUEVO — EL PRODUCTO DEBE FUNCIONAR ASÍ

Flujo principal obligatorio:

```text
ABRIR ATLASNOC
      ↓
INICIO / MAPA
      ↓
[ DESCUBRIR MI RED ]
      ↓
Atlas detecta adaptador + gateway + subred
      ↓
usuario confirma:
“Escanear 192.168.1.0/24”
      ↓
Discovery real
      ↓
normalización/correlación
      ↓
auto-registro de equipos encontrados
      ↓
reconstrucción automática de topología
      ↓
MAPA
      ↓
Router ─ Switch ─ AP ─ Cliente
      ↓
cada enlace muestra su evidencia
```

Después del primer escaneo, el usuario normal ya no debe ir a “Dispositivos”, “Descubrimiento”, “Métricas”, etc. para entender la red.

---

# 3. REDUCIR LA UI A 3 VISTAS PRINCIPALES

## VISTA 1 — `/`
### “Mi red”

Esta será la pantalla principal y reemplazará funcionalmente la separación actual entre Dashboard + Topología + parte de Devices + parte de Metrics.

Debe contener:

1. Resumen superior: Equipos encontrados, En línea, Fuera de línea, Alertas activas, Último escaneo.
2. Botón principal: `Descubrir mi red` o, si ya existe topología, `Volver a escanear`.
3. Mapa Cytoscape grande.
4. Al hacer clic en nodo, panel lateral con nombre, IP, MAC si se conoce, fabricante, tipo, estado, interfaces, vecino/enlace, evidencia, últimas métricas y alertas activas.
5. Cada enlace debe indicar protocolo/evidencia, confianza, puerto local y puerto remoto si existe.

**NO mostrar enlace si no existe evidencia suficiente.**

Archivos a reconvertir:

```text
Controllers/Ui/DashboardController.cs
Views/Dashboard/Index.cshtml
Controllers/Ui/TopologyUiController.cs
Views/TopologyUi/Index.cshtml
wwwroot/js/topology.js
Models/DashboardModels.cs
```

No crear otra cuarta pantalla `NetworkOverview`. Usar `DashboardController` como pantalla raíz y convertirlo en el orquestador de lectura del producto.

## VISTA 2 — `/discover`
### “Descubrir mi red”

Wizard de máximo 2 pasos.

### Paso 1
Atlas detecta automáticamente:

```text
Adaptador: Ethernet
IP local: 192.168.1.50
Gateway: 192.168.1.1
Red sugerida: 192.168.1.0/24
```

El usuario ve:

```text
Encontré tu red:
192.168.1.0/24

[ Escanear ahora ]
[ Cambiar red ]
```

La opción avanzada queda colapsada:

```text
Opciones avanzadas
  SNMP
  credenciales
  concurrencia
  LLDP/CDP/ARP
```

Valores normales por defecto:

```csharp
MaxConcurrency = 32;
EnableLldp = true;
EnableCdp = true;
EnableArp = true;
PingTimeout = TimeSpan.FromMilliseconds(800);
SnmpTimeout = TimeSpan.FromSeconds(2);
```

### Paso 2
Mostrar progreso y luego resumen:

```text
12 equipos alcanzables
5 identificados por SNMP
2 switches
1 router
1 AP
7 clientes/otros
3 enlaces confirmados
```

Botón final:

```text
[ Ver mi red ]
```

NO pedir “Agregar a monitoreo” uno por uno.

Archivos base:

```text
Controllers/Ui/DiscoveryUiController.cs
Views/DiscoveryUi/Index.cshtml
AtlasNOC.Domain/Services/Implementations/DiscoveryService.cs
```

## VISTA 3 — `/settings`
### “Configuración”

Sólo administración real:

- Credenciales SNMP
- intervalos de monitoreo
- API keys (sólo administrador)
- salud del sistema
- auditoría en pestaña avanzada

Las pantallas actuales `Credentials`, `ApiKeys`, `Audit`, `Health` pueden seguir existiendo internamente, pero el menú lateral NO debe mostrar 4-5 entradas.

---

# 4. MENÚ NUEVO

Reemplazar el contenido principal de:

```text
Views/Shared/_Sidebar.cshtml
```

por algo equivalente a:

```text
MI RED
  Inicio
  Descubrir

OPERACIÓN
  Alertas

CONFIGURACIÓN
  Configuración
```

Para usuario ReadOnly:

```text
Inicio
Alertas
```

Para Operator:

```text
Inicio
Descubrir
Alertas
```

Para Administrator:

```text
Inicio
Descubrir
Alertas
Configuración
```

No borrar controladores existentes todavía. Primero ocultarlos del flujo principal y reutilizar sus funciones.

---

# 5. CAMBIO CRÍTICO: DISCOVERY DEBE TERMINAR EN TOPOLOGÍA AUTOMÁTICAMENTE

Crear:

```text
AtlasNOC.Domain/Services/Interfaces/INetworkScanOrchestrator.cs
AtlasNOC.Domain/Services/Implementations/NetworkScanOrchestrator.cs
```

Contrato:

```csharp
public interface INetworkScanOrchestrator
{
    Task<NetworkScanResult> ScanAndBuildAsync(
        NetworkScanRequest request,
        string actor,
        CancellationToken cancellationToken = default);
}
```

DTO:

```csharp
public sealed record NetworkScanRequest(
    string SubnetCidr,
    IReadOnlyList<CredentialId> CredentialIds,
    bool AutoEnroll = true);

public sealed record NetworkScanResult(
    DiscoveryResult Discovery,
    TopologyMap Topology,
    int DevicesCreated,
    int DevicesUpdated,
    int ConfirmedLinks,
    int EvidenceOnlyLinks);
```

Flujo exacto dentro de `NetworkScanOrchestrator.ScanAndBuildAsync`:

```csharp
var discovery = await _discoveryService.DiscoverAsync(...);

if (discovery.Status != DiscoveryStatus.Completed)
    return ...;

await _inventoryReconciler.ReconcileAsync(
    discovery.Devices,
    actor,
    cancellationToken);

var topology = await _topologyService.RebuildTopologyAsync(cancellationToken);

return new NetworkScanResult(...);
```

NO poner esta orquestación en controller.

El controller sólo:

```csharp
var result = await _networkScanOrchestrator.ScanAndBuildAsync(...);
```

y redirige a `/`.

Registrar en `Program.cs`:

```csharp
builder.Services.AddScoped<INetworkScanOrchestrator, NetworkScanOrchestrator>();
builder.Services.AddScoped<IDiscoveryInventoryReconciler, DiscoveryInventoryReconciler>();
```

---

# 6. INVENTARIO AUTOMÁTICO — ELIMINAR “PROMOTE UNO POR UNO”

Crear:

```text
AtlasNOC.Domain/Services/Interfaces/IDiscoveryInventoryReconciler.cs
AtlasNOC.Domain/Services/Implementations/DiscoveryInventoryReconciler.cs
```

Contrato:

```csharp
public interface IDiscoveryInventoryReconciler
{
    Task<InventoryReconcileResult> ReconcileAsync(
        IReadOnlyList<DiscoveredDevice> discovered,
        string actor,
        CancellationToken cancellationToken = default);
}
```

Reglas:

### Si IP ya existe
NO crear duplicado. Actualizar sólo datos enriquecibles si la entidad ya soporta esa actualización. NO destruir nombres manuales del usuario.

### Si IP no existe
Crear automáticamente un `Device`.

Nombre en este orden:

```text
Hostname
↓
Vendor + última parte IP
↓
IP
```

Tipo:

```csharp
discovered.DeviceType ?? DeviceType.Unknown
```

Persistir dispositivo != inventar enlace.

---

# 7. TOPOLOGY SERVICE — RESPONSABILIDADES

Mantener esta regla existente:

```csharp
if (candidates.Count != 1) continue;
```

NO relajarla.

Responsabilidades finales:

```text
DiscoveryService
  descubre hechos

DiscoveryInventoryReconciler
  convierte descubrimiento en inventario

TopologyService
  correlaciona inventario + evidencia

NetworkScanOrchestrator
  ejecuta el flujo completo
```

No hacer que `TopologyService` cree dispositivos.

---

# 8. EVITAR ENLACES FALSOS

Clasificación explícita:

## Confirmed
`LLDP`, `CDP` sólo si el vecino resuelve a exactamente un `DeviceId`.

## Observed
`MAC table` sólo cuando haya puerto local + MAC remota + resolución unívoca.

## Logical
`ARP` NO prueba un cable físico.

En UI:

```text
LLDP/CDP -> línea sólida
MAC      -> línea sólida fina
ARP      -> línea punteada “relación IP observada”
Manual   -> línea manual
```

NO llamar “enlace físico confirmado” a ARP.

`TopologyMetadata.ConfirmedLinks` ya cuenta sólo LLDP/CDP. Mantener esa verdad.

---

# 9. TOPOLOGÍA QUE SE ENTIENDA

Actualmente `TopologyService` posiciona nodos en círculo. Es determinista, pero no representa jerarquía.

Mover el layout a presentación en `wwwroot/js/topology.js`:

Si hay enlaces:

```javascript
layout: {
  name: 'breadthfirst',
  directed: false,
  spacingFactor: 1.4,
  padding: 40
}
```

Si no hay enlaces:

```javascript
layout: {
  name: 'grid',
  fit: true,
  padding: 40
}
```

NO dejar nodos superpuestos ni fingir jerarquía cuando no hay enlace.

---

# 10. DETECCIÓN AUTOMÁTICA DE RED LOCAL

Crear:

```text
AtlasNOC.Domain/Services/Interfaces/ILocalNetworkContextService.cs
AtlasNOC.Domain/Services/Implementations/LocalNetworkContextService.cs
```

DTO:

```csharp
public sealed record LocalNetworkContext(
    string InterfaceName,
    string LocalIp,
    string? GatewayIp,
    string SuggestedSubnetCidr);
```

Implementación usa:

```csharp
NetworkInterface.GetAllNetworkInterfaces()
```

Filtrar:

```text
OperationalStatus.Up
no Loopback
IPv4 válida
gateway IPv4 cuando exista
```

Usar `UnicastIPAddressInformation.IPv4Mask` y calcular CIDR real. NO asumir siempre `/24`.

---

# 11. CREDENCIALES SNMP SIN ASUSTAR AL USUARIO

Primer escaneo debe poder funcionar sin credenciales:

```text
Ping + DNS/hostname + ARP local cuando sea posible
```

Si encuentra dispositivos pero no puede identificarlos:

```text
“Encontré 9 equipos. Puedo obtener modelo, puertos y vecinos si agregas acceso SNMP.”
```

Botón: `[ Configurar SNMP ]`.

No enseñar `CredentialId`.

---

# 12. DETALLE DEL NODO SIN ABRIR OTRA PANTALLA

Modificar `showDetail()` en `wwwroot/js/topology.js`.

El JSON de topología debe permitir mostrar:

```text
Nombre
IP
Vendor
Tipo
Estado
Interfaces
Enlaces
Protocolos
Confianza
```

Para métricas/alertas usar endpoints AJAX específicos si hace falta.

NO obligar a navegar `Mapa -> Device -> Metrics -> Alerts`.

Botón secundario `Detalles técnicos` puede abrir la vista existente.

---

# 13. ALERTAS: UNA SOLA VISTA OPERATIVA

Mantener `/alerts`.

En el mapa:

- rojo = Down/Critical
- amarillo = degradado/alerta
- verde = Up
- gris = desconocido/no probado

Incidents y CVEs pueden seguir existiendo, pero NO tienen que ocupar navegación principal para la primera versión usable.

---

# 14. NO REESCRIBIR ESTO

No tocar salvo bug probado:

```text
CredentialService
AuditService
AlertService
IncidentService
CveService
EfCoreRepository<T>
autorización/policies
secret storage
rate limiting
API pública
```

No introducir MediatR/CQRS/microservicios/Redis/SPA por arquitectura. El objetivo es terminar AtlasNOC, no volverlo a empezar.

---

# 15. PRUEBAS OBLIGATORIAS — COMO ATLASPLC, PERO CON RED

## TEST 1 — Discovery -> Inventory -> Topology

```csharp
ScanAndBuildAsync_DiscoveryAutomaticallyCreatesInventoryAndTopology()
```

Escenario:

```text
192.0.2.1 router
192.0.2.2 switch
LLDP router:Gi1 -> switch:Gi24
```

Assert:

```text
Discovery Completed
2 dispositivos inventariados
2 nodos
1 link
LinkType.Lldp
Confidence > 0
IDs correctos
```

## TEST 2 — NO INVENTAR ENLACE

```csharp
ScanAndBuildAsync_AmbiguousNeighborDoesNotCreateLink()
```

Dos posibles destinos con mismo hostname. Assert `Assert.Empty(topology.Links);`.

## TEST 3 — PING ONLY

```csharp
ScanAndBuildAsync_PingOnlyDeviceAppearsAsUnknownWithoutFakeLink()
```

Assert: dispositivo creado, `DeviceType.Unknown`, nodo aparece, 0 enlaces inventados.

## TEST 4 — RE-ESCANEO IDEMPOTENTE

```csharp
ScanAndBuildAsync_SecondScanDoesNotDuplicateDevicesOrLinks()
```

Ejecutar mismo descubrimiento dos veces. Assert mismo número de dispositivos, mismo enlace, mismo deterministic link id y sin IP duplicadas.

## TEST 5 — PERSISTENCIA REAL / RESTART LÓGICO

Usar MySQL integration test existente.

```text
scope 1:
  discovery
  reconcile
  topology

destruir scope

scope 2:
  cargar inventario
  cargar último discovery persistido
  rebuild topology
```

Assert: nodos sobreviven, enlace LLDP sobrevive y link Id estable.

## TEST 6 — FLUJO WEB DE USUARIO

Con `WebApplicationFactory<Program>`:

```text
login
GET /
GET /discover
POST /discover/start
follow redirect
GET /
GET /topology/json o endpoint consolidado
```

Assert: 200, anti-forgery aplicado, Operator puede ejecutar discovery, ReadOnly no puede, mapa devuelve nodos.

## TEST 7 — VERDAD DE EVIDENCIA

Matriz:

```text
LLDP  -> Confirmed
CDP   -> Confirmed
MAC   -> Observed
ARP   -> Logical
None  -> no link
Ambiguous -> no link
```

Probar cada fila.

---

# 16. PRUEBA DE LABORATORIO REAL

No declarar “AtlasNOC listo” sólo por mocks.

Caso mínimo:

```text
PC AtlasNOC
  ↓ Ethernet/Wi-Fi
router real
  ↓
switch/AP real si existe
  ↓
otros clientes
```

Guardar evidencia:

```text
docs/lab/<fecha>/
  discovery-summary.json
  topology.json
  README.md
```

README debe separar `PROVEN` y `NOT PROVEN`.

NO subir secretos/community strings.

---

# 17. ESTADOS DE VERDAD EN UI

Cada nodo:

```text
Identificado
Parcial
Sólo alcanzable
No disponible
```

Cada enlace:

```text
Confirmado
Observado
Lógico
Manual
```

Tooltip:

```text
Confirmado por LLDP
Puerto local: Gi1/0/4
Puerto remoto: eth0
Confianza: 0.95
```

Si Atlas no sabe: `Puerto: No determinado`.

Nunca inventar.

---

# 18. ORDEN DE IMPLEMENTACIÓN

### BLOQUE A — Orquestación
1. `ILocalNetworkContextService`
2. `LocalNetworkContextService`
3. `IDiscoveryInventoryReconciler`
4. `DiscoveryInventoryReconciler`
5. `INetworkScanOrchestrator`
6. `NetworkScanOrchestrator`
7. registrar DI

Compilar + unit tests.

### BLOQUE B — flujo web
1. simplificar `DiscoveryUiController`
2. wizard `/discover`
3. eliminar promote manual del flujo normal
4. al finalizar scan redirigir a `/`

Compilar + web tests.

### BLOQUE C — mapa
1. Dashboard absorbe resumen + mapa
2. Cytoscape layout usable
3. drawer de dispositivo
4. evidencia visible
5. estados de verdad

Compilar + smoke manual.

### BLOQUE D — navegación
1. reducir sidebar
2. esconder módulos técnicos
3. Settings consolidado

### BLOQUE E — E2E + laboratorio
1. 7 pruebas anteriores
2. MySQL persistence
3. prueba de red real
4. guardar evidencia

---

# 19. CRITERIOS DE CIERRE

AtlasNOC queda cerrado para baseline pre-vendor cuando un usuario no técnico pueda:

```text
1. abrir AtlasNOC
2. iniciar sesión
3. pulsar “Descubrir mi red”
4. aceptar la red detectada
5. pulsar “Escanear”
6. esperar
7. regresar automáticamente al mapa
8. ver los equipos reales encontrados
9. distinguir cuáles están identificados y cuáles no
10. ver sólo enlaces respaldados por evidencia
11. hacer clic en nodo y ver estado/datos principales
12. repetir escaneo sin duplicar nada
```

Y las pruebas demuestren:

```text
build = 0 errores
tests = 100% green
discovery -> inventory -> topology = green
ambiguous neighbor -> no fake link = green
rescan idempotency = green
logical restart persistence = green
web authorization = green
real lab evidence = documentada
```

---

# 20. PROHIBIDO DECLARAR

No escribir:

```text
“detección física completa”
“mapeo físico garantizado”
“puerto exacto detectado”
“topología exacta”
```

salvo que exista evidencia.

Sí usar:

```text
“Topología observada”
“Enlace confirmado por LLDP”
“Relación lógica observada por ARP”
“Puerto no determinado”
“Dispositivo alcanzable, identidad no confirmada”
```

---

# 21. DEFINICIÓN FINAL DE ATLASNOC

```text
DETECTAR RED
   ↓
DESCUBRIR EQUIPOS
   ↓
IDENTIFICARLOS
   ↓
CORRELACIONAR EVIDENCIA
   ↓
CONSTRUIR TOPOLOGÍA
   ↓
MOSTRAR ESTADO
   ↓
MONITOREAR CAMBIOS
```

El backend actual ya contiene buena parte de las piezas. La tarea es conectarlas en un único flujo de producto, eliminar pasos manuales innecesarios y demostrarlo con pruebas.

---

# INSTRUCCIÓN FINAL AL AGENTE

Trabaja sobre el baseline `fa31dfacabbc5496753524033c8025eb016ee84b`.

Antes de modificar:

```text
read -> understand -> change -> build -> test -> verify
```

No generes otro documento de arquitectura.  
No pares después de diagnosticar.  
No agregues simulaciones para “hacer verde” el producto.

Cada bloque debe terminar con:

```text
dotnet build
dotnet test
```

Al final ejecutar las pruebas E2E y de persistencia.

Si una prueba exige red/hardware real que el entorno no tiene, NO la falsees: déjala como `LAB REQUIRED` y crea el harness para ejecutarla en la máquina real.

Objetivo final:

> Un operador medio menso debe poder abrir AtlasNOC, pulsar “Descubrir mi red” y terminar mirando un mapa útil sin saber qué chingados es LLDP.
