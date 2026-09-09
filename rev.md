# AtlasNOC — Revisión técnica y plan obligatorio de corrección

**Repositorio:** `akamike17/AtlasNOC`  
**Rama objetivo:** `deepseek-rebuild`  
**Commit auditado como referencia:** `c458c47abb85aa288b114ad991ac2c33bcb56db9`  
**Objetivo:** llevar AtlasNOC desde un laboratorio funcional a un NOC/WISP coherente, seguro y verificable contra red real.

---

# 0. REGLAS DE EJECUCIÓN

Este documento NO es una lista de sugerencias. Es una lista de cambios concretos que deben implementarse y verificarse.

## Reglas obligatorias

1. Trabajar únicamente sobre `deepseek-rebuild` o sobre una rama nueva derivada exactamente de ese commit.
2. Antes de modificar cada archivo:
   - leer archivo completo;
   - identificar dependencias;
   - no duplicar servicios ni modelos ya existentes.
3. Después de modificar:
   - releer archivo completo;
   - compilar;
   - ejecutar tests afectados.
4. No declarar una fase terminada porque compile.
5. No reemplazar funcionalidad real con mocks fuera de los proyectos de prueba.
6. `LabMode=true` únicamente puede activar simuladores.
7. `LabMode=false` nunca debe fabricar datos.
8. Ningún dato desconocido se representa con `0`, `Up`, `Healthy` o valores inventados.
9. Todo cambio administrativo debe quedar auditado.
10. Toda API externa debe autenticar y autorizar realmente.
11. Todo secreto debe salir del repositorio.
12. No hacer cambios cosméticos antes de reparar integridad, seguridad y flujo real.
13. Cada bug corregido debe recibir al menos una prueba de regresión.
14. No eliminar pruebas para hacer verde el build.
15. Si una prueba descubre otro bug real, corregir el bug antes de continuar.

---

# 1. BLOQUE CRÍTICO — SECRETOS PUBLICADOS

## Problema

Existen cadenas de conexión con credenciales MySQL dentro de archivos versionados.

Archivos implicados:

- `src/AtlasNOC.Web/appsettings.json`
- `src/AtlasNOC.Worker/appsettings.json`
- `tests/AtlasNOC.Tests.Integration/RepositoryIntegrationTests.cs`
- `tests/AtlasNOC.Tests.Runtime/LabRuntimeTests.cs`
- `tests/AtlasNOC.Tests.E2E/E2EFlowsTests.cs`

## Modificar

### `src/AtlasNOC.Web/appsettings.json`

Eliminar usuario/contraseña reales.

Debe quedar una cadena sin secreto utilizable en producción, por ejemplo:

```json
"ConnectionStrings": {
  "DefaultConnection": ""
}
```

La aplicación debe exigir:

```text
ConnectionStrings__DefaultConnection
```

desde variables de entorno o Secret Manager.

### `src/AtlasNOC.Worker/appsettings.json`

Mismo criterio.

### Tests

No hardcodear credenciales.

Usar variable:

```text
ATLASNOC_TEST_CONNECTION
```

y si no existe:

- Integration/Runtime/E2E deben marcarse como omitidos con mensaje claro;
- o usar una fixture central que resuelva la conexión.

No usar contraseña fallback real.

## Crear

`tests/AtlasNOC.Tests.Shared/TestDatabaseConfiguration.cs`

Responsabilidad:

- leer `ATLASNOC_TEST_CONNECTION`;
- validar que el nombre de base contenga `_test` o `_e2e`;
- impedir accidentalmente borrar `atlasnoc_rebuild`;
- devolver conexión sólo para bases de prueba.

## Prueba obligatoria

Agregar prueba que falle si una cadena de conexión versionada contiene:

```text
Password=
Pwd=
User Id=
```

salvo archivos de ejemplo con valores claramente falsos.

## Criterio de terminado

- ningún secreto real en Git;
- tests siguen pudiendo correr mediante variable de entorno;
- base de producción no puede ser usada por fixtures destructivas.

---

# 2. API KEYS — IMPLEMENTAR AUTENTICACIÓN REAL

## Problema

Las API keys se crean, hashean y revocan, pero no autentican endpoints.

`[Authorize]` usa autenticación de cookie humana.

`X-Api-Key` actualmente sólo participa en rate limiting.

Los scopes tampoco se aplican.

## Archivos

Modificar:

- `src/AtlasNOC.Web/Program.cs`
- `src/AtlasNOC.Domain/Entities/ApiKey.cs`
- `src/AtlasNOC.Infrastructure/Persistence/Repositories/ApiKeyRepository.cs`
- `src/AtlasNOC.Infrastructure/Services/SupportServices.cs`
- `src/AtlasNOC.Web/Controllers/Api/TopologyApiController.cs`
- `src/AtlasNOC.Web/Controllers/Api/MetricsApiController.cs`

Crear:

- `src/AtlasNOC.Web/Security/ApiKeyAuthenticationHandler.cs`
- `src/AtlasNOC.Web/Security/ApiKeyAuthenticationOptions.cs`
- `src/AtlasNOC.Web/Security/ApiScopeRequirement.cs`
- `src/AtlasNOC.Web/Security/ApiScopeAuthorizationHandler.cs`
- `src/AtlasNOC.Web/Security/ApiScopes.cs`

## Implementación requerida

### Esquema API Key

Leer únicamente:

```http
X-Api-Key: atn_xxxxx
```

Pasos:

1. rechazar header ausente;
2. validar prefijo;
3. SHA-256 de la key completa;
4. buscar por hash;
5. verificar:
   - existe;
   - `IsActive`;
   - no expirada;
   - no revocada;
6. generar `ClaimsPrincipal`.

Claims mínimos:

```text
sub = OwnerUserId
auth_type = api_key
api_key_id = Id
scope = cada scope
```

No guardar key plaintext.

### Política combinada

Cookie humana:
- para MVC.

API Key:
- para `/api/*`.

No permitir que una API key funcione como login humano.

### Scopes

Definir constantes:

```text
topology.read
metrics.read
devices.read
devices.write
sites.read
sites.write
discovery.run
alerts.read
alerts.write
incidents.read
incidents.write
system.read
```

`TopologyApiController` debe exigir `topology.read`.

`MetricsApiController` debe exigir `metrics.read`.

Cuando se creen los demás API controllers deben usar su scope correspondiente.

## Tests obligatorios

Agregar Integration/E2E:

1. sin key -> 401;
2. key válida con scope correcto -> 200;
3. key válida sin scope -> 403;
4. key revocada -> 401;
5. key expirada -> 401;
6. key inexistente -> 401;
7. cookie humana sigue funcionando en UI;
8. API key no puede abrir vistas MVC protegidas.

---

# 3. ROLES — READONLY DEBE SER REALMENTE READONLY

## Problema

Varios controllers permiten escritura a cualquier usuario autenticado.

## Modificar

- `SitesController`
- `DevicesController`
- `AlertsController`
- `IncidentsController`

## Regla

GET de lectura:

```csharp
[Authorize(Roles = "Administrator,NocOperator,ReadOnly")]
```

POST/PUT/DELETE:

```csharp
[Authorize(Roles = "Administrator,NocOperator")]
```

Administración de seguridad:

```csharp
[Authorize(Roles = "Administrator")]
```

## Casos exactos

ReadOnly puede:

- ver dashboard;
- ver sites;
- ver devices;
- ver topology;
- ver métricas;
- ver alertas;
- ver incidentes.

ReadOnly NO puede:

- crear sitio;
- crear dispositivo;
- iniciar discovery;
- crear credencial;
- reconocer alerta;
- resolver alerta;
- resolver incidente;
- crear reglas;
- crear/revocar API key;
- administrar usuarios.

## Tests

Crear matriz de autorización E2E:

| Acción | Administrator | NocOperator | ReadOnly |
|---|---:|---:|---:|
| Ver Sites | 200 | 200 | 200 |
| Crear Site | Sí | Sí | No |
| Ejecutar Discovery | Sí | Sí | No |
| Crear Credential | Sí | Sí | No |
| Acknowledge Alert | Sí | Sí | No |
| ApiKeys | Sí | No | No |
| Users | Sí | No | No |

---

# 4. LOGIN — LOCKOUT E ISACTIVE

## Problema 1

Se configura lockout, pero:

```csharp
lockoutOnFailure: false
```

lo desactiva.

## Cambio

`AccountController.Login`:

```csharp
PasswordSignInAsync(
    userName,
    password,
    rememberMe,
    lockoutOnFailure: true)
```

Manejar expresamente:

```csharp
result.IsLockedOut
result.IsNotAllowed
result.RequiresTwoFactor
```

No revelar si el usuario existe.

## Problema 2

`ApplicationUser.IsActive` no se aplica.

## Crear

`src/AtlasNOC.Infrastructure/Identity/AtlasSignInManager.cs`

o un `IUserClaimsPrincipalFactory`/pre-check equivalente.

Antes de autenticar:

```text
IsActive == true
```

Si está desactivado:

- no iniciar sesión;
- invalidar cookie existente en la siguiente validación.

## Agregar validación periódica de cookie

Usar `SecurityStampValidator` o `CookieAuthenticationEvents` para verificar:

- usuario existe;
- usuario sigue activo.

## Tests

- 5 passwords incorrectos -> locked out;
- password correcto durante lockout -> no entra;
- tras ventana -> puede entrar;
- usuario `IsActive=false` -> no entra;
- usuario desactivado mientras tiene cookie -> pierde acceso.

---

# 5. DISCOVERY — USAR CREDENCIAL Y SITIO SELECCIONADOS

## Problema

`DiscoveryRun.CredentialId` y `TargetSiteId` se guardan, pero el executor los ignora.

## Modificar

- `DiscoveryService.cs`
- `DiscoveryExecutor.cs`
- `CredentialService`
- interfaces Application correspondientes.

## Credenciales

Crear un DTO interno seguro:

```csharp
ResolvedDeviceCredential
{
    SnmpVersion
    Community
    UserName
    AuthProtocol
    AuthPassword
    PrivProtocol
    PrivPassword
}
```

Los secretos sólo deben existir en memoria el tiempo necesario.

`CredentialService` debe tener:

```csharp
Task<ResolvedDeviceCredential?> ResolveAsync(Guid id, CancellationToken ct)
```

que:

1. busca credencial;
2. comprueba activa;
3. descifra secretos;
4. devuelve DTO temporal.

Nunca devolver el secreto a una vista.

## DiscoveryExecutor

Al comenzar:

1. parsear `run.CredentialId`;
2. resolver credencial;
3. si fue especificada y no existe/inactiva -> `run.Fail(...)`;
4. pasar credencial al probe/driver.

Eliminar:

```csharp
"public"
```

hardcodeado.

## Site

Al crear dispositivo descubierto:

```csharp
siteId: run.TargetSiteId != null
    ? SiteId.From(Guid.Parse(run.TargetSiteId))
    : null
```

Si el dispositivo ya existe y discovery tiene site explícito:

- actualizar site sólo según política definida;
- no mover automáticamente un equipo entre sites sin auditar el cambio;
- preferiblemente registrar conflicto/pending assignment.

## Tests

- community incorrecta -> no fingerprint;
- community correcta -> discovery exitoso;
- credential inactiva -> run Failed;
- run con SiteId -> equipos nuevos quedan en ese site;
- un SiteId inexistente -> se rechaza antes del worker.

---

# 6. SNMP REAL — V2C, V3, INTERFACES Y LLDP

## Modificar

`src/AtlasNOC.Infrastructure/Probes/SnmpProbe.cs`

## No continuar usando la interfaz actual si obliga a pasar sólo `community`.

Rediseñar `ISnmpProbe` para recibir objeto de autenticación.

Ejemplo:

```csharp
SnmpConnectionOptions
{
    Version,
    Community,
    UserName,
    AuthProtocol,
    AuthPassword,
    PrivacyProtocol,
    PrivacyPassword,
    TimeoutMs,
    Retries
}
```

## SNMP v2c

Mantener GET sysName/sysObjectID/sysDescr.

Aplicar timeout real.

## SNMP v3

Implementar:

- noAuthNoPriv;
- authNoPriv;
- authPriv.

Soportar al menos:

- SHA-1 / SHA-256 donde biblioteca lo permita;
- AES privacy donde biblioteca lo permita.

Rechazar combinaciones inválidas.

## Interfaces reales

No inventar ifIndex.

Obtener:

```text
ifIndex
ifDescr
ifName
ifAlias
ifType
ifAdminStatus
ifOperStatus
ifSpeed / ifHighSpeed
ifPhysAddress
ifInErrors
ifOutErrors
ifInDiscards
ifOutDiscards
```

Si falta dato -> `null`/Unknown.

Nunca:

```text
admin=Up
oper=Up
type=ethernet
```

por defecto sólo porque apareció una fila.

## LLDP

Implementar lectura de LLDP-MIB:

- local port;
- remote chassis id;
- remote port id;
- remote system name;
- remote description si está disponible.

Crear `NeighborData` con evidencia real.

## CDP

Agregar posteriormente mediante MIB Cisco, pero LLDP es obligatorio primero.

## Tests

Usar respuestas SNMP simuladas a nivel de transporte o fixtures de PDU.

Probar:

- ifIndex no secuencial;
- interfaz down;
- missing speed;
- LLDP bidireccional;
- timeout;
- community incorrecta;
- v3 incorrecto;
- v3 correcto.

---

# 7. CIDR E ICMP — CONCURRENCIA Y LÍMITES

## Problema

El código dice concurrente pero escanea secuencialmente.

## Modificar

`DiscoveryExecutor.cs`

Usar concurrencia limitada.

Recomendado:

```csharp
Parallel.ForEachAsync
```

con:

```text
MaxDegreeOfParallelism = configurable
```

Configuración:

```json
"Discovery": {
  "MaxConcurrentPing": 64,
  "PingTimeoutMs": 1000,
  "MaxTargetsPerRun": 4096
}
```

## CidrSubnet

Corregir:

- `/32` -> escanear esa única dirección;
- `/31` -> ambas direcciones son hosts utilizables;
- no crear listas gigantes antes de validar máximo;
- rechazar scope por encima de `MaxTargetsPerRun`.

No aceptar hostname inválido silenciosamente como si fuera IP válida.

## Tests

- `/32`;
- `/31`;
- `/30`;
- `/24`;
- scope demasiado grande;
- mezcla de IP + CIDR;
- token cancelado;
- 100 hosts demuestra paralelismo con tiempo acotado.

---

# 8. MIKROTIK — DRIVER REAL

## Problemas actuales

- opciones vacías;
- credenciales no conectadas;
- velocidad siempre null;
- ifIndex artificial;
- errores silenciados;
- `SkipCertificateValidation` no se aplica.

## Cambiar arquitectura

No usar username/password globales para todos los MikroTik.

El driver debe recibir contexto/credencial por ejecución.

Si no se quiere cambiar toda la interfaz, crear:

```csharp
DeviceAccessContext
{
    ManagementIp
    Credential
}
```

y migrar `IDeviceDriver`.

## HTTP/TLS

No desactivar validación TLS globalmente.

Si se permite certificado autofirmado:

- debe ser opción explícita por perfil;
- registrar warning;
- idealmente permitir thumbprint pinning.

## Interfaces RouterOS

Extraer identificador estable real de interfaz.

No generar `idx++`.

Obtener:

- `.id` o índice estable disponible;
- name;
- type;
- running;
- disabled;
- mac-address;
- actual-mtu;
- actual rate si endpoint lo proporciona.

Eliminar la expresión rota que deja siempre `speed=null`.

## Uptime

RouterOS suele entregar uptime textual.

No asumir JSON number salvo que endpoint realmente lo entregue.

Crear parser de duración RouterOS.

## Tests

MockHttp:

- 401;
- 403;
- 404;
- timeout;
- JSON válido;
- JSON incompleto;
- certificado rechazado;
- interfaz disabled;
- uptime textual.

---

# 9. UBIQUITI — REESCRIBIR PARTE DEL DRIVER

## Problemas

- ControllerUrl puede venir null;
- CreateClient lanza fuera del try;
- AddAuth nunca se usa;
- parser `data` objeto/arreglo es inconsistente;
- endpoints dependen de generación/controlador;
- mezcla conceptos UniFi Controller con device IP.

## Regla arquitectónica

Separar:

```text
UniFiControllerDriver
AirOSDeviceDriver
```

No tratar ambos como el mismo protocolo.

## UniFi Controller

Credencial:

- ControllerUrl;
- usuario/token/API key según versión;
- site;
- TLS policy.

La consulta de dispositivo debe identificarlo por MAC/id, no asumir que:

```text
/api/s/default/stat/device/{ip}
```

es universal.

## Parser JSON

Crear DTOs explícitos o parser robusto.

No usar `JsonElement` ambiguo con helper que exige Object y luego EnumerateArray.

## Autenticación

Implementar el mecanismo real que soporte el controlador seleccionado.

No dejar `AddAuth()` muerto.

## Tests

Fixtures de JSON reales de:

- UniFi Network moderno;
- respuesta vacía;
- controller auth fail;
- device no encontrado;
- cliente wireless;
- uplink.

---

# 10. DISCOVERY — ACTUALIZAR INVENTARIO, NO SÓLO INSERTAR

## Problema

En rediscovery:

- Device existente sólo hace MarkSeen;
- interfaces existentes no se actualizan;
- desaparecidas no se marcan stale;
- links existentes no hacen MarkSeen.

## Modificar dominio

### Device

Agregar método controlado:

```csharp
UpdateDiscoveredIdentity(...)
```

Actualizar:

- hostname;
- vendor;
- model;
- serial;
- firmware;
- driver key;

sólo cuando dato nuevo no sea null y pase reglas de consistencia.

### DeviceInterface

Agregar:

```csharp
Refresh(...)
MarkSeen(...)
MarkStale(...)
```

## DiscoveryExecutor

Por cada interfaz:

- si nueva -> add;
- si existe -> refresh;
- marcar LastSeen.

Al final:

- interfaces previamente conocidas no vistas en esta corrida -> stale o estado Unknown;
- no borrarlas inmediatamente.

## NetworkLink

Si ya existe:

- `MarkSeen()`;
- actualizar confidence si evidencia más fuerte;
- actualizar discovery source bajo política;
- quitar stale.

Si no se ve por N corridas/TTL:

- marcar stale;
- no borrar automáticamente un link manual.

## Tests

- cambiar firmware;
- interfaz pasa Up -> Down;
- interfaz desaparece;
- enlace reaparece;
- rediscovery no duplica.

---

# 11. PERSISTIR NEIGHBOR OBSERVATIONS

## Problema

La correlación en discovery usa observaciones en memoria pero no guarda la evidencia.

## DiscoveryExecutor

Por cada `NeighborData`:

crear `NeighborObservation`.

Campos:

```text
LocalDeviceId = GUID real
LocalInterfaceId = GUID real
RemoteIdentity
RemotePortIdentity
Protocol
RawEvidenceHash
ObservedAtUtc
```

Antes de insertar:

- evitar duplicado exacto dentro de ventana razonable;
- conservar historial si cambia evidencia.

## Correlación

No pasar `LocalDeviceId` como hostname.

Cambiar DTO de correlación para usar:

```text
LocalDeviceGuid
LocalDeviceIdentity
LocalInterfaceGuid
RemoteIdentity
RemotePortIdentity
```

## TopologyCorrelationWorker

No marcar todas las observaciones `Resolved`.

Sólo marcar:

- `Resolved` si produjo correlación inequívoca;
- `Ambiguous` si hay múltiples candidatos;
- `Pending` si falta contraparte;
- `Rejected` si evidencia inválida.

Cambiar el modelo si hace falta:

```csharp
NeighborObservationStatus
```

## Tests

- pendiente hoy + vecino descubierto mañana -> luego resuelve;
- hostname duplicado -> Ambiguous;
- LLDP bidireccional -> Resolved;
- sin contraparte -> sigue Pending.

---

# 12. CORRELADOR TOPOLOGÍA — REESCRIBIR O(n²)

## Problema

Se vuelve a buscar pares en toda la colección dentro de cada grupo.

## Objetivo

Correlación O(n) u O(n log n).

Construir índices:

```text
byLocalIdentity
byRemoteIdentity
byLocalInterface
```

Crear claves normalizadas.

## Reglas de confianza

LLDP/CDP:

- identidad remota;
- remote port;
- contraparte inversa;
- chassis/MAC si existe.

MikroTik/Ubiquiti:

- usar evidencia específica disponible;
- no tratar protocolo desconocido como Manual.

## Protocolo desconocido

Debe resultar:

```text
DiscoverySource.Unknown
```

Agregar enum si no existe.

No auto-confirmar.

## NetworkLink

Cambiar:

```csharp
IsConfirmed = isManual || confidence >= 0.5;
```

por una política explícita.

Ejemplo:

```text
Manual -> confirmado
LLDP/CDP bidireccional -> confirmado
Wireless asociación directa -> confirmado
MikroTik/Ubiquiti fuerte -> confirmado según reglas
Unknown -> nunca auto-confirmado
```

## Estado de link

Al crear:

```text
AdminStatus = Unknown
OperStatus = Unknown
```

salvo dato real.

---

# 13. POLLING — NO INVENTAR MÉTRICAS

## Problema

Si health no trae CPU/memoria se escribe cero.

## Cambiar

Sólo escribir:

```csharp
if (health.CpuPercent.HasValue)
```

y lo mismo para memoria.

## Estado Device

Ping exitoso no debe ser equivalente a saludable.

Definir:

```text
Reachable
Unreachable
Unknown
```

o mantener Status Up/Down pero Health separado.

## LastSeen

`MarkPolled()` NO debe llamar siempre `MarkSeen()`.

Separar:

```csharp
MarkPolled()
MarkSeen()
```

`MarkSeen()` sólo si hubo evidencia real de respuesta.

## Driver contract

Polling debe invocar:

- ICMP;
- GetHealthAsync;
- GetMetricsAsync;
- GetInterfacesAsync a menor frecuencia;
- GetWirelessAssociationsAsync cuando aplica.

No duplicar métricas de health y GetMetrics.

Definir claramente qué produce cada método.

---

# 14. POLLING CONCURRENTE Y POLLINGPROFILE

## Problema

PollAllManaged es serial.

## Implementar

Concurrencia limitada configurable:

```json
"Polling": {
  "MaxConcurrency": 32,
  "DefaultIntervalSeconds": 30
}
```

## PollingProfile

Debe controlar:

- interval;
- timeout;
- retries;
- interface refresh interval;
- wireless refresh interval.

Cada dispositivo puede:

- usar perfil explícito;
- heredar default.

Worker debe calcular dispositivos vencidos por `NextPollAt` o equivalente.

No dormir simplemente 30 s después de un ciclo completo.

---

# 15. SEPARAR HOST WEB Y HOST WORKER

## Problema crítico

`AddInfrastructure()` registra HostedServices y Web también llama `AddInfrastructure()`.

Si se ejecutan Web + Worker, todo se duplica.

## Refactor

Cambiar:

```csharp
AddInfrastructure(bool labMode)
```

por:

```csharp
AddInfrastructure(...)
AddAtlasWorkers(...)
```

### Web

```csharp
builder.Services.AddInfrastructure(...);
```

NO registrar workers.

### Worker

```csharp
services.AddInfrastructure(...);
services.AddAtlasWorkers();
```

### E2E

Si necesita workers dentro del Web de test:

habilitarlos expresamente mediante:

```text
RunWorkersInWebForTests=true
```

o iniciar Worker como proceso separado.

Preferible iniciar ambos procesos para E2E final.

## Tests

Verificar que producción Web no contiene `IHostedService` de NOC.

---

# 16. MULTIINSTANCIA — CLAIM/LEASE DE TRABAJOS

## DiscoveryRun

Agregar campos:

```text
ClaimedBy
ClaimedAtUtc
LeaseExpiresAtUtc
AttemptCount
```

Worker debe reclamar una corrida de manera atómica.

No procesar un `Running` arbitrario.

Si un worker muere:

- tras vencer lease otro puede recuperarlo.

## Cancellation

Agregar:

```text
Cancelled
```

a `DiscoveryRunStatus`.

`OperationCanceledException` por cancelación del run:

- marcar Cancelled.

Cancelación del proceso host:

- no falsear como Failed;
- dejar lease recuperable.

---

# 17. INTEGRIDAD DE BASE — FOREIGN KEYS

## Agregar relaciones EF

Como mínimo:

```text
Site -> Organization
Device -> Site
DeviceInterface -> Device
NetworkLink.AInterface -> DeviceInterface
NetworkLink.BInterface -> DeviceInterface
NeighborObservation -> Device
NeighborObservation -> DeviceInterface
DeviceCapability -> Device
RadioSector -> Device
WirelessAssociation -> AP/CPE Device
ServiceEndpoint -> Device
```

Definir delete behavior cuidadosamente.

No usar cascade delete indiscriminado en evidencia/topología histórica.

## Unicidad NetworkLink

Crear representación canónica:

```text
MinInterfaceId
MaxInterfaceId
```

o columnas calculadas/normalizadas.

Índice único.

Evitar carrera:

```text
A-B
B-A
```

deben ser el mismo link.

---

# 18. SETUP TRANSACCIONAL

## Problema

Puede quedar organización/usuario/roles a medias.

## Implementar

Setup debe:

1. abrir transacción;
2. revalidar que setup siga requerido;
3. crear roles;
4. crear organización;
5. crear admin;
6. agregar rol;
7. SaveChanges;
8. commit.

Ante cualquier error:

- rollback.

Agregar protección contra setup concurrente.

## `CreateAdministratorAsync`

Nunca ignorar `IdentityResult`.

Cambiar retorno a:

```csharp
Task<OperationResult>
```

o lanzar excepción de dominio controlada.

---

# 19. ADMINISTRACIÓN DE USUARIOS

## Agregar acciones

`UsersController`:

- Index;
- Create;
- Edit;
- ChangeRole;
- Disable;
- Enable;
- ResetPassword/GenerateReset;
- Detail.

## Reglas

- no permitir eliminar/desactivar al último Administrator activo;
- un admin no debe poder dejar sistema sin administrador;
- cambios de rol auditados;
- disable invalida sesiones;
- nunca mostrar password/hash.

## E2E

Crear:

- segundo admin;
- NocOperator;
- ReadOnly;
- cambiar rol;
- desactivar;
- verificar acceso.

---

# 20. ALERT RULES — CONSECUTIVE FAULTS REAL

## Problema

`ConsecutiveFaults` se guarda pero se ignora.

## Implementar

Para cada:

```text
RuleId + ResourceType + ResourceId
```

evaluar N muestras consecutivas cronológicamente.

Ejemplo:

```text
ConsecutiveFaults = 3
```

No disparar:

```text
fail, fail, ok
```

Sí disparar:

```text
fail, fail, fail
```

## Operador

Validar al crear:

```text
>
>=
<
<=
==
```

Operador inválido:

- rechazar request;
- no usar fallback `>`.

## Recovery

Una alerta Acknowledged también debe poder resolverse cuando desaparece condición.

Acknowledgement y Resolution son conceptos diferentes.

---

# 21. INCIDENT CORRELATION

## Problema

Cuenta enlaces del dispositivo, pero no calcula dependencias reales.

## Implementar gradualmente

Primero construir grafo device-device desde links confirmados.

Definir orientación cuando exista:

- uplink/downlink;
- upstream/downstream;
- root/core/access.

Si orientación es desconocida:

- no inventarla.

Para incidentes:

1. detectar device down;
2. encontrar vecinos conectados;
3. comprobar cuáles presentan alertas compatibles;
4. calcular candidato de causa raíz;
5. registrar evidencia.

No llamar causa raíz definitiva sin evidencia.

---

# 22. NOTIFICACIONES — NO MARCAR COMO ENVIADO SI NO SE ENVIÓ

## Problema

Email/Slack/PagerDuty no implementados pueden terminar marcados como notificados.

## Cambiar modelo

Crear entidad:

```text
NotificationDelivery
```

Campos:

```text
Id
AlertId
ChannelId
State
AttemptCount
LastAttemptUtc
SentAtUtc
LastError
NextRetryUtc
```

## Worker

Una entrega por:

```text
alert + channel
```

Estados:

```text
Pending
Sending
Sent
Failed
DeadLetter
```

`Alert.NotificationSentAtUtc` puede mantenerse sólo como agregado opcional, no como control principal.

## Canales

Implementar sólo tipos reales.

Si tipo no implementado:

- no marcar Sent;
- registrar Unsupported.

## Webhook

- reutilizar `IHttpClientFactory`;
- timeout;
- retry con backoff;
- no crear `new HttpClient()` por mensaje.

---

# 23. DATA PROTECTION

## Producción

Si ambiente != Development:

- exigir protección externa de key ring;
- o documentar y bloquear arranque si certificado configurado es requerido por política.

No continuar silenciosamente si thumbprint configurado no existe.

Actualmente si no encuentra certificado, sigue.

Cambiar a:

```text
throw InvalidOperationException
```

en producción cuando exista thumbprint inválido.

## Backup

Documentar que DB + keyring/certificado forman parte del backup lógico.

---

# 24. COOKIE Y REVERSE PROXY

## Cookie

Producción:

```csharp
SecurePolicy = CookieSecurePolicy.Always
```

Development puede permitir SameAsRequest.

## Forwarded Headers

Agregar antes de auth/rate limit:

```csharp
UseForwardedHeaders
```

con proxies/networks explícitamente confiables.

No confiar cualquier `X-Forwarded-For` de Internet.

Rate limiter debe usar IP efectiva validada.

---

# 25. AUDITORÍA REAL

## Deben auditarse

- Login exitoso;
- Login fallido;
- lockout;
- Logout;
- Setup;
- Create/Edit Site;
- Create/Edit Device;
- Start Discovery;
- Create/Disable Credential;
- Create/Revoke API key;
- Create/Toggle AlertRule;
- Acknowledge/Resolve Alert;
- Resolve Incident;
- Create/Edit/Disable User;
- ChangeRole.

## Datos auditados

```text
ActorUserId real
ActorUserName/Email
ActorRole
Action
TargetId
TargetType
Result
Reason
IPAddress
UserAgent
TimestampUtc
OldValue permitido
NewValue permitido
```

No guardar secretos en auditoría.

Nunca registrar:

- password;
- API key plaintext;
- community;
- auth password;
- priv password.

---

# 26. VALIDACIÓN DE REQUESTS

Usar DataAnnotations o FluentValidation.

## Validar

### Site
- Name requerido;
- Code requerido;
- Code formato;
- lat [-90,90];
- lon [-180,180].

### Device
- hostname requerido;
- IP válida IPv4/IPv6 según soporte;
- enum DeviceType válido;
- enum Vendor válido;
- SiteId existente.

### Discovery
- scope válido;
- máximo de targets;
- SiteId existente;
- CredentialId existente y activo.

### Credential
- nombre;
- SnmpVersion válido;
- v2c exige community;
- v3 exige username;
- auth/priv coherentes.

### AlertRule
- operador permitido;
- métrica permitida;
- Severity válida;
- ConsecutiveFaults >= 1.

Todos los controllers POST deben:

```csharp
if (!ModelState.IsValid)
    return View(request);
```

---

# 27. API CONTROLLERS FALTANTES

Crear según especificación:

- `ApiDevicesController`
- `ApiSitesController`
- `ApiDiscoveryController`
- `ApiAlertsController`
- `ApiIncidentsController`
- `ApiSubscribersController`
- `ApiIntegrationsController`
- `ApiSystemController`

Ya existen:

- `TopologyApiController`
- `MetricsApiController`

## Reglas

Cada uno:

- API authentication real;
- scope;
- rate limiting;
- DTOs;
- validation;
- no devolver entidades EF directamente;
- cancellation token;
- ProblemDetails para errores.

---

# 28. PANTALLAS FALTANTES

Completar:

- Detalle de sitio;
- Interfaces/Puertos;
- Enlaces;
- Métricas;
- Detalle de alerta;
- Detalle de incidente;
- Suscriptores/CPE;
- administración real de usuarios;
- health/auditoría completa.

No crear vistas vacías sólo para completar conteo.

Cada vista debe usar servicios reales.

---

# 29. E2E — DIVIDIR LAS 18 ETAPAS

Actualmente existe un gran test de ciclo completo.

Mantener un smoke end-to-end, pero crear tests independientes.

## Suite mínima

1. InitialSetupE2E
2. LoginAndLockoutE2E
3. RoleAuthorizationE2E
4. SiteLifecycleE2E
5. CredentialLifecycleE2E
6. DiscoveryLifecycleE2E
7. TopologyE2E
8. DeviceDetailE2E
9. PollingMetricsE2E
10. OutageDetectionE2E
11. AlertCreationE2E
12. AlertAcknowledgementE2E
13. IncidentCorrelationE2E
14. RecoveryE2E
15. IncidentResolutionE2E
16. ApiKeyAuthenticationE2E
17. ApiKeyRevocationE2E
18. AuditTrailE2E

Cada una debe tener assertions propias.

---

# 30. E2E — NO MODIFICAR DB PARA SIMULAR LA CAÍDA

## Problema

La caída actual se fabrica insertando métrica y estado directamente en MySQL.

## Cambiar simulador

Crear estado mutable de laboratorio:

```csharp
ILabNetworkControl
```

Sólo registrado con:

```text
LabMode=true
```

Permitir:

```text
SetNodeOnline(ip, false)
SetNodeOnline(ip, true)
SetCpu(ip, value)
SetLinkState(...)
```

E2E debe:

1. poner nodo offline mediante simulador;
2. esperar PollingWorker;
3. verificar métrica real generada por polling;
4. esperar alerta;
5. esperar incidente;
6. poner nodo online;
7. verificar recuperación.

No tocar tablas directamente para esos pasos.

---

# 31. E2E — API KEY DE VERDAD

Después de crear key:

capturar plaintext mostrado una sola vez.

Hacer:

```http
GET /api/topology/graph
X-Api-Key: ...
```

Esperar 200.

Revocar.

Repetir.

Esperar 401.

Además:

- scope incorrecto -> 403.

---

# 32. E2E — NO MATAR PROCESOS AJENOS

Eliminar `KillPortListener()` basado en netstat/kill genérico.

Usar:

- puerto efímero disponible;
- o fixture que conserve PID del proceso que ella misma inició.

Nunca matar un PID que no creó el test.

---

# 33. E2E — NO PROBAR BINARIO VIEJO

Antes de ejecutar E2E:

- build explícito;
- o usar `dotnet run --no-launch-profile` desde el proyecto;
- o WebApplicationFactory si se decide host in-process.

Si se usa DLL:

verificar timestamp/hash contra build actual.

Pipeline recomendado:

```powershell
dotnet restore
dotnet build AtlasNOC.sln -c Release --no-restore
dotnet test tests\AtlasNOC.Tests.Unit -c Release --no-build
dotnet test tests\AtlasNOC.Tests.Integration -c Release --no-build
dotnet test tests\AtlasNOC.Tests.Runtime -c Release --no-build
dotnet test tests\AtlasNOC.Tests.E2E -c Release --no-build
```

---

# 34. TEST DE CIFRADO REAL

Eliminar prueba que sólo guarda un string que “parece cifrado”.

Crear:

1. `Protect("public")`;
2. comprobar output != plaintext;
3. persistir;
4. recuperar;
5. `Unprotect`;
6. comprobar == `"public"`.

Agregar:

- dato alterado -> Unprotect falla;
- purpose distinto -> no descifra.

---

# 35. TESTS DE DRIVERS REALES A NIVEL HTTP/PARSER

## MikroTik

Probar métodos completos.

## Ubiquiti

Probar métodos completos.

No limitar tests a `CanHandle`.

Usar `HttpMessageHandler` falso determinista.

No necesita Internet.

---

# 36. HEALTH

`SystemHealthService` debe poder responder incluso si DB falla.

Actualmente después de `CanConnect=false` continúa ejecutando CountAsync.

Cambiar:

```text
si DB no conecta:
DatabaseOk=false
DeviceCount=0 o null
OpenAlertCount=0 o null
devolver inmediatamente
```

Idealmente cambiar DTO counts a nullable para distinguir:

```text
0 real
desconocido
```

---

# 37. WORKERS — RESILIENCIA

Cada worker debe:

- crear scope por ciclo;
- usar CancellationToken;
- no tragarse excepciones críticas sin métrica/log;
- registrar duración;
- registrar cantidad procesada;
- evitar overlap del mismo worker;
- soportar multiinstancia mediante lease cuando corresponda.

Agregar health interno por worker:

```text
LastRunStarted
LastRunCompleted
LastSuccess
LastError
ItemsProcessed
```

---

# 38. OBSERVABILIDAD

Agregar métricas operacionales:

```text
atlasnoc_discovery_runs_total
atlasnoc_discovery_targets_total
atlasnoc_poll_duration_ms
atlasnoc_poll_failures_total
atlasnoc_active_alerts
atlasnoc_notification_failures
atlasnoc_worker_last_success_timestamp
```

Puede empezar con logs estructurados si no se incorpora OpenTelemetry todavía.

Nunca loggear secretos.

---

# 39. TOPOLOGÍA — COHERENCIA VISUAL Y DE DATOS

La UI debe diferenciar:

```text
Confirmed
Pending
Stale
Manual
Unknown
```

No dibujar pending como link confirmado.

Los nodos sin relación deben permanecer en sección separada.

Filtro por site sólo debe incluir groups correspondientes a los devices filtrados, o etiquetar claramente grupos vacíos.

---

# 40. MIGRACIONES Y PRODUCCIÓN

No ejecutar migraciones indiscriminadamente desde múltiples Web nodes.

Agregar opción:

```text
Database:ApplyMigrationsOnStartup
```

Default:

- Development = true;
- Production = false.

Crear procedimiento/documentación de deployment:

```powershell
dotnet ef database update
```

o herramienta controlada de migración.

---

# 41. ORDEN EXACTO DE IMPLEMENTACIÓN

No cambiar este orden salvo dependencia técnica real.

## Fase A — Seguridad inmediata

1. eliminar secretos;
2. rotar credencial expuesta;
3. API key auth;
4. scopes;
5. ReadOnly;
6. lockout;
7. IsActive;
8. cookie/forwarded headers;
9. auditoría básica.

### Gate A

No avanzar si falla cualquier test de autenticación/autorización.

---

## Fase B — Discovery real

10. resolver credential;
11. asignar target site;
12. SNMP options;
13. SNMP v2c;
14. SNMP v3;
15. interfaces reales;
16. LLDP;
17. CIDR;
18. concurrencia ICMP;
19. persistir observations.

### Gate B

Prueba contra al menos un agente SNMP controlado debe obtener:

- identity;
- interfaces;
- estado;
- vecino LLDP cuando disponible.

---

## Fase C — Drivers vendor

20. MikroTik;
21. Ubiquiti;
22. tests HTTP/parser;
23. credenciales vendor por perfil.

### Gate C

No aceptar “driver listo” sólo por `CanHandle`.

Debe leer al menos identidad + interfaces + health de fixture realista.

---

## Fase D — Topología

24. correlador nuevo;
25. observations states;
26. link canonical unique;
27. refresh/stale;
28. foreign keys;
29. incident dependencies.

### Gate D

Rediscovery no duplica y una observación ambigua jamás crea link confirmado.

---

## Fase E — Polling

30. no inventar CPU/memory;
31. corregir LastSeen;
32. usar GetMetrics;
33. refresh de interfaces;
34. wireless;
35. concurrencia;
36. PollingProfiles;
37. workers separados.

### Gate E

Una caída simulada debe ser detectada por PollingWorker sin tocar DB manualmente.

---

## Fase F — Alertas/notificaciones

38. consecutive faults;
39. recovery de acknowledged;
40. operadores válidos;
41. deliveries por canal;
42. retries;
43. unsupported channel.

### Gate F

No debe existir alerta marcada como “notificada” si ningún canal tuvo entrega exitosa.

---

## Fase G — UI/API faltante

44. user admin;
45. APIs faltantes;
46. interfaces;
47. links;
48. metrics;
49. subscribers;
50. detalles.

---

## Fase H — Pruebas finales

51. Unit;
52. Integration;
53. Runtime;
54. 18 E2E independientes;
55. smoke E2E completo;
56. red simulada real por probes;
57. prueba hardware/red controlada.

---

# 42. CRITERIOS DE ACEPTACIÓN FINAL

AtlasNOC NO se considera listo hasta cumplir todos:

- [ ] cero secretos versionados;
- [ ] API keys autentican realmente;
- [ ] scopes aplicados;
- [ ] revoke bloquea inmediatamente;
- [ ] ReadOnly no puede modificar;
- [ ] lockout funciona;
- [ ] IsActive funciona;
- [ ] credencial seleccionada se usa realmente;
- [ ] SiteId de discovery tiene efecto real;
- [ ] no hay `"public"` hardcodeado en producción;
- [ ] SNMP v2c funcional;
- [ ] SNMP v3 funcional;
- [ ] interfaces con ifIndex real;
- [ ] LLDP real;
- [ ] discovery concurrente con límite;
- [ ] `/32` y `/31` correctos;
- [ ] NeighborObservations persistidas;
- [ ] correlación no depende de GUID vs hostname incorrectamente;
- [ ] pending no se marca resolved sin resolver;
- [ ] protocolo desconocido no se convierte a Manual;
- [ ] link no nace Up sin evidencia;
- [ ] rediscovery actualiza dispositivos;
- [ ] rediscovery actualiza interfaces;
- [ ] rediscovery refresca links;
- [ ] stale funciona;
- [ ] FKs reales;
- [ ] unique link A-B/B-A;
- [ ] polling no fabrica 0;
- [ ] LastSeen sólo cambia con evidencia de vida;
- [ ] polling concurrente;
- [ ] PollingProfile aplicado;
- [ ] Web no ejecuta workers en producción;
- [ ] Worker no duplica trabajos entre instancias;
- [ ] cancelación deja estado coherente;
- [ ] setup transaccional;
- [ ] usuarios administrables;
- [ ] último admin protegido;
- [ ] ConsecutiveFaults funciona;
- [ ] alertas acknowledged se recuperan;
- [ ] notificación por canal tiene estado propio;
- [ ] ningún canal falso queda como Sent;
- [ ] Data Protection protegido para producción;
- [ ] auditoría cubre acciones administrativas;
- [ ] requests validados;
- [ ] 10 API controllers;
- [ ] pantallas faltantes completas;
- [ ] 18 E2E independientes;
- [ ] E2E no mata procesos ajenos;
- [ ] E2E no usa binario viejo;
- [ ] E2E outage no modifica DB directamente;
- [ ] cifrado probado round-trip;
- [ ] drivers probados con HTTP/parser;
- [ ] `dotnet build -c Release` sin errores;
- [ ] Unit verdes;
- [ ] Integration verdes;
- [ ] Runtime verdes;
- [ ] E2E verdes;
- [ ] prueba contra red física controlada documentada.

---

# 43. PROHIBICIONES DURANTE LA CORRECCIÓN

No hacer:

```text
catch { }
```

sin al menos log/estado cuando el error afecta funcionalidad real.

No usar:

```text
?? 0
```

para métricas desconocidas.

No usar:

```text
Status = Up
```

si sólo se descubrió una relación.

No hardcodear:

```text
public
admin
passwords
controller URLs
```

en código de producción.

No dar por completada una integración vendor con únicamente:

```text
CanHandle(...)
```

No modificar directamente la DB desde E2E para fingir comportamiento que debería producir un worker.

No convertir condiciones desconocidas en estado conocido.

No usar mocks de LAB cuando `LabMode=false`.

---

# 44. SALIDA QUE DEBE PRODUCIR QUIEN EJECUTE ESTE MD

Después de cada fase, entregar:

```text
FASE:
Commit:
Archivos modificados:
Bug(s) corregidos:
Pruebas agregadas:
Build:
Unit:
Integration:
Runtime:
E2E:
Riesgos pendientes:
```

No responder solamente:

```text
"listo"
"compila"
"tests pasan"
"100%"
```

Debe mostrar evidencia concreta.

---

# 45. PRIMER COMMIT RECOMENDADO

Nombre:

```text
security: remove secrets and enforce real auth boundaries
```

Debe incluir únicamente:

- secretos fuera de repo;
- configuración segura de test DB;
- API key authentication;
- scopes;
- lockout;
- IsActive;
- permisos ReadOnly;
- pruebas correspondientes.

No mezclar todavía SNMP/MikroTik/Ubiquiti en ese commit.

---

# 46. SEGUNDO COMMIT RECOMENDADO

```text
discovery: use configured credentials and persist topology evidence
```

Incluye:

- CredentialId real;
- TargetSiteId real;
- SNMP credential options;
- observations persistidas;
- correcciones CIDR;
- concurrencia limitada.

---

# 47. TERCER COMMIT RECOMENDADO

```text
network: implement real SNMP inventory and LLDP
```

Incluye:

- v2c;
- v3;
- ifTable/ifXTable;
- LLDP;
- tests.

---

# 48. CUARTO COMMIT RECOMENDADO

```text
drivers: harden MikroTik and Ubiquiti integrations
```

---

# 49. QUINTO COMMIT RECOMENDADO

```text
topology: make correlation persistent deterministic and safe
```

---

# 50. SEXTO COMMIT RECOMENDADO

```text
polling: remove fabricated metrics and add scheduled concurrency
```

---

# 51. SÉPTIMO COMMIT RECOMENDADO

```text
alerts: enforce consecutive faults recovery and reliable delivery
```

---

# 52. OCTAVO COMMIT RECOMENDADO

```text
platform: separate workers add integrity constraints and complete audit
```

---

# 53. NOVENO COMMIT RECOMENDADO

```text
ui-api: complete missing operational surfaces
```

---

# 54. DÉCIMO COMMIT RECOMENDADO

```text
tests: prove full lifecycle without direct database simulation
```

---

# RESULTADO ESPERADO

Cuando termine este documento, AtlasNOC deberá poder demostrar un flujo real:

```text
credencial segura
    ↓
scope de discovery
    ↓
ICMP real
    ↓
SNMP/vendor real
    ↓
identity real
    ↓
interfaces reales
    ↓
LLDP/neighbor evidence
    ↓
NeighborObservation persistida
    ↓
correlación determinista
    ↓
NetworkLink confirmado
    ↓
polling periódico
    ↓
métricas reales
    ↓
alerta tras N fallos
    ↓
incidente correlacionado
    ↓
notificación con delivery comprobable
    ↓
recuperación
    ↓
cierre
    ↓
auditoría completa
```

Si uno de esos pasos se sustituye por datos inventados, manipulación directa de la DB o comportamiento exclusivo de `LabMode`, el producto todavía NO ha llegado a producción.
