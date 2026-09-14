# ATLASNOC — CIERRE QUIRÚRGICO POST-AUDITORÍA
## Rango auditado: `0f60df6d1b86a1be048b8f23f550e5703ce96758..a2dd59a12cd2233eba7878f6e14e4df5a8a76d91`

**Repositorio:** `akamike17/AtlasNOC`  
**Rama:** `codex/atlasnoc-review-20260909`  
**HEAD auditado:** `a2dd59a12cd2233eba7878f6e14e4df5a8a76d91`  
**Especificación de referencia:** `ATLASNOC_ESPECIFICACION_MAESTRA_QUIRURGICA.md`  
**Regla:** NO rediseñar. NO rehacer lo correcto. Corregir hallazgos concretos, probar, verificar y cerrar.

---

# 0. VEREDICTO

El rango contiene una expansión real y amplia del producto: control de dispositivos separado del driver de lectura, WISP observations/connectors, clientes/servicios/facturación básica, soporte, inventario, incidentes, rutas, coverage, simulador, snapshot operacional y documentación de verdad.

**PERO NO ESTÁ CERRADO.**

El estado correcto es:

- **Arquitectura operacional:** avanzada.
- **Persistencia / API:** amplia pero todavía con lógica de negocio parcial.
- **Control MikroTik:** **NO APTO PARA LAB REAL TODAVÍA** por errores de seguridad/semántica detectados.
- **Diagnóstico e impacto:** implementaciones demasiado superficiales; pueden producir conclusiones falsas.
- **Cobranza/suspensión/reconexión:** lógica incompleta respecto a la especificación.
- **WISP operacional:** observación sí; provisión/autorización/bloqueo real todavía no encadena negocio → control.
- **UI operacional:** existe punto de entrada, pero el grueso de la nueva funcionalidad vive en API/persistencia; falta demostrar la cabina completa.
- **Lab físico:** correctamente sigue como `NOT PROVEN`.

No declarar `READY` global hasta resolver lo siguiente.

---

# 1. CRITICAL — CONTROL DE RED PUEDE USAR LA CREDENCIAL EQUIVOCADA

Archivo:

`src/AtlasNOC.Infrastructure/Services/NetworkActionService.cs`

Actualmente se obtiene:

```csharp
_db.DeviceCredentials
    .Where(x => x.CanUse)
    .Select(...)
    .FirstOrDefaultAsync(...)
```

Eso selecciona **la primera credencial utilizable global**, no una credencial asociada al dispositivo, sitio, driver, vendor o target.

## Riesgo

Una acción sobre Router-A puede terminar intentando autenticarse con la credencial de Router-B.

Esto es inaceptable para control físico.

## Corrección obligatoria

Resolver credencial por contexto explícito. Debe existir una asociación determinista entre:

- `DeviceId`
- credencial autorizada
- driver/protocolo
- ámbito/site cuando aplique.

Si el dispositivo tiene cero credenciales compatibles:

```text
CREDENTIAL_MISSING
```

Si tiene más de una y no existe preferencia inequívoca:

```text
CREDENTIAL_AMBIGUOUS
```

**Nunca elegir “la primera”.**

Agregar pruebas:

1. dos dispositivos + dos credenciales → cada acción usa la suya;
2. credencial de otro dispositivo → rechazada;
3. ninguna → rechazada;
4. múltiples ambiguas → rechazada;
5. credencial `CanUse=false` → rechazada.

---

# 2. CRITICAL — `SetInterfaceDescription` PUEDE HABILITAR LA INTERFAZ

Archivo:

`src/AtlasNOC.Infrastructure/Devices/MikroTikControlDriver.cs`

Actualmente para cualquier acción distinta de reboot se construye un body que siempre contiene:

```text
disabled = action == DisableInterface ? "true" : "false"
comment = description ?? ""
```

Por lo tanto:

```text
SetInterfaceDescription
```

envía:

```text
disabled=false
```

y puede habilitar una interfaz que estaba deshabilitada.

Además:

```text
EnableInterface / DisableInterface
```

envían:

```text
comment=""
```

y pueden borrar comentarios existentes.

## Corrección obligatoria

Cada acción debe modificar **únicamente** la propiedad objetivo.

- `EnableInterface` → sólo `disabled=false`
- `DisableInterface` → sólo `disabled=true`
- `SetInterfaceDescription` → sólo `comment=<valor>`
- `Reboot` → sólo comando reboot

No enviar propiedades no solicitadas.

Preferir la semántica REST documentada de RouterOS:

- `PATCH /rest/interface/{id-or-name}` para modificar un registro;
- o comando POST concreto sólo si está validado por prueba exacta.

No mezclar semántica de “set” con cuerpo genérico.

## Pruebas obligatorias

Capturar el `HttpRequestMessage` exacto y verificar:

- verbo;
- URI;
- body;
- no propiedades colaterales;
- auth;
- timeout;
- TLS;
- error 4xx/5xx;
- cancelación.

Añadir explícitamente:

```text
SetInterfaceDescription_does_not_enable_disabled_interface
EnableInterface_does_not_clear_comment
DisableInterface_does_not_clear_comment
```

---

# 3. CRITICAL — NO EJECUTAR LAB FÍSICO HASTA CERRAR 1 Y 2

Mantener:

```text
ATLAS_REAL_CONTROL_TESTS=1
ATLAS_REAL_CONTROL_TARGET=<explicit target>
```

o equivalente ya existente.

El lab real debe continuar opt-in y nunca seleccionar un target automáticamente.

Antes de la primera prueba real:

1. target explícito;
2. credencial explícita;
3. interfaz de laboratorio explícita;
4. preview;
5. confirmación;
6. snapshot/config backup cuando sea soportado;
7. ejecución;
8. post-verificación;
9. rollback manual documentado.

---

# 4. HIGH — `INetworkActionService` NO IMPLEMENTA EL ORQUESTADOR PEDIDO

La especificación exige:

```text
autorización
→ capability
→ credencial
→ impacto
→ confirmación
→ backup/snapshot si aplica
→ ejecución
→ auditoría
→ refresh
→ post-verificación
```

El servicio actual prácticamente hace:

```text
device
→ driver
→ capability
→ primera credencial utilizable
→ Execute
→ audit
```

Falta formalizar `PreviewAsync`.

## Corrección

Crear un resultado de preview que incluya como mínimo:

- DeviceId
- Action
- Supported
- CredentialState
- RiskLevel
- Impact
- RequiresConfirmation
- RequiresBackup
- TruthState
- Warnings

La ejecución de acciones `Medium/High` debe requerir token/confirmación vinculada al preview o confirmación explícita verificable, no solamente confiar en la UI.

Definir riesgo:

- `ReadOnly`
- `Low`
- `Medium`
- `High`

`Reboot` e interfaz uplink deben ser al menos `High` cuando puedan afectar servicio.

---

# 5. HIGH — `NetworkImpactService` PRODUCE UNA COMPLETITUD FALSA

Archivo:

`src/AtlasNOC.Infrastructure/Services/NetworkImpactService.cs`

Problemas:

1. Cuenta enlaces adyacentes, no dependencias downstream.
2. Devuelve siempre `Services=0`.
3. Devuelve siempre `Sites=0`.
4. Marca `Complete=true` si existe **cualquier enlace confirmado no stale en toda la base**, aunque no pertenezca al dispositivo consultado.

Eso puede decir “impacto completo” sobre un equipo cuya propia topología es incompleta.

## Corrección

Recorrer el grafo desde el dispositivo objetivo usando sólo enlaces válidos según evidencia/orientación.

Calcular:

- dispositivos downstream;
- sitios downstream;
- servicios/clientes downstream;
- enlaces confirmados;
- ramas sin evidencia;
- `Complete=false` si alguna rama necesaria no puede determinarse.

Nunca inferir dirección si no hay evidencia.

Texto obligatorio cuando corresponda:

```text
Impacto parcial: la topología de este camino está incompleta.
```

Agregar tests con:

- chain A→B→C;
- branch;
- stale link;
- link no confirmado;
- otro enlace confirmado ajeno al target;
- servicios ligados downstream.

---

# 6. HIGH — DIAGNÓSTICO DE CLIENTE USA ALERTAS GLOBALES

Archivo:

`src/AtlasNOC.Infrastructure/Services/NetworkDiagnosticsService.cs`

Actualmente cuenta **todas** las alertas e incidentes abiertos del sistema y con eso degrada al cliente.

Ejemplo:

```text
Cliente A sano
Router de Cliente Z con alerta
→ Cliente A puede devolver DEGRADED
```

Eso es un falso diagnóstico.

## Corrección

Diagnóstico por camino real del servicio:

```text
Customer
→ CustomerService
→ NetworkAssignment / CPE / Router
→ AP/Sector
→ Site
→ Backhaul
→ Core/Upstream
```

Usar sólo evidencia vinculada a ese camino:

- estado del endpoint;
- asociación wireless;
- signal/RSSI/SNR/CCQ si existe;
- device health;
- alertas sobre nodos/enlaces de la ruta;
- incidentes raíz relacionados;
- ping/probes cuando estén habilitados;
- DNS/upstream cuando aplique.

Salida:

```text
Observed
Probable
NotDetermined
```

Nunca “culpar” un equipo sin evidencia.

---

# 7. HIGH — SUSPENSIÓN POR MOROSIDAD NO IMPLEMENTA GRACIA DE 3 DÍAS

Endpoint actual:

```text
POST billing/{customerId}/suspend-if-overdue
```

usa:

```text
Balance > 0
```

como sinónimo de deuda vencida.

Eso no distingue:

- cargo vigente;
- fecha de vencimiento;
- periodo;
- gracia;
- convenio/promesa;
- pago adelantado;
- saldo no vencido.

## Corrección

El ledger necesita suficiente información para responder:

```text
qué se debe
de qué periodo
cuándo venció
cuándo termina la gracia
qué está cubierto por convenio/promesa
```

Implementar política configurable:

```text
GraceDays = 3
```

No suspender hasta:

```text
DueAtUtc + GraceDays
```

salvo suspensión manual autorizada y auditada.

Agregar estados de cobranza derivados, no inventados:

- Current
- DueSoon
- OverdueInGrace
- Overdue
- Suspended
- PromiseActive
- AgreementActive
- Cancelled

---

# 8. HIGH — `suspend/reconnect/activate/change-plan` SÓLO CAMBIAN BASE DE DATOS

Los endpoints actuales modifican `CustomerService.Status` / `PlanId`, pero no ejecutan provisioning real.

Eso está bien como persistencia, pero **no puede presentarse como operación física completa**.

## Corrección

Separar:

```text
Business state
```

de:

```text
Provisioning state
```

Ejemplo:

```text
Requested
Provisioning
Active
Failed
PartiallyApplied
Suspended
```

Las acciones deben pasar por un servicio de provisioning que determine el mecanismo real según infraestructura:

- MikroTik queue/address-list/PPPoE/etc. sólo cuando exista adapter probado;
- AP/CPE/controller sólo cuando exista driver probado;
- `UNSUPPORTED` si no existe.

Si no hay driver real:

```text
Business record updated, network provisioning NOT EXECUTED.
```

No simular éxito físico.

---

# 9. HIGH — CAMBIO DE PLAN NO VALIDA CAPACIDAD NI PRORRATEO

Actualmente:

- requiere `Confirmed`;
- cambia `PlanId`;
- devuelve diferencia mensual.

Falta:

- capacidad del camino completo;
- saturación del sector/AP/backhaul;
- política de oversubscription;
- prorrateo por días restantes;
- cargo futuro;
- rollback si provisioning falla.

## Corrección

Antes de aceptar:

```text
Plan requested
→ route capacity
→ sector/AP capacity
→ backhaul capacity
→ policy
→ preview económico
→ confirmation
→ provisioning
→ persist
```

Si no puede calcularse capacidad:

```text
RequiresFieldValidation / CapacityUnknown
```

No asumir que cabe.

---

# 10. HIGH — PREPAGO NO CREA COBERTURA TEMPORAL REAL

El endpoint de prepago actualmente registra un `Payment` en ledger.

Debe además conservar:

- fecha de inicio;
- fecha de fin;
- meses pagados;
- bonus days;
- política aplicada;
- referencia del pago;
- servicio;
- actor/confirmación.

Política inicial configurable:

- 3 meses → +5 días
- 6 meses → +10 días
- 9 meses → +15 días
- 12 meses → +1 mes

La bonificación debe afectar la vigencia real, no sólo aparecer en la descripción del ledger.

---

# 11. HIGH — `PayAndReconnect` NO MODELA RECONEXIÓN REAL

Actualmente:

```text
registra Payment
→ si Balance <= 0
→ CustomerService.Reconnect()
```

Falta:

- confirmación real del pago;
- método/proveedor;
- cash validation;
- fee de reconexión configurable;
- excepción/condonación con motivo;
- periodo de servicio;
- provisioning real;
- resultado de provisioning;
- auditoría de la reconexión.

Implementar default configurable:

```text
ReconnectionFee = 50 MXN
```

No hardcodear en dominio.

---

# 12. HIGH — PROMESA DE PAGO APLICA MAL PAGOS PARCIALES

En `AddLedger`, para promesas activas:

```text
promise.ApplyPayment(request.Amount);
break;
```

Problemas:

- aplica el monto completo a la primera promesa;
- no calcula remanente;
- no reparte entre promesas;
- el `break` impide continuar;
- puede marcar una promesa saldada y perder semánticamente el excedente respecto al acuerdo.

## Corrección

Usar algoritmo de asignación:

```text
remainingPayment = amount

foreach active promise ordered by due date:
    allocated = min(remainingPayment, promise.Remaining)
    promise.ApplyPayment(allocated)
    remainingPayment -= allocated
    if remainingPayment == 0: break
```

Registrar la asignación con evidencia/ledger.

---

# 13. HIGH — CRÉDITOS POR FALLA SON MANUALES, NO DERIVADOS DE EVIDENCIA

`CreateCredit` acepta libremente:

- IncidentId
- FromUtc
- ToUtc
- SuggestedAmount
- Reason

La especificación requiere que Atlas **sugiera** crédito usando:

- incidente raíz;
- servicio afectado;
- duración afectada;
- periodo/precio;
- atribución al proveedor.

## Corrección

Crear cálculo:

```text
OutageCreditPreview
```

que derive el importe.

El humano puede aprobar/rechazar/modificar con motivo y auditoría.

No acreditar por:

- CPE apagada por el cliente;
- router del cliente sin energía;
- evidencia insuficiente;
- pérdida aislada no atribuible al proveedor.

---

# 14. HIGH — COVERAGE ES DEMASIADO SUPERFICIAL

Actualmente el `GET coverage/evaluate` busca un chequeo por **igualdad exacta de address** y usa `CapacityMbps`.

No existe todavía evaluación real de:

- dirección normalizada;
- coordenadas;
- zona/colonia;
- distancia;
- infraestructura cercana;
- tecnología;
- sector;
- capacidad de ruta;
- evidencia.

## Corrección

No hace falta construir GIS monstruoso, pero sí cumplir el baseline:

```text
NormalizedAddress
Coordinates?
Zone
NearestInfrastructure
Technology
Capacity
Evidence
Decision
```

Si faltan coordenadas/evidencia:

```text
RequiresFieldValidation
```

No devolver `Available` sólo porque alguien persistió manualmente ese estado sin mostrar evidencia.

---

# 15. MEDIUM/HIGH — PLANIFICADOR DE RUTAS NO ES GEOGRÁFICO

El actual:

- ordena por `ScheduledAtUtc`;
- suma duración/travel ya dado;
- aplica 15% si no hay tiempo real.

Truth matrix ya lo reconoce como parcial.

## Cierre mínimo

Añadir al menos:

- prioridad;
- ventana del cliente;
- zona/lat/lon cuando exista;
- duración estimada por tipo de trabajo;
- buffer configurable;
- travel estimate explícito;
- dependencia/root incident;
- recálculo después de retraso.

No hace falta IA.

Si no hay coordenadas:

```text
route quality = PARTIAL
```

---

# 16. MEDIUM/HIGH — `IWispOperationsService` NO CORRELACIONA CLIENTES WISP

Actualmente devuelve:

```text
snapshot.DeviceStates
```

Eso no equivale a:

```text
Customer → Service → CPE → Association → AP → Sector → Site → Backhaul
```

## Corrección

Crear DTO operacional WISP real:

- CustomerId
- ServiceId
- CPE
- MAC
- AP
- Sector
- Site
- signal
- status
- plan
- traffic cuando exista
- capacity state
- authorization state
- evidence/truth level.

Mantener `UNKNOWN` cuando no pueda correlacionarse.

---

# 17. MEDIUM/HIGH — AUTORIZAR/RECHAZAR CPE NO EJECUTA PROVISIÓN/BLOQUEO REAL

`CpeAuthorizationCase.Decide(...)` persiste el estado.

Debe distinguir:

```text
Decision = Authorized
Provisioning = Pending/Applied/Failed/Unsupported
```

y:

```text
Decision = Rejected
Enforcement = Pending/Applied/Failed/Unsupported
```

No afirmar “expulsado/bloqueado” si sólo se cambió una fila.

El mecanismo real depende del driver/AP/controlador.

---

# 18. MEDIUM — FRAUDE/MOVIMIENTO SOSPECHOSO TODAVÍA NECESITA MOTOR DE EVIDENCIA

Existe estado `FraudReview`, pero debe activarse por reglas explicables:

- cambio inesperado MAC/serial;
- CPE vista en otro AP/sector;
- ubicación incompatible;
- reemplazo sin autorización;
- duplicidad imposible.

Salida:

```text
POSIBLE FRAUDE
```

Nunca culpa automática.

Guardar explicación y evidencia.

---

# 19. MEDIUM — BACKUP/ROLLBACK ES METADATO, NO RESTORE OPERACIONAL

`ConfigurationRevision` guarda hashes/source/reason y `KnownGood`.

Eso es útil, pero aún no significa que Atlas pueda:

```text
Backup
Compare
Restore
CloneToReplacement
```

## Corrección

Truth matrix debe seguir `PARTIAL` hasta que exista adaptador real.

Para MikroTik, si se implementa backup/export:

- guardar artefacto seguro;
- hash;
- timestamp;
- versión;
- target;
- restore sólo opt-in y con compatibilidad;
- nunca afirmar restore universal.

---

# 20. MEDIUM — SIMULATED CONTROL DRIVER ES DEMASIADO PERMISIVO

Actualmente devuelve éxito para prácticamente cualquier acción y sólo mantiene estado de enable/disable.

Debe simular:

- reboot state;
- interface description;
- failure injection;
- unsupported capability;
- credential rejection opcional;
- timeout;
- partial failure;
- post-verification.

Eso permitirá que el E2E pruebe el orquestador real sin tocar hardware.

---

# 21. MEDIUM — MANUAL PAYMENT PROVIDER NO ES CAPTURA DE PAGO

`ManualPaymentProvider.CaptureAsync` acepta `Amount > 0`.

Debe entenderse como:

```text
manual payment awaiting/after human validation
```

No como confirmación bancaria.

Agregar estado:

- PendingValidation
- Confirmed
- Rejected

y separar `recorded` de `confirmed`.

Proveedores futuros implementan su propia confirmación/webhook.

---

# 22. MEDIUM — AUDITORÍA DE ACCIONES DE RED ES INSUFICIENTE

La auditoría actual registra esencialmente:

```text
NetworkAction + action + actor + device
```

Debe guardar sin secretos:

- request id/correlation id;
- DeviceId;
- DriverKey;
- action;
- interface;
- risk;
- preview/impact summary;
- credential reference ID, nunca secreto;
- start/end;
- result;
- evidence;
- pre-state;
- post-state;
- reason;
- confirmation actor.

---

# 23. MEDIUM — UI / CABINA OPERACIONAL DEBE DEMOSTRARSE

No basta con endpoints API.

El criterio de cierre sigue siendo:

```text
Grafo → seleccionar nodo → información → acción → preview → confirmación → resultado → actualización
```

y para cliente:

```text
buscar cliente
→ servicio
→ estado/red
→ cuenta/pagos
→ equipo
→ incidentes
→ diagnóstico
→ acciones
```

No es necesario embellecer todo. Sí es obligatorio que los flujos principales puedan realizarse desde la aplicación sin Postman.

---

# 24. PRODUCT TRUTH — CONSERVAR Y ENDURECER

Conservar la filosofía de `PRODUCT_TRUTH_MATRIX.md`.

Estados mínimos:

- OBSERVED
- MONITORED
- MANAGEABLE
- CONTROLLED
- SIMULATED
- UNSUPPORTED
- UNKNOWN

No elevar:

- MikroTik a CONTROLLED físico hasta lab real.
- Ubiquiti control a supported.
- GPON/DSL a supported.
- restore a ready.
- WISP enforcement a real si sólo es DB.

---

# 25. MIGRACIONES Y CONSISTENCIA

Revisar las migraciones añadidas en este rango y comprobar contra MySQL real:

1. `database update` desde base limpia;
2. upgrade desde esquema previo;
3. rollback/restore de backup de test;
4. constraints/FK;
5. índices únicos necesarios;
6. precisión decimal de dinero;
7. timestamps UTC;
8. collation/case handling para MAC, ServiceCode, serials.

No editar migraciones ya aplicadas si están consolidadas; crear migración correctiva.

---

# 26. TESTS OBLIGATORIOS NUEVOS

Además de los existentes, agregar como mínimo:

### Control
- credential scoped to exact device
- ambiguous credential rejected
- unsupported action rejected
- description does not enable interface
- enable/disable does not erase comment
- RouterOS exact method/path/body
- action preview
- high-risk confirmation required
- post-verification failure reported
- audit contains no secret

### Impact
- direct
- multihop
- branch
- stale
- unconfirmed
- unrelated confirmed link does not mark complete
- customer/service count downstream

### Diagnostics
- alert on unrelated device does not degrade customer
- root incident on customer path does degrade
- unknown path returns NotDetermined
- no fabricated root cause

### Billing
- 3-day grace
- due date
- partial payment
- promise allocation
- reconnection fee
- fee waiver with reason
- prepayment validity dates
- bonus days
- change-plan proration
- no reconnection while balance remains overdue

### WISP
- unknown CPE detected
- authorize requires service
- decision != enforcement
- unsupported enforcement reported honestly
- replacement preserves service/customer
- fraud suspicion includes evidence

### Simulator
- simulated timeout
- simulated failure
- simulated reboot
- simulated interface description
- simulated control state verification

---

# 27. PRUEBA DE HUMO DE CIERRE

Construir un escenario reproducible, preferentemente E2E + simulator:

1. Crear zona/sitio.
2. Crear core/backhaul/AP/sector.
3. Crear cliente.
4. Crear plan.
5. Crear servicio.
6. Detectar CPE desconocida.
7. Mostrar `NO AUTORIZADA`.
8. Vincularla al servicio.
9. Autorizar.
10. Provisioning simulado pasa a `Applied`.
11. Crear cargo mensual.
12. Antes del vencimiento no suspender.
13. Dentro de gracia no suspender.
14. Después de gracia suspender.
15. Provisioning simulado refleja suspensión.
16. Registrar pago parcial → no reconectar si queda vencido.
17. Registrar pago completo + fee según política.
18. Reconectar.
19. Cambiar plan con preview de capacidad y prorrateo.
20. Forzar saturación → bloquear/advertir upgrade.
21. Simular falla de AP.
22. Crear incidente raíz.
23. Clientes downstream afectados, no clientes ajenos.
24. Abrir ticket.
25. Programar visita.
26. Calcular ruta.
27. Recuperar AP.
28. Resolver incidente.
29. Calcular crédito sugerido basado en duración real.
30. Revisar auditoría completa.

Si algún paso depende de hardware no disponible:

```text
SIMULATED
```

Debe estar explícitamente marcado.

---

# 28. VALIDACIÓN FINAL

Ejecutar:

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

Además:

- MySQL real de prueba;
- E2E real de software;
- ningún test obligatorio `SKIP`;
- cero secretos en logs;
- cero credenciales hardcoded;
- migrations limpias.

El laboratorio físico queda separado y opt-in.

No declarar GitHub CI si el SHA no tiene checks verificables.

---

# 29. ENTREGABLE DE CODEX

Codex debe terminar con UN reporte breve:

```text
HEAD:
Commits creados:
Archivos principales:
Migraciones:
Build:
Unit:
Integration:
Runtime:
E2E:
MySQL:
Smoke test 30/30:
Lab físico: NOT RUN / RUN
Product truth final:
Hallazgos pendientes:
```

No crear otra arquitectura nueva.

No cambiar de stack.

No borrar funcionalidad existente útil.

No hacer squash antes de revisión final.

No declarar hardware probado sin hardware.

---

# 30. CRITERIO DE ACEPTACIÓN

Se acepta el cierre de software cuando:

- los CRITICAL anteriores están resueltos;
- ningún cambio de control puede tocar propiedades no solicitadas;
- credenciales están scoped correctamente;
- impacto/diagnóstico no producen falsos positivos globales;
- billing respeta due date + gracia;
- business state y provisioning state están separados;
- WISP authorization distingue decisión de enforcement;
- control físico sigue opt-in;
- simulator prueba los mismos workflows;
- smoke test completo pasa;
- MySQL + E2E pasan;
- truth matrix no exagera.

**NO se requiere probar hardware esta noche.**

El hardware real será la siguiente fase controlada.

---

# 31. ORDEN DE EJECUCIÓN

1. Corregir MikroTik control.
2. Corregir credential resolution.
3. Implementar preview/risk/impact/confirmation.
4. Corregir NetworkImpact.
5. Corregir NetworkDiagnostics.
6. Endurecer billing/grace/promise/prepayment/reconnection/change-plan.
7. Separar provisioning state.
8. Encadenar CPE authorization → provisioning/enforcement.
9. Endurecer simulator.
10. Completar UI mínima operacional.
11. Tests.
12. MySQL.
13. E2E.
14. Smoke 30/30.
15. Actualizar product truth.
16. Commit + push.
17. DETENERSE para auditoría externa final.

---

# 32. FRASE DE CIERRE

**SI UNA FILA CAMBIA EN MYSQL PERO EL PRODUCTO AFIRMA QUE CAMBIÓ LA RED, LA TAREA NO ESTÁ TERMINADA.**

**SI UNA ACCIÓN DE INTERFAZ PUEDE MODIFICAR OTRA PROPIEDAD NO SOLICITADA, NO ESTÁ LISTA PARA HARDWARE.**

**SI UN INCIDENTE AJENO PUEDE DEGRADAR A OTRO CLIENTE, EL DIAGNÓSTICO NO ES CONFIABLE.**
