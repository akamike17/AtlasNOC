# ATLASNOC — CIERRE QUIRÚRGICO POST `f8dfeba`

## 0. ORDEN DE EJECUCIÓN — LEER ANTES DE TOCAR CÓDIGO

Estás trabajando sobre **AtlasNOC**, rama:

`codex/atlasnoc-review-20260909`

Baseline remoto obligatorio:

`f8dfeba1e3ce1178f566ef44942fa68ec20d21b9`

Mensaje:

`test: add operational closure matrix and E2E smoke`

Este documento **NO pide rediseñar AtlasNOC**, **NO pide rehacer lo correcto**, **NO pide otra auditoría narrativa** y **NO autoriza detenerse después de cada hallazgo**.

Tu trabajo es cerrar, con código y pruebas, TODO lo realizable sin laboratorio físico ni proveedor externo que todavía esté marcado `PARCIAL` o `NO IMPLEMENTADO` en la matriz post-05670d4.

### REGLA PRINCIPAL

**NO TE DETENGAS PARA REPORTAR AVANCES.**

El ciclo obligatorio es:

`READ → PLAN INTERNO → WRITE → BUILD → TEST → RUN → VERIFY → CORRECT → REPEAT → FINAL AUDIT → COMMIT → PUSH`

Si una prueba falla:

`FALLO → CAUSA RAÍZ → CORRECCIÓN → REPETIR PRUEBA`

No respondas al usuario sólo para decir que encontraste un error.

No pidas permiso para una corrección local, reversible y necesaria para cumplir esta especificación.

No hagas commit de una corrección conocida como incompleta.

No cambies rutas, contratos o arquitectura sólo para hacer verde una prueba si el comportamiento del producto queda peor.

No reemplaces implementación con documentación.

No reemplaces requisitos con un contador de requests.

No declares una capacidad física, de proveedor o hardware como probada sin evidencia real.

---

# 1. COSAS YA ACEPTADAS — NO REHACER

Conserva y protege lo que ya quedó correctamente implementado.

## 1.1 Routing y smoke

Se acepta la separación:

- MVC: `/operations/customers`
- API GET: `/api/operations/customers/list`
- API POST: `/api/operations/customers`
- `app.MapControllers()`
- HTTPS redirection deshabilitado únicamente en environment `Testing` para el listener HTTP loopback del E2E.

No vuelvas a mover estas rutas salvo que una prueba objetiva demuestre un conflicto real.

## 1.2 Smoke E2E

Se acepta `OperationalClosureSmokeTests` como smoke real API + proceso web + Playwright + MySQL.

Debe seguir pasando.

**Pero 30 requests NO equivalen a 30 requisitos funcionales.**

El smoke verifica recorrido/integración. Cada capacidad pendiente requiere pruebas semánticas propias.

## 1.3 Seguridad/control ya corregidos

No rehagas:

- credenciales scoped por device/site/driver;
- rechazo de credencial faltante o ambigua;
- PATCH MikroTik limitado a la propiedad solicitada;
- separación `IDeviceDriver` observación vs `IDeviceControlDriver` control;
- `ProvisioningStatus.Unsupported` cuando sólo cambia estado de negocio;
- laboratorio físico opt-in.

Regresión en cualquiera de estos puntos = fallo de cierre.

---

# 2. DEFINICIÓN EXACTA DE TERMINADO

Al finalizar, `docs/PRODUCT_TRUTH_MATRIX.md` debe ser consecuencia de la evidencia, no una lista de deseos.

Para cualquier requisito realizable enteramente en software local:

- no puede quedar `NO IMPLEMENTADO`;
- no puede quedar `PARCIAL` por trabajo interno pendiente;
- puede quedar `PARTIAL`, `UNSUPPORTED` o `NOT PROVEN` **únicamente** si la parte faltante depende objetivamente de hardware real, API/vendor no implementado deliberadamente o evidencia física externa.

Estados permitidos con significado estricto:

- `IMPLEMENTED`: código completo para el alcance declarado.
- `TESTED`: pruebas automáticas semánticas pasan.
- `INTEGRATION_TESTED`: integración real local verificada.
- `E2E_TESTED`: flujo real proceso/API/UI/DB según corresponda.
- `SIMULATED`: sólo simulador; jamás implica hardware.
- `UNSUPPORTED`: deliberadamente no soportado y UI/API lo dicen claramente.
- `NOT_PROVEN`: falta evidencia externa/real.

Regla:

`NO EVIDENCE = NOT PROVEN`

`PERSISTED ≠ APPLIED TO NETWORK`

`SIMULATED ≠ REAL DEVICE`

`HTTP 200 ≠ REQUIREMENT VERIFIED`

---

# 3. BLOQUE CRÍTICO A — NETWORK ACTION ORCHESTRATOR

El flujo de control debe quedar como una operación coherente, no métodos aislados.

Implementar/terminar:

`Preview → Authorization → Capability → Credential → Impact → Risk → Confirmation → Execute → Audit → Refresh → PostVerify`

## 3.1 Preview

`INetworkActionService.PreviewAsync` debe usar datos reales del modelo y servicios existentes.

PROHIBIDO devolver impacto hardcodeado como:

`NetworkImpactResult(0,0,0,false,...)`

cuando exista información topológica para calcularlo.

El preview debe incluir como mínimo:

- DeviceId;
- dispositivo/hostname identificable;
- acción;
- interface/target si aplica;
- capability soportada/no soportada;
- driver que ejecutaría;
- credential scope resuelto sin revelar secretos;
- riesgo;
- impacto calculado;
- evidencia incompleta explícita;
- si requiere confirmación;
- token/nonce de confirmación cuando corresponda;
- expiración corta del token;
- texto humano de consecuencia esperada.

## 3.2 Riesgo

Clasificación mínima:

- Low: operación no disruptiva.
- Medium: puede afectar un endpoint/interfaz/cliente.
- High: puede afectar múltiples clientes, uplink, AP/sector/backhaul/core o reboot.

No clasifiques reboot o disable de interfaz con downstream como Low.

## 3.3 Confirmación

Medium/High requieren confirmación verificable ligada al preview.

El token debe quedar ligado al menos a:

- actor;
- device;
- action;
- target/interface;
- preview/impact relevante;
- expiración.

No aceptar un `Confirmed=true` genérico como sustituto.

Un token de otro device/action debe fallar.

Token vencido debe fallar.

Token reutilizado después de ejecución debe fallar si el diseño es single-use.

## 3.4 Ejecución

Antes de ejecutar revalidar:

- autorización;
- capability;
- credential scope;
- confirmation token;
- estado mínimo necesario.

Después:

- registrar resultado;
- refrescar observación cuando sea posible;
- post-verificar efecto cuando el driver/simulador permita observarlo;
- si no se puede observar, devolver `NOT VERIFIED`, no inventar éxito físico.

## 3.5 Auditoría

Cada acción debe dejar trazabilidad suficiente:

- correlation/action id;
- timestamp;
- actor;
- role/auth type;
- device;
- driver;
- credential reference/id **sin secreto**;
- action;
- target/interface;
- risk;
- impact snapshot;
- pre-state cuando observable;
- result;
- post-state cuando observable;
- verification status;
- error seguro/sanitizado.

No guardar password/token/community/string sensible.

---

# 4. BLOQUE CRÍTICO B — IMPACTO TOPOLÓGICO REAL

`NetworkImpactService` no puede considerar el grafo completo “complete” porque exista un link confirmado en otra parte de la base.

El cálculo debe estar scoped al dispositivo objetivo.

Implementar traversal downstream con prevención de ciclos.

Debe distinguir:

- enlaces confirmados;
- enlaces stale;
- enlaces ambiguos/no confiables;
- ausencia de evidencia.

Calcular cuando los datos existan:

- downstream devices;
- customer services afectados;
- sites/sector/AP relacionados;
- si el objetivo parece uplink/backhaul/core;
- si el impacto es completo o parcial;
- por qué es parcial.

No inventar relación de cliente si no existe evidencia persistida.

Pruebas obligatorias:

1. cadena A→B→C;
2. árbol A→B/C→D/E;
3. ciclo A→B→C→A sin loop infinito;
4. link stale excluido o degradado explícitamente;
5. grafo no relacionado no vuelve `Complete=true` al target;
6. servicio downstream contado una sola vez;
7. target aislado = impacto desconocido/parcial, no cero “seguro”.

---

# 5. BLOQUE CRÍTICO C — DIAGNÓSTICO POR CAMINO DEL CLIENTE

Eliminar diagnósticos contaminados por alertas/incidentes globales no relacionados.

La correlación objetivo es:

`Customer → Service → Router/CPE → WirelessAssociation/Access → AP → Sector → Site → Backhaul → Core`

Usa sólo relaciones que existan realmente en el modelo.

Cuando falte un tramo, devolver explícitamente:

`NOT DETERMINED / EVIDENCE MISSING`

No rellenar huecos por heurística débil como hechos.

Salida mínima:

- customer/service;
- path conocido ordenado;
- health/availability por tramo cuando exista;
- alertas/incidentes correlacionados al path;
- causa probable;
- causa observada si existe evidencia directa;
- confidence/evidence;
- tramo donde se perdió certeza;
- root incident relacionado si existe.

Pruebas:

- alerta ajena no afecta diagnóstico;
- AP compartido caído afecta clientes realmente dependientes;
- cliente aislado no crea falsa causa raíz compartida;
- path incompleto no se presenta como completo.

---

# 6. BILLING — HACERLO CONSISTENTE, NO SÓLO CRUD

## 6.1 Vencimiento y gracia

Conservar 3 días de gracia como default configurable.

No basta con encontrar cualquier charge viejo vencido si ya fue cubierto económicamente.

La decisión de mora debe representar deuda vencida real, no sólo existencia histórica de un cargo.

Agregar pruebas con:

- cargo futuro;
- cargo vencido dentro de gracia;
- vencido fuera de gracia;
- cargo vencido completamente pagado;
- pagos parciales;
- múltiples periodos.

## 6.2 Estados de pago

Crear resultado/estado explícito neutral de proveedor:

- Pending;
- Confirmed;
- Rejected;
- Failed/Unknown si el diseño lo requiere.

`ManualPaymentProvider` no debe fingir confirmación electrónica automática.

Pago en efectivo/manual puede requerir validación autorizada.

Pago electrónico confirmado puede habilitar flujo de reconexión.

No amarres el dominio a un proveedor comercial concreto.

## 6.3 Reconnection fee

Default de negocio: **MXN $50**, configurable.

Debe poder condonarse sólo con:

- permiso apropiado;
- motivo;
- auditoría.

No cobrar fee si no corresponde según política/estado.

## 6.4 PayAndReconnect

Separar:

`PAYMENT CONFIRMED` de `BUSINESS RECONNECTED` de `NETWORK PROVISIONING APPLIED`.

No devolver un resultado que haga creer que la red fue reconectada si sólo cambió MySQL.

Flujo esperado:

1. validar servicio/cuenta;
2. procesar/validar pago;
3. registrar ledger/receipt;
4. resolver deuda vencida;
5. aplicar fee cuando corresponda;
6. cambiar estado de negocio;
7. solicitar provisioning sólo si existe capability real;
8. guardar provisioning status;
9. verificar red cuando sea observable.

Sin adapter real:

`BusinessStatus=Active/Reconnected`

pero

`ProvisioningStatus=Unsupported/NotApplied`

según corresponda.

## 6.5 Promesas y pagos parciales

Mantener asignación por remanente, pero persistir trazabilidad de qué cantidad de cada pago cubrió qué promesa si el modelo lo permite sin romper arquitectura.

Nunca perder remanente.

Nunca marcar una promesa totalmente pagada con pago insuficiente.

Pruebas con 2+ promesas y pago que cruza ambas.

## 6.6 Prepago

No dejar bonus sólo en texto de descripción.

Persistir vigencia/cobertura del servicio derivada del prepago.

Política default:

- 3 meses → +5 días;
- 6 meses → +10 días;
- 9 meses → +15 días;
- 12 meses → +1 mes.

Debe existir una fecha/periodo verificable resultante.

Evitar duplicar/extender incorrectamente si se registra dos veces la misma operación.

## 6.7 Change Plan

Antes de aceptar upgrade, evaluar capacidad cuando exista evidencia de ruta/capacidad.

Si no existe evidencia suficiente:

- no afirmar “capacity OK”;
- devolver `RequiresValidation`/equivalente.

Calcular prorrateo real según política definida y fechas, no sólo `MonthlyDifference`.

Separar cambio comercial de provisioning de red.

Rollback transaccional del cambio comercial si una operación local obligatoria falla.

---

# 7. CRÉDITOS POR CAÍDA — NO ACEPTAR MONTO ARBITRARIO COMO “EVIDENCIA”

Crear `OutageCreditPreview` o equivalente.

La sugerencia debe derivarse de:

- customer/service;
- incidente atribuible al proveedor;
- ventana temporal;
- duración válida;
- plan/tarifa o política de crédito;
- evidencia de afectación del servicio.

No basta con recibir `SuggestedAmount` del cliente API y persistirlo.

El operador puede ajustar si el negocio lo permite, pero debe quedar:

- sugerencia calculada;
- monto final;
- actor;
- razón de override;
- aprobación requerida según rol.

Un incidente sin relación demostrable con el servicio no debe producir crédito automático como válido.

---

# 8. WISP — CORRELACIÓN OPERACIONAL REAL

`IWispOperationsService.ListOperationalClientsAsync` no puede seguir siendo sólo una lista de snapshots de devices.

Crear DTO operacional correlacionado con los datos disponibles:

- CustomerId;
- ServiceId;
- service code;
- CPE/router/device id cuando exista;
- MAC/IP;
- AP;
- sector;
- site;
- association evidence;
- signal RSSI/SNR cuando exista;
- state;
- provisioning state;
- unauthorized/fraud-review state;
- path completeness/evidence.

No inventar sector/site cuando no existe relación.

## 8.1 CPE nueva

Nueva MAC/CPE detectada bajo AP:

`NO AUTORIZADA`

Debe conservar evidencia observada:

- AP;
- MAC;
- IP;
- vendor si existe;
- RSSI/SNR;
- first seen;
- source/evidence.

## 8.2 ACCEPT

Autorizar debe:

- requerir Service válido;
- persistir decisión;
- asociar equipo/servicio según modelo;
- intentar enforcement/provisioning sólo si existe driver/capability real;
- reflejar `Unsupported` si no existe.

## 8.3 REJECT / EXPULSAR

Misma regla: no afirmar bloqueo físico si sólo se persistió decisión.

## 8.4 Reemplazo de CPE

Preservar Customer/Service lógico.

Registrar equipo anterior/nuevo y auditoría.

No crear un cliente nuevo para reemplazo de hardware.

---

# 9. FRAUD EVIDENCE ENGINE

`FraudReview` como enum/estado no es un motor de evidencia.

Implementar detección **explicable** de señales, no culpabilidad automática.

Ejemplos de evidencia válida si los datos existen:

- misma identidad lógica apareciendo en AP/sector inesperado;
- MAC/equipo asociado a servicio distinto;
- movimiento incompatible con ubicación registrada;
- asociación simultánea contradictoria;
- cambio abrupto de CPE sin workflow de reemplazo.

Salida:

- `PossibleFraud`/review flag;
- reglas disparadas;
- evidencia;
- timestamp;
- confidence prudente.

PROHIBIDO suspender o acusar automáticamente sólo por una heurística.

Pruebas de falso positivo obligatorias.

---

# 10. COVERAGE Y CAPACITY

Coverage no debe reducirse a Address string + último estado manual.

Sin construir GIS completo, ampliar el modelo/software local para soportar cuando esté disponible:

- normalized address;
- latitude/longitude opcionales;
- zone/site opcional;
- technology;
- nearby infrastructure/evidence;
- capacity evidence;
- checkedAt/source.

Estados:

- Available;
- ProbablyAvailable;
- RequiresFieldValidation;
- NoCoverageConfirmed;
- CapacityLimited;
- Saturated;
- Planned.

Si faltan coordenadas/evidencia, ser conservador.

No devolver `CapacityLimited` simplemente porque coverage no era available; diferenciar causa.

Pruebas de cada estado relevante.

---

# 11. ROUTE PLANNER

El planner no puede ser sólo ordenar por horario y sumar minutos.

Usar, cuando existan:

- priority;
- zone;
- coordinates;
- appointment windows;
- estimated work duration;
- travel estimate;
- working hours;
- buffer;
- root incident/dependency;
- required equipment si está modelado.

No necesitas Google Maps ni API pagada.

Para distancia local usa una función determinista (p. ej. Haversine) cuando haya coordenadas.

Cuando no haya coordenadas, degradar explícitamente a zone/time ordering.

Estimaciones iniciales:

- home diagnostic: 45 min;
- router: 30;
- CPE: 45;
- alignment: 60;
- RJ45/RJ11: 45;
- WISP install: 90;
- complex: 120;
- existing fiber/ONT: 120;
- new fiber: 180–240;
- AP/site: 90;
- backhaul: 90;
- buffer: 10–20%.

Pruebas:

- prioridades;
- ventanas;
- dos zonas;
- coords presentes;
- coords ausentes;
- retraso que hace inviable una visita;
- root incident agrupa/reordena razonablemente si aplica.

---

# 12. CONFIGURATION BACKUP / COMPARE / RESTORE TRUTH

`ConfigurationRevision` con hashes no equivale a backup restaurable.

Separar claramente:

- metadata revision;
- captured configuration artifact;
- compare;
- known-good;
- restore capability;
- restore execution;
- restore verification.

Si un driver no soporta captura/restore:

`UNSUPPORTED`

No mostrar botón Restore operativo para ese device.

Para simulator sí puedes implementar round-trip real simulado:

`capture → change → compare → restore → verify`

Eso debe quedar `SIMULATED/E2E_TESTED`, no `REAL DEVICE PROVEN`.

Para MikroTik sólo promover lo que realmente implemente y pueda verificar sin hardware; de lo contrario conservar `NOT PROVEN`/`UNSUPPORTED` apropiado.

---

# 13. SIMULADOR — DEBE SERVIR PARA PROBAR SEMÁNTICA

`SimulatedDeviceControlDriver` no debe anunciar acciones que no modela correctamente.

Debe mantener estado observable suficiente para probar:

- interface enable;
- interface disable;
- description/comment;
- reboot lifecycle;
- failure;
- timeout;
- unsupported action;
- post-verification;
- opcionalmente backup/restore simulado.

Una acción soportada debe producir un cambio observable consistente.

Una no soportada debe devolver Unsupported, no Success vacío.

Agregar escenarios de fallo deterministas configurables para tests.

---

# 14. AUTORIZACIÓN — CERRAR EL CAMBIO DE `ApiScopeAuthorizationHandler`

Actualmente un principal humano por cookie puede satisfacer `Api.TopologyRead` si tiene uno de varios roles.

Esto puede ser válido para el panel, pero debe quedar **deliberado, consistente y probado**.

Primero corrige el comentario XML/documentación del handler para que describa el comportamiento real.

Luego prueba matriz de acceso mínima:

- anonymous;
- ReadOnly;
- Support;
- NocOperator;
- Administrator;
- API key topology read;
- API key sin scope;
- API key wildcard.

Verificar explícitamente:

- quién puede GET customer/billing/assets/credits;
- quién puede POST cada mutación sensible;
- ReadOnly jamás muta;
- Support no ejecuta acciones de red administrativas;
- API key de lectura jamás obtiene escritura sólo por estar autenticada.

Si alguna API expone información más sensible de lo necesario a ReadOnly, aplicar policy/role específica; no abrirla sólo para que el E2E pase.

---

# 15. UI OPERACIONAL — EL GRAFO NO ES DECORACIÓN

Cerrar el flujo mínimo desde UI para una acción soportada/simulada:

`Graph node → Actions → capability → Preview → impact/risk → confirmation → Execute → result → refreshed state/audit`

La UI debe:

- ocultar/deshabilitar acciones no soportadas;
- mostrar SIMULATED/REAL/UNSUPPORTED/NOT PROVEN claramente;
- mostrar impacto incompleto;
- pedir confirmación Medium/High;
- no permitir ejecución sin token válido;
- mostrar resultado y verification status;
- enlazar al audit/evento cuando sea posible.

No hace falta rediseñar toda la interfaz.

Haz el cambio mínimo limpio que convierta el grafo en cabina operativa.

Prueba E2E de este flujo con simulator.

---

# 16. ROOT INCIDENTS Y CORRELACIÓN

Conservar el arreglo previo: una caída aislada no debe inventar root cause/downstream.

Agregar/confirmar pruebas:

- una sola CPE caída = incidente aislado;
- AP compartido con múltiples downstream afectados = candidato a root incident;
- recuperación del root actualiza/correlaciona downstream sin borrar historia;
- evento no relacionado no se agrega al root;
- root cause candidate no equivale a confirmed root cause sin evidencia suficiente.

---

# 17. SEGURIDAD Y CONSISTENCIA TRANSACCIONAL

Auditar los nuevos endpoints y servicios modificados durante este cierre.

Obligatorio:

- validation de IDs y ownership;
- no IDOR entre customer/service/account/asset;
- roles/policies correctos;
- no secretos en response/log/audit;
- no mass assignment peligroso;
- montos > 0;
- fechas coherentes;
- enums inválidos rechazados;
- concurrency donde una doble operación financiera/control pueda duplicar efecto;
- operaciones multi-entidad críticas dentro de transacción cuando corresponda;
- idempotencia o protección contra doble submit en pagos/provisioning/control de alto impacto cuando sea razonable.

No introduzcas un framework gigante; resuelve riesgos concretos.

---

# 18. MIGRACIONES MYSQL

Toda entidad/campo nuevo persistente requiere migración real EF Core.

Validar dos caminos:

## Clean

BD vacía → aplicar todas migraciones → aplicación arranca.

## Upgrade

BD en baseline anterior razonable → aplicar nuevas migraciones → datos existentes conservados.

No usar `EnsureCreated` como sustituto de migrations en producción.

El E2E puede recrear su base dedicada como ya hace.

Agregar índices/constraints para invariantes importantes cuando corresponda.

---

# 19. PRUEBAS OBLIGATORIAS

No persigas un número arbitrario de tests. Persigue evidencia.

Como mínimo deben existir pruebas semánticas para los bloques modificados:

1. credential scope regresión;
2. MikroTik request body regresión;
3. action preview real;
4. confirmation token válido;
5. token equivocado/vencido;
6. impact multihop;
7. impact cycles;
8. unrelated topology isolation;
9. customer diagnostic path;
10. unrelated alerts isolation;
11. billing grace;
12. paid overdue charge;
13. partial payments/promises;
14. payment provider states;
15. reconnection fee/waiver audit;
16. PayAndReconnect business vs provisioning truth;
17. prepayment validity;
18. plan change capacity/proration;
19. outage credit evidence;
20. WISP correlated client DTO;
21. unauthorized CPE;
22. CPE replacement;
23. fraud evidence + false positive;
24. coverage states;
25. route planning coordinates/fallback;
26. simulator action state;
27. simulator failure/timeout;
28. backup/restore simulator roundtrip if implemented;
29. authorization matrix;
30. UI action flow E2E;
31. existing operational smoke 30/30;
32. MySQL clean migration;
33. MySQL upgrade migration where feasible;
34. existing unit/integration/runtime/E2E suites.

Si una prueba sólo hace `Assert.True(true)` o sólo cuenta requests, NO sirve como evidencia del requisito.

---

# 20. SMOKE FINAL — 30 CHECKPOINTS DE PRODUCTO, NO 30 REQUESTS

Mantén el smoke existente, pero el cierre final debe verificar además estos **30 checkpoints funcionales**. Pueden distribuirse entre tests; no tienen que vivir en un único método.

1. setup/login;
2. discovery/snapshot accesible;
3. customer creado;
4. service creado;
5. plan asociado;
6. CPE unauthorized detectada;
7. CPE asociada a service;
8. provisioning truth visible;
9. coverage evaluada;
10. asset asignado;
11. billing charge con due date;
12. grace correcta;
13. payment parcial;
14. promise allocation correcta;
15. prepago produce vigencia;
16. suspensión comercial correcta;
17. reconexión separa business/network provisioning;
18. ticket/support interaction;
19. visit/route plan;
20. root incident correlation;
21. diagnostic path scoped;
22. outage credit derivado de evidencia;
23. topology impact multihop;
24. network action preview;
25. risk + confirmation token;
26. simulated control execution;
27. post-verification;
28. audit completo;
29. UI graph action flow;
30. product truth matrix coincide con evidencia.

Todos los checkpoints puramente locales deben quedar verdes.

Los que dependan de hardware real se validan con simulator y se etiquetan como tales; **NO ejecutes laboratorio físico**.

---

# 21. PROHIBICIONES

Durante este cierre NO:

- ejecutes laboratorio físico;
- uses un router/PLC/AP real automáticamente;
- inventes credenciales;
- hagas SNMP SET genérico;
- conviertas Cisco/Generic SNMP en control sólo para llenar matriz;
- afirmes provisioning físico donde sólo cambió DB;
- declares restore real donde sólo existe hash;
- declares fraude como hecho por heurística;
- inventes cobertura/capacidad;
- uses APIs pagadas para routing;
- cambies arquitectura sana sin necesidad;
- borres historia financiera/incidentes/auditoría para simplificar tests;
- desactives seguridad para hacer verde E2E;
- reduzcas asserts para ocultar fallos;
- marques test Skip para esconder una regresión que sí puede correr localmente;
- edites la truth matrix a READY antes de tener evidencia.

---

# 22. DISCIPLINA DE DECISIÓN

Cuando encuentres dos posibles soluciones, elige por este orden:

1. verdad del producto;
2. seguridad;
3. integridad de datos;
4. comportamiento observable correcto;
5. compatibilidad con arquitectura existente;
6. simplicidad;
7. menor diff razonable.

No elijas “la que hace pasar el test” si contradice cualquiera de las anteriores.

Si el test está mal, corrige el test **y** conserva el contrato correcto.

Si código y test discrepan, determina primero el requisito de producto.

No cambies producción para satisfacer una expectativa accidental del test.

---

# 23. AUDITORÍA HOSTIL FINAL OBLIGATORIA

Sólo después de implementar todo, haz una revisión hostil del diff acumulado desde:

`f8dfeba1e3ce1178f566ef44942fa68ec20d21b9`

hasta HEAD local.

Busca expresamente:

- fake capabilities;
- `TODO/FIXME/NotImplementedException` relevantes;
- hardcodes de impacto/capacidad/éxito;
- `catch {}` silenciosos;
- secretos;
- rutas duplicadas;
- policies debilitadas;
- acciones sin audit;
- business state presentado como network state;
- pruebas tautológicas;
- endpoints mutables sin autorización;
- relaciones customer/service sin ownership validation;
- pagos duplicables;
- migraciones faltantes;
- N+1 obvios en flujos operacionales críticos;
- regresiones en discovery/topology/alerts/incidents.

Corrige los hallazgos corregibles antes de continuar.

No escribas otro documento de “cosas pendientes” para pasárselas al usuario.

**TÚ eres quien debe cerrar esas cosas pendientes.**

---

# 24. SECUENCIA FINAL DE VALIDACIÓN

Ejecuta en este orden:

1. restore;
2. build Release;
3. unit tests;
4. integration tests;
5. runtime tests;
6. E2E tests;
7. operational smoke;
8. authorization/security tests;
9. MySQL migration clean;
10. MySQL upgrade validation;
11. auditoría estática final;
12. revisar `git diff`;
13. revisar `git status`;
14. actualizar `PRODUCT_TRUTH_MATRIX.md` **sólo con evidencia real**;
15. repetir build/tests afectados si la matriz/documentación no toca código, y suite completa si cualquier corrección final sí toca código.

Cero errores de compilación.

No aceptes warnings nuevos evitables introducidos por este trabajo.

No ocultes suites omitidas: reporta exactamente qué corrió y qué no.

---

# 25. COMMIT Y PUSH

Sólo cuando el cierre local esté verde:

- crea commit final coherente;
- push a `origin/codex/atlasnoc-review-20260909`;
- verifica que el HEAD remoto sea exactamente el commit creado.

Si Git/Codex solicita confirmación explícita del remoto, el destino autorizado para esta tarea es:

`https://github.com/akamike17/AtlasNOC.git`

rama:

`codex/atlasnoc-review-20260909`

No cambies de rama.

No hagas force push.

No mezcles archivos ajenos/no relacionados.

---

# 26. ÚNICO REPORTE PERMITIDO AL TERMINAR

No envíes reportes intermedios.

Al final entrega **un solo reporte compacto** con exactamente:

- HEAD SHA;
- commit message;
- push confirmado sí/no;
- archivos principales modificados;
- Build PASS/FAIL;
- Unit X/X;
- Integration X/X;
- Runtime X/X;
- E2E X/X;
- operational smoke PASS/FAIL;
- MySQL clean migration PASS/FAIL;
- MySQL upgrade PASS/FAIL/NOT RUN con razón;
- matriz de requisitos: IMPLEMENTED/TESTED/SIMULATED/UNSUPPORTED/NOT PROVEN;
- laboratorio físico: `NOT RUN`;
- hallazgos residuales **únicamente** si dependen objetivamente de hardware/vendor externo o son riesgos no corregibles en este alcance.

No declares “terminado” si quedan `PARCIAL` o `NO IMPLEMENTADO` que puedas resolver localmente.

---

# 27. CRITERIO DE RECHAZO

El cierre será rechazado si ocurre cualquiera:

- sólo actualizas la matriz;
- sólo agregas tests sin implementar comportamiento;
- sólo haces CRUD;
- el grafo sigue sin flujo operacional de acción;
- `Preview` devuelve impacto ficticio/hardcodeado;
- `PayAndReconnect` dice reconectado cuando sólo cambió MySQL;
- WISP sigue siendo snapshots sin correlación customer/service/CPE/AP;
- crédito sigue aceptando monto arbitrario como evidencia;
- prepago sigue siendo texto sin vigencia;
- route planner sigue siendo únicamente sort por fecha;
- fraud review sigue siendo sólo un enum;
- backup sigue siendo sólo hashes y se presenta como restore;
- autorización se debilita para hacer pasar E2E;
- smoke 30/30 es usado como sustituto de pruebas semánticas;
- hardware no ejecutado se declara probado;
- te detienes a mitad para preguntar qué hacer después.

---

# 28. FRASE DE CONTROL

Antes de declarar terminado, responde internamente estas preguntas:

**¿AtlasNOC puede explicar qué sabe, qué no sabe, qué puede controlar, qué sólo simuló y qué efecto realmente verificó?**

**¿Un operador puede pasar del grafo a una acción soportada, previsualizar impacto, confirmar, ejecutar y comprobar el resultado sin usar Postman ni editar la base?**

**¿Billing distingue dinero registrado, pago confirmado, estado comercial y provisioning de red?**

**¿WISP correlaciona cliente/servicio/equipo/acceso con evidencia en vez de sólo mostrar dispositivos?**

**¿Cada READY/IMPLEMENTED de la matriz tiene una prueba que verifica semántica y no sólo HTTP 200?**

Si alguna respuesta es NO y puede resolverse localmente, **SIGUE TRABAJANDO**.

---

# 29. ORDEN FINAL AL AGENTE

Empieza desde `f8dfeba`.

Lee primero la implementación existente y `docs/PRODUCT_TRUTH_MATRIX.md`.

Construye una lista interna de las filas `PARCIAL`/`NO IMPLEMENTADO`.

Corrígelas en el orden de este documento.

Después de cada bloque ejecuta sólo las pruebas necesarias para iterar rápido, pero **NO REPORTES**.

Al terminar todos los bloques ejecuta la validación completa.

Corrige cualquier regresión.

Haz auditoría hostil.

Actualiza truth matrix.

Commit.

Push.

Verifica HEAD remoto.

Entrega únicamente el reporte final.

## NO TE DETENGAS EN EL PRIMER ERROR.
## NO TE DETENGAS DESPUÉS DE UN BLOQUE VERDE.
## NO PIDAS AL USUARIO QUE TOME DECISIONES TÉCNICAS QUE PUEDAS RESOLVER CON EL CONTRATO EXISTENTE.
## NO INVENTES CAPACIDADES PARA CERRAR LA MATRIZ.
## NO HAGAS TRAMPA A LAS PRUEBAS.

### SI UNA FILA CAMBIA EN MYSQL PERO ATLASNOC AFIRMA QUE CAMBIÓ LA RED, LA TAREA NO ESTÁ TERMINADA.

### SI EL GRAFO SÓLO SE VE BONITO PERO NO LLEVA A OPERACIONES REALES/SIMULADAS VERIFICABLES, LA TAREA NO ESTÁ TERMINADA.

### SI QUEDA TRABAJO LOCAL REALIZABLE MARCADO PARCIAL O NO IMPLEMENTADO, LA TAREA NO ESTÁ TERMINADA.
