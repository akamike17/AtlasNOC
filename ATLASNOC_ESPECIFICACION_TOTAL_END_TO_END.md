# ATLASNOC --- ESPECIFICACIÓN TÉCNICA TOTAL END-TO-END

## De equipo apagado a operación madura con 100+ clientes y expansión de 4 zonas

> **Documento rector de producto, arquitectura, operación, datos,
> seguridad, UX, automatización, pruebas y cierre.**
>
> AtlasNOC no es un visor de topología. Es una cabina operacional
> ISP/WISP/NOC orientada a grafo que debe descubrir, inventariar,
> correlacionar, monitorear, diagnosticar y ---sólo donde exista
> capacidad probada--- controlar infraestructura; además debe
> administrar clientes, servicios, cobranza, soporte, inventario,
> capacidad, expansión y auditoría.

------------------------------------------------------------------------

# 0. REGLAS ABSOLUTAS

1.  **Verdad técnica antes que apariencia.** Estados: `OBSERVED`,
    `MONITORED`, `MANAGEABLE`, `CONTROLLED`, `SIMULATED`, `UNSUPPORTED`,
    `UNKNOWN`.
2.  **Nunca inventar topología.** LLDP/CDP = físico confirmado; MAC/FDB
    = observación L2; ARP = relación lógica; wireless association =
    radio observada; manual = usuario; inferido = marcado como inferido,
    jamás confirmado.
3.  Un dispositivo sin enlace confirmado **se muestra como nodo
    aislado**, no desaparece.
4.  Datos simulados sólo en `LabMode` y visualmente identificados como
    `SIMULATED`.
5.  Login humano = Identity/cookie. API key =
    integraciones/automatización, nunca login humano.
6.  Credenciales de equipos cifradas; secretos fuera de logs, vistas,
    auditoría y repositorio.
7.  Controllers no contienen lógica de negocio ni comandos específicos
    de fabricante.
8.  Drivers devuelven contratos neutrales.
9.  Toda acción mutante: autorización → capability check → credencial →
    impacto → preview → confirmación → driver → auditoría → refresh →
    verificación postacción.
10. No declarar READY por compilar, por HTTP 200 ni por unit tests.
    Requiere runtime + E2E + evidencia.
11. No botones muertos, vistas vacías, TODO funcional,
    `NotImplementedException`, mocks productivos ni éxito simulado.
12. Si algo está a medias y es implementable: terminarlo. Si está roto:
    corregirlo. Si falta dentro del alcance: implementarlo.

------------------------------------------------------------------------

# 1. CICLO DE VIDA COMPLETO

``` text
PC APAGADA
  ↓
Windows inicia
  ↓
MySQL disponible
  ↓
AtlasNOC Web + Worker arrancan
  ↓
configuración / migraciones / health / secretos
  ↓
/setup si no existe administrador
  ↓
login humano
  ↓
Dashboard / Centro de red
  ↓
Descubrir mi red
  ↓
adaptador + IP + gateway + CIDR
  ↓
confirmar alcance
  ↓
DiscoveryRun en background
  ↓
ICMP → SNMP/fingerprint → interfaces → LLDP/CDP/ARP/wireless
  ↓
normalización → selección driver → upsert idempotente
  ↓
NeighborObservation → correlación → NetworkLink sólo con evidencia
  ↓
inventario → topología → polling → métricas → alertas → incidentes
  ↓
Sitios/Zonas/Cobertura
  ↓
Prospecto → cobertura → cliente → servicio → instalación → CPE
  ↓
activación → facturación/cuenta corriente → pago → soporte
  ↓
100+ clientes operando
  ↓
capacidad/demanda detecta necesidad de expansión
  ↓
crear 4 zonas nuevas → infraestructura → capacidad → cobertura
  ↓
migrar/crear servicios sin perder identidad/historial
  ↓
operación madura, auditada, respaldada y recuperable
```

------------------------------------------------------------------------

# 2. ARRANQUE DEL EQUIPO Y DEL SOFTWARE

## 2.1 Precondiciones

-   Windows operativo.
-   MySQL 8 accesible.
-   .NET 8 runtime/hosting según despliegue.
-   Configuración de AtlasNOC válida.
-   Secretos fuera del repositorio.
-   Web y Worker con identidad/permisos mínimos necesarios.

## 2.2 Secuencia de bootstrap

1.  Cargar `appsettings.json`.
2.  Aplicar `appsettings.{Environment}.json`.
3.  Aplicar variables de entorno.
4.  Aplicar secretos locales/almacén seguro.
5.  Resolver `ConnectionStrings:DefaultConnection` sin imprimir
    password.
6.  Comprobar conectividad MySQL.
7.  Verificar esquema/migraciones.
8.  Aplicar únicamente migraciones controladas autorizadas.
9.  Registrar DI.
10. Registrar Identity, cookies, roles, policies y API-key
    authentication.
11. Registrar drivers, adapters, repositories, services y workers.
12. Ejecutar health checks.
13. Iniciar Web.
14. Iniciar workers.
15. Si no existe administrador, permitir `/setup`; si ya existe,
    `/setup` queda cerrado.

## 2.3 Variables/configuración críticas

  ------------------------------------------------------------------------------------------------
  Variable/setting                        Propósito                        Regla
  --------------------------------------- -------------------------------- -----------------------
  `ASPNETCORE_ENVIRONMENT`                Development/Testing/Production   Nunca usar Testing para
                                                                           operación real

  `ConnectionStrings:DefaultConnection`   MySQL                            Password secreto

  Polling ICMP                            disponibilidad                   baseline 30 s,
                                                                           configurable

  Polling estado/vendor                   health                           baseline 60 s

  Polling interfaces/counters             métricas                         baseline 60 s

  Inventario profundo                     identidad/capacidades            baseline 15 min

  Vecinos/topología                       relaciones                       baseline 5 min

  Snapshot operacional                    UI                               10--15 s inicialmente

  Metric raw retention                    retención                        30 días default

  Rollup 5 min                            histórico                        180 días

  Rollup 1 h                              histórico largo                  2 años

  `ATLAS_REAL_CONTROL_TESTS`              habilita lab físico opt-in       default off

  `ATLAS_REAL_CONTROL_TARGET`             target explícito lab             nunca autoelegir
  ------------------------------------------------------------------------------------------------

Todo intervalo debe vivir en configuración/perfil, no como constante
dispersa.

------------------------------------------------------------------------

# 3. PRIMER ARRANQUE / SETUP

Ruta `/setup`, sólo cuando no existe administrador.

Campos: - WISP/organización; - username admin; - nombre; - contraseña; -
confirmación.

Resultado transaccional: - `WispOrganization`; - `ApplicationUser`; -
rol Administrator; - `AuditEvent` de bootstrap sin contraseña; - setup
cerrado.

Después: `/account/login` con usuario, contraseña y recordar sesión.

Roles funcionales objetivo: - **Básico/Administrativo:** clientes,
pagos, recibos, prospectos, documentación, cobranza limitada. -
**Operador/NocOperator:** red, discovery, autorización,
suspensión/reconexión, incidentes y control permitido. - **Soporte:**
diagnóstico, tickets, afectados, visitas e historial. -
**Administrator:** seguridad, drivers, credenciales, políticas,
backups/restores, overrides y acciones críticas. - **ReadOnly:**
consulta donde aplique.

------------------------------------------------------------------------

# 4. ARQUITECTURA

## Producción

-   `AtlasNOC.Domain`: entidades, enums, value objects, reglas puras.
-   `AtlasNOC.Application`: DTOs, casos de uso, validación, contratos.
-   `AtlasNOC.Infrastructure`: EF/MySQL, Identity, cifrado, probes,
    drivers/adapters, notificaciones.
-   `AtlasNOC.Worker`:
    discovery/polling/correlation/alerts/notifications/retention.
-   `AtlasNOC.Web`: MVC/Razor/API/auth/UI.

## Pruebas

-   Unit.
-   Integration.
-   Runtime.
-   E2E Playwright.

Dependencias: Domain no conoce EF/MVC/fabricantes; Application no conoce
MySQL concreto; Web no habla RouterOS/SNMP directamente.

------------------------------------------------------------------------

# 5. MODELO DE RED

Soportar sin imponer jerarquía rígida:

``` text
Internet/Upstream
 → Edge/Core
 → distribución
 → central/minicentral/POP/torre/sitio
 → fibra/radio/cobre/GPON/DSL
 → switch/OLT/AP/sector
 → CPE/ONU/ONT/router
 → servicio
 → cliente
```

Cada nodo conoce identidad, ubicación, upstream, downstream,
capacidades, configuración, truth-state, métricas, evidencias,
historial, incidencias y acciones permitidas.

## Entidades de red mínimas

### `NetworkSite`

`Id, Name, Code, SiteType, ParentSiteId?, Latitude?, Longitude?, Address, IsActive`.

### `Device`

`Id, SiteId?, Hostname, ManagementIp, DeviceType, Vendor, Model, SerialNumber, FirmwareVersion, Status, LastSeenAt, LastPolledAt, DriverKey, IsManaged`.

### `DeviceInterface`

`DeviceId, IfIndex, Name, Description, MacAddress, IpAddress, AdminStatus, OperStatus, SpeedBps, InterfaceType, LastSeenAt`.

### `NetworkLink`

`AInterfaceId, BInterfaceId, LinkType, DiscoverySource, Confidence, AdminStatus, OperStatus, CapacityBps, LastSeenAt, IsConfirmed`.

### `NeighborObservation`

`LocalDeviceId, LocalInterfaceId, RemoteIdentity, RemotePortIdentity, Protocol, ObservedAt, RawEvidenceHash`.

Una observación ambigua NO se convierte automáticamente en link.

------------------------------------------------------------------------

# 6. "DESCUBRIR MI RED" --- FLUJO DE USUARIO

El usuario básico no captura CIDR/credenciales/protocolos
innecesariamente.

1.  Dashboard muestra `Descubrir mi red`.
2.  `ILocalNetworkContextService` detecta interfaces utilizables.
3.  Selecciona/proporciona adaptador, IP local, gateway, máscara/CIDR y
    red candidata.
4.  UI muestra: `Red local detectada: 192.168.x.0/24`.
5.  Usuario confirma alcance.
6.  Opcionalmente selecciona credenciales autorizadas.
7.  `INetworkScanOrchestrator.ScanAndBuildAsync()` crea trabajo.
8.  UI vuelve inmediatamente; no bloquea thread web esperando red.
9.  Worker procesa.
10. Al finalizar, inventario y mapa se actualizan automáticamente.

### `NetworkScanRequest`

-   `SubnetCidr`
-   `CredentialIds`
-   `AutoEnroll=true`
-   actor/contexto
-   site opcional
-   cancellation token

### `NetworkScanResult`

-   Discovery result
-   Topology
-   DevicesCreated
-   DevicesUpdated
-   ConfirmedLinks
-   EvidenceOnlyLinks
-   failures/conflicts

------------------------------------------------------------------------

# 7. PIPELINE DE DISCOVERY

1.  Validar alcance CIDR/IP/seed/site.
2.  Crear `DiscoveryRun` con StartedAt/status/actor/scope.
3.  ICMP concurrente con límite y timeout.
4.  Host vivo → SNMP v2c/v3 según perfil.
5.  Obtener `sysName`, `sysObjectID`, uptime.
6.  Fingerprint vendor/model.
7.  Leer interfaces.
8.  Leer LLDP/CDP.
9.  ARP/MAC-FDB sólo como evidencia auxiliar según fuente.
10. Wireless associations donde driver lo soporte.
11. `DeviceDriverRegistry` selecciona driver.
12. Driver obtiene identidad/health/capabilities adicionales.
13. `IDiscoveryInventoryReconciler` hace upsert idempotente.
14. IP existente: no duplicar; enriquecer sin destruir nombre manual.
15. Nueva IP/identidad suficientemente estable: crear Device.
16. Upsert interfaces.
17. Persistir observations.
18. Correlation engine intenta resolver extremos.
19. Ambigüedad: guardar evidencia, no link confirmado.
20. Evidencia suficiente: crear/update `NetworkLink` estable.
21. Rebuild topology.
22. Finalizar `DiscoveryRun` con resumen y errores parciales.

Fallo de un host no aborta todo el scan.

------------------------------------------------------------------------

# 8. DRIVERS Y CAPACIDADES

`IDeviceDriver` lectura neutral: - `CanHandle` - `GetIdentityAsync` -
`GetInterfacesAsync` - `GetNeighborsAsync` - `GetHealthAsync` -
`GetMetricsAsync` - `GetWirelessAssociationsAsync`

Drivers/adapters objetivo: - Generic SNMP. - MikroTik. - Ubiquiti por
familia/protocolo, nunca "Ubiquiti genérico" ficticio. - Cisco
inicialmente observación.

`IDeviceControlDriver` separado para mutación: - Reboot -
Enable/DisableInterface - SetInterfaceDescription - PoeOn/PoeOff -
DisconnectWirelessClient - Enable/DisableSubscriber -
BackupConfiguration - RestoreConfiguration

Sólo exponer una acción si
`IDeviceCapabilityService/IControlDriverRegistry` demuestra soporte.

------------------------------------------------------------------------

# 9. INVENTARIO AUTOMÁTICO

No promover dispositivo por dispositivo.

Un discovery exitoso debe producir inventario automáticamente,
preservando: - IDs estables; - nombres manuales; - historial; -
serial/MAC únicos cuando aplique; - site; - LastSeen/LastPolled; - truth
state; - driver/capabilities.

Rescan idempotente: no duplica Devices, Interfaces ni Links.

------------------------------------------------------------------------

# 10. TOPOLOGÍA / GRAFO

Backend entrega contrato único: - `nodes[]` - `edges[]` - `groups[]` -
`unlinkedNodeCount`

Nodo incluye como mínimo:
`id, label, ip, deviceType, vendor, status, siteId`.

Edge incluye IDs estables, endpoints, tipo, evidencia, confianza, estado
y confirmación.

Frontend Cytoscape: - layout jerárquico/anti-overlap; - upstream
arriba; - grupos por site; - filtros site/status/type/vendor/search; -
ocultar CPE; - sólo afectados; - zoom/pan/fit; - posiciones manuales
opcionales; - refresh; - leyenda; - estados con timestamp.

6 dispositivos + 0 enlaces = **6 nodos aislados + "0 relaciones
confirmadas"**.

Click nodo abre panel contextual con resumen, red, interfaces, wireless,
clientes, tráfico, capacidad, alertas, config, backups, incidentes,
historial y acciones aplicables.

Click edge: extremos, interfaces, capacidad, tráfico, errores,
pérdida/latencia y evidencia.

------------------------------------------------------------------------

# 11. POLLING Y MÉTRICAS

`PollingWorker` procesa dispositivos administrados individualmente; un
fallo no detiene el lote.

Por dispositivo: ICMP → health → metrics → interfaces → wireless →
MetricSample → LastSeen → DeviceStateEvent → alert evaluation.

Métricas mínimas: availability, RTT, packet loss, CPU, RAM, uptime,
interface in/out bps, utilization %, errors, discards.

Wireless si existe: signal, noise, SNR, CCQ/quality, Tx/Rx rate, client
count.

`MetricSample`:
`Id, ResourceType, ResourceId, MetricName, TimestampUtc, ValueDouble, Unit, Quality`.

Nunca gráfica muda: mostrar `sin muestras`, `credencial inválida`,
`métrica no soportada`, `polling detenido` o edad de última muestra.

------------------------------------------------------------------------

# 12. ALERTAS E INCIDENTES

Reglas configurables:
recurso/métrica/operador/threshold/severidad/consecutive
failures/enable.

Ejemplos: down 2/3 polls, latencia, loss, interface down, utilización,
señal, temperatura/CPU si soportado.

Alerta: `Open → Acknowledged → Resolved`; guarda regla, recurso, valor,
umbral, first/last seen y evidence.

Caída upstream: - calcular descendientes; - root cause candidate; -
downstream `Unreachable due to upstream`; - correlacionar, no generar
cientos de incidentes independientes.

Incidente raíz conoce infraestructura afectada, servicios/clientes
afectados, tickets y recuperación.

------------------------------------------------------------------------

# 13. ORGANIZACIÓN GEOGRÁFICA Y 4 ZONAS NUEVAS

Jerarquía: País → Estado → Municipio → Zona → Colonia → Central →
POP/Torre/Sitio → Sector → enlace → AP → CPE → cliente.

Una **Zona** debe tener: - Id estable; - código humano único; -
nombre; - parent geography; - polygon/área o referencias geográficas
cuando se disponga; - centrales/sitios asociados; - tecnologías
disponibles; - capacidad total/reservada/usada; - estado (`PLANNED`,
`BUILDING`, `ACTIVE`, `CAPACITY_LIMITED`, `SATURATED`, `RETIRED` o
equivalente coherente con dominio); - fecha de alta/activación; -
responsable/notas auditables.

## Ejemplo expansión a 4 zonas

1.  Planeación detecta demanda/capacidad.
2.  Crear Zona Norte, Sur, Oriente, Poniente con códigos.
3.  Crear/relacionar centrales/POP/torres.
4.  Registrar backhauls y capacidad.
5.  Registrar switches/AP/sectores.
6.  Definir cobertura por evidencia.
7.  Discovery de infraestructura nueva.
8.  Correlacionar topología.
9.  Validar capacidad end-to-end.
10. Cambiar zona `PLANNED→ACTIVE` sólo con infraestructura/cobertura
    comprobada.
11. Habilitar altas comerciales.
12. ServiceCode se genera por zona sin cambiar CustomerId.
13. Si un cliente migra, crear/cambiar servicio y conservar historial.
14. Dashboard/capacity/coverage deben agregar datos por las cuatro zonas
    y globalmente.

------------------------------------------------------------------------

# 14. COBERTURA

Antes de vender: domicilio → normalización → coordenadas → zona →
infraestructura → medio → capacidad → evidencia.

Estados: `AVAILABLE`, `PROBABLY_AVAILABLE`, `REQUIRES_FIELD_VALIDATION`,
`NO_COVERAGE_CONFIRMED`, `CAPACITY_LIMITED`, `SATURATED`, `PLANNED`.

Mostrar infraestructura cercana, tecnología, distancia, sector,
capacidad, clientes actuales y margen. No decir "sin cobertura" sólo por
no coincidir texto de colonia.

Mapa geográfico separado/complementario al grafo: centrales, POP,
torres, AP, sectores, enlaces, clientes, cobertura, demanda, incidentes
y capacidad.

------------------------------------------------------------------------

# 15. PROSPECTO → CLIENTE

Prospecto opcional no es Customer.

`Prospect → coverage → seguimiento → documentación → pago/contrato → instalación → Customer → Service`.

Conservar histórico y usar demanda no cubierta para expansión.

------------------------------------------------------------------------

# 16. CUSTOMER, SERVICE, ADDRESS, EQUIPMENT Y ACCOUNT SON DISTINTOS

## Customer

-   CustomerId global/inmutable
-   número cliente
-   nombre/razón social
-   tipo normal/física/moral
-   teléfono/email
-   fiscal cuando aplique
-   status/created
-   notas controladas
-   debt/equipment/fraud flags

Identidad normal: nombre, clave elector, domicilio, teléfono, email,
comprobante validado, privacidad. No almacenar imagen INE salvo decisión
explícita.

Detectar duplicidad de clave, cliente, teléfonos/email/direcciones
sospechosas; mandar anomalías legítimas a revisión, no bloquear
ciegamente.

## Service

-   ServiceId
-   ServiceCode humano por zona (`Col_Esp_00001`)
-   CustomerId
-   PlanId
-   Zone
-   Address/coordinates
-   Technology
-   ActivationDate
-   BillingCycle
-   Status
-   AssignedEquipment
-   NetworkPath
-   History

Un Customer puede tener varios Services.

------------------------------------------------------------------------

# 17. ALTA DEL CLIENTE 1...100

Cada alta repite el mismo workflow transaccional y auditado:

1.  Buscar duplicados.
2.  Crear/usar prospecto.
3.  Validar cobertura.
4.  Validar capacidad de ruta completa.
5.  Capturar identidad/contacto/domicilio.
6.  Seleccionar plan compatible.
7.  Calcular instalación.
8.  Generar contrato/documentos.
9.  Registrar pago requerido.
10. Crear orden de instalación.
11. Reservar equipo/stock.
12. Técnico instala.
13. CPE/router aparece por discovery/wireless.
14. Cabina identifica contra orden/cliente.
15. Asociar Asset/CPE al Service.
16. Validar señal/conectividad.
17. Activar mediante driver sólo si soportado; si no, registrar paso
    manual verificable.
18. Crear backup si dispositivo lo permite.
19. Marcar Service Active.
20. Crear cuenta/ciclo de cobro.
21. Auditar actor, tiempos, equipo y evidencia.

Al cliente #100 el sistema debe seguir teniendo IDs únicos, códigos por
zona, cero duplicados accidentales, capacidad recalculada y tiempos
razonables.

------------------------------------------------------------------------

# 18. CPE DESCONOCIDA / NO AUTORIZADA

Mostrar AP/sector/MAC/IP/vendor/RSSI/SNR/primera detección.

Acciones: Identificar, relacionar orden/cliente, Aceptar,
Rechazar/Expulsar.

Aceptar: asociar Service, registrar inicio, aplicar política soportada,
activar, auditar. Rechazar: marcar no autorizado, ejecutar expulsión
sólo si capability probada, conservar evidencia.

Cambio CPE: nunca crear otro cliente; confirmar reemplazo, motivo,
actor, historial y equipo anterior.

------------------------------------------------------------------------

# 19. PLANES Y CAPACIDAD

Plan: nombre, tecnología, down/up, precio, ciclo, reglas comerciales,
QoS/prioridad, capacidad mínima.

Validación end-to-end:
`Cliente→CPE→AP/Sector→Backhaul→POP→Core→Upstream`.

Calcular capacidad teórica, reservada, promedio, pico, oversubscription,
margen y downstream customers.

Umbrales iniciales configurables: 70% warning, 85% high, 95% critical.

Cambio plan: validar capacidad → diferencia → impacto → confirmación →
aplicar → auditar. Upgrade mid-cycle: prorrateo días restantes y
diferencia al siguiente pago.

------------------------------------------------------------------------

# 20. CUENTA CORRIENTE / BILLING

No pagos sueltos. Cuenta con cargos, mensualidades, descuentos,
créditos, reconexión, instalación, equipo, pagos, convenios, promesas y
saldo.

Estados: corriente, vence pronto, vencido, 1/2/3+ meses, suspendido,
convenio, promesa incumplida, cancelado con saldo.

Pago adelantado configurable baseline: 3m +5 días, 6m +10, 9m +15, 12m
+1 mes.

------------------------------------------------------------------------

# 21. PAGOS

`IPaymentProvider` neutral: Manual, Efectivo, Transferencia, SPEI,
Tarjeta, OXXO, MercadoPago, Stripe, Conekta/futuro.

Pago: CustomerId, ServiceId, PaymentReference, Amount, Date, Provider,
ExternalTransactionId, CapturedBy, Evidence/metadata, estado de
validación.

Pago manual/efectivo no debe considerarse confirmado automáticamente si
requiere validación. Reconexión sólo después de pago validado
suficiente.

------------------------------------------------------------------------

# 22. MOROSIDAD / SUSPENSIÓN / RECONEXIÓN

Baseline configurable: vencimiento → 3 días gracia → suspensión
automática.

Manual anticipada: permiso + motivo + auditoría.

Reconnection fee baseline `$50 MXN`, configurable/condonable con motivo.

Pago electrónico confirmado: reconexión automática si cubre regla.
Efectivo: reconectar al validar usuario autorizado.

Promesa/convenio persiste monto, saldo, fecha, autorizador, condiciones,
vencimiento y estado. Incumplimiento nunca borra historial/deuda.

------------------------------------------------------------------------

# 23. CONTRATOS / RECIBOS

Primera fase: recibos internos, estados de cuenta, comprobantes.
Preparar abstracción fiscal futura sin hardcodear SAT/CFDI cambiante.

Contrato: sin plazo forzoso, mensualidad, condiciones, equipo,
obligaciones, suspensión/reconexión, pagos, devolución, privacidad,
pagaré de equipo de empresa y aceptación/firma. Texto legal
configurable/revisable.

Instalación baseline configurable: ciudad gratis + mes adelantado; fuera
ciudad instalación \$350 + router \$350.

------------------------------------------------------------------------

# 24. INVENTARIO / STOCK

Asset:
`AssetId, Type, Brand, Model, Serial, MAC, Firmware, Owner, Status, Location, Cost/Value, Warranty, History`.

Owner: empresa/cliente.

Estados: UnknownDetected, Stock, Reserved, InProduction, Assigned,
Recovered, PendingInspection, Tested, Repair, Damaged, Retired,
PendingRecovery.

Equipo recuperado: Recovered → PendingInspection → Tested → Stock; si
falla Repair/Retired.

------------------------------------------------------------------------

# 25. BACKUP / CONFIG REVISION / REPLACEMENT

`ConfigurationRevision`: revision, timestamp, user, reason, beforeHash,
afterHash, source, backupLocation, knownGood, deviceId.

Acciones: Backup, Compare, Restore, MarkKnownGood, CloneToReplacement.

Reemplazo: old Failed/Retired → nuevo detectado → validar capacidades →
mismo lugar lógico → config compatible → connectivity check → historial
preservado. Incompatibilidad: diferencias + Admin; jamás restore ciego.

------------------------------------------------------------------------

# 26. ACCIONES DE RED

`INetworkActionService` centraliza mutaciones.

Riesgo: ReadOnly/Low/Medium/High. High requiere confirmación reforzada.

`INetworkImpactService` calcula downstream:
sitios/AP/servicios/incidentes. Si topología incompleta, declarar
impacto parcial.

Primer control real objetivo MikroTik: reboot, enable/disable interface,
set description. Más acciones sólo después de laboratorio.

------------------------------------------------------------------------

# 27. SOPORTE

Cliente llama → buscar Customer/Service → diagnóstico automático →
ticket/incidencia → solución remota → cerrar o visita.

Guardar motivo, síntomas, hora, diagnóstico, acciones, operador,
resultado, duración e incidente raíz.

`INetworkDiagnosticService` recorre CPE/router, AP, asociación, señal,
interface, backhaul, central, gateway, WAN, DNS, loss/latency e
incidentes.

Resultado humano: causa probable, afectados y recomendación. Estado
Observed/Probable/Undetermined.

------------------------------------------------------------------------

# 28. VISITAS Y RUTAS

Visita sólo si cliente acepta. Guardar ventana, zona, tipo,
material/equipo, prioridad.

`ITechnicianRoutePlanner`: prioridad, zona, coordenadas, ventana,
duración, traslado, jornada, buffer, root incident y materiales.

Baselines configurables: domicilio 45m, router 30m, CPE 45m, alignment
60m, RJ45/11 45m, WISP 90m, compleja 120m, fibra existente 120m, fibra
nueva 180--240m, AP/site 90m, backhaul 90m; buffer 10--20%.

Registrar estimado vs real y recalcular ETA si se atrasa.

Prioridad P1/P2/P3/P4; baseline interno 15m/4h, 30m/8h, 1h/24h, 4h/48h.
No venderlo como SLA contractual por defecto.

------------------------------------------------------------------------

# 29. CRÉDITOS POR FALLA

No por ping perdido. Debe existir indisponibilidad atribuible al
proveedor/incidente real y relación con el Service afectado.

Calcular periodo, tarifa, tiempo afectado, crédito sugerido y total.
Nunca sugerir crédito a Customer ajeno al incidente.

------------------------------------------------------------------------

# 30. CANCELACIÓN

Solicitud → saldo final → equipos empresa → recuperación →
desaprovisionamiento → cierre técnico → Service Cancelled.

Nunca borrar Customer. Mostrar deuda/equipo/convenio pendiente.

------------------------------------------------------------------------

# 31. FRAUDE / ANOMALÍAS

Detectar CPE en AP inesperado, zona extraña, MAC no reportada, ubicación
incompatible, duplicidad y patrones anómalos.

Primero `ALERTA DE POSIBLE FRAUDE`; mostrar evidencia. Acciones:
revisar, aceptar cambio legítimo, bloquear/expulsar sólo con capacidad,
abrir caso.

------------------------------------------------------------------------

# 32. NOTIFICACIONES

`INotificationProvider`: InApp primero; arquitectura para
Email/SMS/WhatsApp/Push/Webhook/Telegram/Teams/futuro.

NotificationWorker desacoplado; retries/backoff; estado de entrega; no
perder evento por fallo de un proveedor.

------------------------------------------------------------------------

# 33. SEGURIDAD

-   Identity cookie para humanos.
-   API keys hashed, mostradas una vez,
    owner/description/scopes/expiration/status/revoke.
-   credenciales target cifradas y rotables;
-   HTTPS producción;
-   CSP;
-   antiforgery en MVC mutante;
-   rate limiting;
-   validación de inputs;
-   timeout/retry/cancellation;
-   least privilege;
-   sanitización de auditoría/logs;
-   ningún secreto en commit.

Toda acción crítica audita user, role, UTC timestamp, device,
customer/service, action, reason, sanitized params, result, before/after
cuando aplique, correlation id.

------------------------------------------------------------------------

# 34. UI OPERATIVA

Vistas principales consolidadas: - `/` Centro de red - `/network/{id}`
dispositivo - `/wisp` - `/customers` - `/support` - `/inventory` -
`/coverage` - `/settings`

Funciones internas existentes pueden conservar vistas auxiliares, pero
navegación principal debe seguir tareas del operador.

Dashboard: total/up/down/unknown, sites afectados, alerts por severity,
incidents, availability, aggregate traffic, top interfaces saturadas,
links loss/latency, activity y mapa.

Toda vista: loading, empty, error, unauthorized, validation y success
explícitos; ninguna caja vacía silenciosa.

------------------------------------------------------------------------

# 35. OPERACIÓN CON 100 CLIENTES

Con 100 clientes activos Atlas debe permitir: - búsqueda inmediata por
nombre/número/ServiceCode/teléfono/IP/MAC; - filtrado
zona/plan/status/deuda/AP/sector; - dashboard agregado y por zona; -
impacto de outage calculado por dependencias; - billing batch mensual
idempotente; - cobranza por buckets; - pagos concurrentes sin doble
aplicación; - suspension/reconnection idempotentes; - tickets vinculados
a root incident; - inventario asignado/recovery consistente; - capacity
recalculada por alta/cambio/cancelación; - auditoría consultable; -
tiempos de UI aceptables sin cargar todo en Razor.

No existe lógica especial para "cliente 100": el modelo debe escalar por
diseño.

------------------------------------------------------------------------

# 36. EXPANSIÓN DE CUATRO ZONAS --- ESCENARIO DE ACEPTACIÓN

Estado inicial: 100 clientes distribuidos en zonas existentes.

1.  Capacity planner detecta sectores/backhauls \> umbral.
2.  Prospect demand muestra áreas no cubiertas.
3.  Admin crea cuatro zonas `PLANNED`.
4.  Define códigos y geografía.
5.  Registra centrales/POP/sites/towers planeados.
6.  Registra enlaces/backhauls y capacidades esperadas como planeadas,
    nunca observadas.
7.  Al instalar hardware, discovery lo detecta `OBSERVED`.
8.  Se asocia al site/zona.
9.  Credenciales válidas permiten `MANAGEABLE`.
10. Polling produce `MONITORED`.
11. Control probado permite `CONTROLLED` sólo donde corresponda.
12. Topología conecta únicamente evidencia real.
13. Coverage cambia según infraestructura/capacidad real.
14. Zona pasa ACTIVE sólo tras checklist técnico/comercial.
15. Nuevos prospectos pueden convertirse.
16. ServiceCodes usan secuencia/código de zona.
17. Altas recalculan capacidad.
18. Saturación impide/revisa nuevas altas según política.
19. Falla en nueva zona genera root incident y affected services
    correctos.
20. Reportes separan cada zona y total WISP.

------------------------------------------------------------------------

# 37. VALIDACIONES TRANSVERSALES

IP/mask/gateway/VLAN/MAC; duplicate IP/serial/customer; valid zone;
compatible plan/equipment/driver; valid credential; capacity; allowed
action/role; money \>=0; UTC ordering; concurrency; FK/context
consistency; no cross-customer/service mutation; no cross-zone
accidental reassignment.

Nunca enviar configuración inválida al dispositivo.

------------------------------------------------------------------------

# 38. CONCURRENCIA / TRANSACCIONES / IDEMPOTENCIA

Transacción para operaciones multi-entidad críticas. No dejar Customer
sin BillingAccount, Service sin Customer, Payment aplicado parcialmente,
Equipment doblemente asignado ni NetworkLink con extremos inexistentes.

Idempotency requerida en discovery/upsert, billing generation, payment
reconciliation, suspension/reconnection, alert processing y jobs
reintentables.

Optimistic concurrency donde dos operadores puedan editar lo mismo;
mostrar conflicto, no pisar silenciosamente.

------------------------------------------------------------------------

# 39. WORKERS

-   DiscoveryWorker
-   PollingWorker
-   TopologyCorrelationWorker
-   AlertEvaluationWorker
-   NotificationWorker
-   MetricRetentionWorker

Cada worker: cancellation, timeout, retry/backoff, isolation por item,
structured logging, correlation id, health, no loop agresivo, no
bloquear shutdown.

------------------------------------------------------------------------

# 40. OBSERVABILIDAD DEL PROPIO ATLAS

System Health debe mostrar Web, Worker, DB, jobs, último
polling/discovery, queues si existen, errores recientes y
versión/schema. No mostrar OK si dependencia está fallando.

Logs estructurados sin secretos; distinguir error esperado de excepción;
conservar causa original (no enmascarar 500 como 405 mediante error
handler).

------------------------------------------------------------------------

# 41. SIMULADOR

Debe simular central/router/switch/link/AP/sector/CPE/customer, failure,
saturation, unauthorized client, payment, suspension/reconnection, CPE
replacement, fraud, incident, recovery y routes.

`SimulatedDeviceControlDriver` modifica estado simulado
verificablemente. REAL y SIMULATED siempre distinguibles.

LAB-01: 61+ nodos; LAB-02: 200 nodos para rendimiento.

------------------------------------------------------------------------

# 42. PRUEBAS

## Unit

Cobertura, customer/service separation, duplicates, billing, prepayment,
reconnection, promise, credits, plans, capacity, stock, replacement,
backup/rollback, authorization, capabilities, audit, impact, incident
correlation, route planning, fraud, WISP correlation.

## Integration

MySQL/EF migrations, FKs, transactions, Identity, credential encryption,
repositories, idempotency, concurrency.

## Runtime

Servidor + MySQL + workers + simulated network; restart conserva state.

## E2E Playwright

Setup/login/logout, site, credential, discovery, devices, topology,
device detail, metrics, outage, alert acknowledge, incident, recovery,
API key create/revoke, audit; además flujo
customer→service→billing→suspend→payment→reconnect→support→visit→credit.

Browser debe registrar 0 JS errors, requestfailed y 4xx/5xx inesperados.

------------------------------------------------------------------------

# 43. PRUEBA DE HUMO DE NEGOCIO --- 30 PASOS

1 central; 1 backhaul; 1 site; 1 AP; 3 CPE; 2 customers; 1 unauthorized
CPE; detect; accept one; reject another; create plan; activate service;
generate monthly charge; overdue; suspend; payment; reconnect; saturate
backhaul; critical alert; drop link; root incident; affected customers;
ticket; visit; route; restore link; close incident; credit; statement;
full audit.

Si un paso no existe realmente, no está terminado.

------------------------------------------------------------------------

# 44. PRUEBA DE ESCALA DE NEGOCIO --- 100 CLIENTES + 4 ZONAS

Fixture reproducible: - 1 organización; - ≥1 core/upstream; - ≥4 zonas
activas nuevas; - sitios/torres/POP por zona; - backhauls; -
AP/sectores; - ≥100 Customers; - ≥100 Services; - CPE/assets
asociados; - múltiples planes; - monthly ledger; - algunos overdue; -
payments; - tickets/incidents; - métricas.

Validar: 1. cero CustomerId duplicados; 2. ServiceCode único según
política; 3. cada Service apunta a Customer/Plan/Zone válidos; 4. assets
no doble asignados; 5. topology IDs estables; 6. 0 links inventados; 7.
capacity totals coherentes; 8. billing total = suma ledger; 9. pagos =
movimientos conciliados; 10. suspended/reconnected coherentes; 11.
búsqueda/filtros funcionan; 12. caída de un backhaul afecta sólo
dependientes; 13. root incident evita tormenta de incidentes; 14.
auditoría completa; 15. restart conserva todo; 16. rescan no duplica;
17. cuatro zonas siguen separadas y agregables; 18. UI no se vuelve
inutilizable; 19. workers terminan ciclos dentro de intervalos
configurados; 20. no hay secrets/log leaks.

------------------------------------------------------------------------

# 45. LAB FÍSICO

Opt-in exclusivamente: - `ATLAS_REAL_CONTROL_TESTS=1` -
`ATLAS_REAL_CONTROL_TARGET=<ip explícita>`

Nunca autoelegir target. Read-only primero. Mutaciones sólo dispositivo
autorizado, capability probada, backup/impact/preview/confirmation y
recovery plan.

------------------------------------------------------------------------

# 46. DEFINITION OF DONE TOTAL

No cerrar hasta demostrar: - build Release 0 errores; - warnings
críticos 0; - Unit/Integration/Runtime/E2E requeridos verdes; -
discovery real/simulado; - idempotencia; - topology evidence-based e
interactiva; - nodos aislados visibles; - monitoring/metrics; - root
cause correlation; - Customer/Service/Account/Equipment separation; -
unauthorized CPE workflow; - activation/rejection; - billing/payments; -
suspension/reconnection; - inventory/backups/replacement; -
support/tickets/visits/routes; - capacity/coverage; - 100-client
scenario; - four-zone expansion scenario; - audit/security/roles/API
scopes; - restart persistence; - encrypted credentials; - simulator; -
primer control real sólo donde esté probado; - physical lab marcado
PROVEN o NOT PROVEN honestamente.

Product Truth final debe clasificar cada área
READY/PARTIAL/UNSUPPORTED/NOT PROVEN con evidencia. Nunca promover por
intuición.

------------------------------------------------------------------------

# 47. PROHIBICIONES FINALES

No dashboard-only. No CRUD sin operación. No buttons dead. No production
fake data. No invented topology. No fake actions. No generic Ubiquiti
support. No generic SNMP SET. No secrets/logs/plaintext. No delete
Customer to clear debt. No credit from ping loss. No reconnect before
payment validation. No blind restore. No destructive migration without
analysis. No controllers vendor-specific. No EF queries in Razor. No
giant service. No "finished because it looks good".

------------------------------------------------------------------------

# 48. CONTRATO PARA CUALQUIER AGENTE DE CÓDIGO

Secuencia obligatoria:

`READ → TRACE → REPRODUCE → WRITE → BUILD → TEST → RUNTIME → E2E → BROWSER → REAUDIT`

Antes de crear algo, buscar equivalente. No duplicar
entity/DTO/service/controller/view/worker. Si encuentra gap
implementable, continúa sin pedir permiso. Si aparece bloqueo externo,
demuestra el bloqueo y sigue con todo lo independiente. Nunca modifica
tests o seguridad para esconder el defecto.

La meta final es que una persona de cabina pueda encender el equipo,
entrar a AtlasNOC, descubrir una red, ver y entender infraestructura
real, operar sólo capacidades demostradas, administrar 100+ clientes y
sus servicios/cobranza/soporte, ampliar cuatro zonas sin perder
identidad ni historia y reconstruir exactamente quién hizo qué, qué
falló, a quién afectó y cómo se recuperó.
