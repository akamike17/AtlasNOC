# ATLASNOC --- CORRECCIÓN TOTAL CONTINUA

## ORDEN

HEAD revisado: `4c863c7f75d07a9eb6bd09772cac93139af5cc8a`, rama
`atlasnoc`.

Esto NO es una auditoría documental. Es una **corrección total continua
del producto dentro del alcance existente**.

**Continúa sin detenerte en cada hallazgo.** - Si algo está PARTIAL:
termínalo. - Si está BROKEN: arréglalo. - Si la UI existe pero no
funciona: conéctala. - Si backend existe pero falta vista/flujo:
impleméntalo. - Si falta backend necesario dentro del alcance:
impleméntalo. - Si falta prueba: créala. - Si falla una prueba: causa
raíz → corrección → repetición. - Si falta empty/error/loading state:
impleméntalo. - Si una acción visible no hace nada: impleméntala. - Sólo
deja UNSUPPORTED/NOT PROVEN cuando dependa realmente de
hardware/protocolo externo no disponible. - No preguntes si debes
continuar. La respuesta es SÍ.

## NO ROMPER LO VALIDADO

Conserva fix FK Customer/BillingAccount, seguridad auth/roles/scopes/API
keys, contrato `label/ip`, renderer compartido, assets locales
Cytoscape/Chart.js y suites verdes. No simulación en modo normal.

## HALLAZGO DE REVISIÓN

Tu `docs/UI_VIEWS_AUDIT.md` todavía declara PARTIAL: Dashboard,
Interfaces, Links, AlertRules, Integrations, Subscribers, System y
Users.

**No puedes cerrar dejando esas filas PARTIAL si son implementables
localmente.**

Además, las filas READY deben revalidarse con flujo real; cobertura
"indirecta" no basta para declarar READY.

## DASHBOARD

Llévalo a READY: health real, conteos coherentes, topología visible,
nodos aislados visibles, selección/navegación, empty/error, assets
locales y E2E dedicado.

Existe discrepancia: el usuario vio 6 dispositivos y tu consulta de
`atlasnoc_rebuild` encontró 0. Investiga la configuración efectiva de la
instancia visible: appsettings, environment, user-secrets sin revelar
secretos, launch profile/proceso y DB efectiva. Reporta
servidor/base/origen con password redactado. Determina si era otra DB,
otra instancia, datos antiguos o contador defectuoso. Corrige si es
defecto.

## INTERFACES

Lleva a READY: listado por dispositivo, detail,
MAC/IP/name/status/speed/link count, empty/not-found, navegación
Device→Interfaces→Detail, auth y E2E dedicado.

## LINKS

Lleva a READY: listado/detail, endpoints legibles,
evidencia/protocolo/confianza, manual link sólo válido, confirm/reject,
stale/manual/confirmed, errores, auth y E2E. Nunca fabriques relaciones.

## ALERT RULES

Lleva a READY: list/create/edit/toggle si el dominio lo soporta,
validaciones, persistencia, empty/error, auth y E2E.

## INTEGRATIONS

Lleva a READY la gestión/configuración que sí es implementable. Drivers
físicos no probados permanecen honestamente Unsupported/Not proven.
Prueba listado, estado, capacidades, configuración, validación y
errores.

## SUBSCRIBERS

Lleva a READY: list/create/detail, endpoints asociados, navegación,
empty/not-found, validación, auth y E2E.

## SYSTEM

Lleva a READY: DB health, Web/Worker si hay fuente real, conteos,
timestamp, degradación/error y E2E. No mostrar OK si falla el servicio
real.

## USERS

Lleva a READY con cuidado: list/create/edit/roles/password
reset/active-inactive si existe, validación, antiforgery, autorización y
E2E por rol. Nunca debilites `[Authorize]`.

## REAUDITAR READY

Revalida Devices, Discovery, Topology, Metrics, Alerts, Incidents,
Sites, Credentials, ApiKeys, Operations, Audit y Setup: happy path,
empty, invalid input, not-found, auth, navegador, console/network. Si
algo falla, corrígelo antes de seguir.

## OPERATIONS / SERVICE CREDIT

Audita el nuevo `ServiceCreditCalculator` y `credits/preview`: fechas,
UTC, tarifa 0, topes y especialmente relación cliente↔incidente. Si hoy
puede sugerir crédito para un cliente no afectado simplemente porque el
incidente existe y está resuelto, corrígelo usando evidencia/relación
disponible; no inventes relación.

## TOPOLOGÍA FINAL

Asegura `deviceType`, vendor, status, siteId, groups/site filter,
unlinked count, refresh, confirmed/unconfirmed, navegación, 0 nodes, 0
edges con nodes, invalid edge descartado, offline assets y sin CSP
errors. Agrega prueba explícita: **6 devices + 0 links = 6 nodos
visibles**.

## TESTS

Agrega E2E dedicados a las 8 áreas PARTIAL. Cada flujo verifica HTTP +
UI + persistencia cuando escribe + recarga + error/validación +
autorización.

Luego: `dotnet restore` `dotnet build AtlasNOC.sln -c Release`
`dotnet test AtlasNOC.sln -c Release --no-build`

Ejecuta Unit, Integration, Runtime y E2E explícitamente. Objetivo: 0
Failed; 0 Skipped en requisitos ejecutables localmente. Skips sólo por
dependencia externa genuina.

Revisa también los 8 CS1998: corrige si es seguro/trivial o documenta
razón concreta; no descartarlos sólo por ser preexistentes.

## BROWSER

Playwright debe recorrer TODAS las vistas navegables para el rol
correcto y registrar page errors, console errors, requestfailed, 4xx/5xx
inesperados, links rotos, formularios y botones sin acción. Corrige y
repite ante cualquier fallo.

## SEGURIDAD

Al final audita `[Authorize]`, policies, ApiScopeAuthorizationHandler,
scopes API key, antiforgery MVC, roles, secretos y CSP. Ninguna
corrección funcional justifica bypass.

## DOCUMENTACIÓN

Actualiza `UI_VIEWS_AUDIT.md` sólo DESPUÉS de implementar/probar.
PARTIAL sólo puede permanecer con evidencia concreta de dependencia
externa o capacidad realmente fuera del alcance.

## BUCLE OBLIGATORIO

`AUDITAR → GAP → IMPLEMENTAR → BUILD → TEST → BROWSER → REAUDITAR`

Repite hasta que no queden gaps implementables. **No detenerse tras una
iteración, no pedir permiso, no usar otro informe como sustituto del
código.**

## LÍMITES REALES

No fingir hardware físico, Ubiquiti genérico sin driver, GPON/DSL, WISP
físico no demostrado, restore destructivo real ni acciones peligrosas
sin autorización. Eso sí puede quedar NOT PROVEN/UNSUPPORTED. Todo lo
demás implementable continúa.

## GIT

**NO commit / NO push / NO merge / NO force.** Al final deja cambios
para revisión y entrega: `git status --short` `git diff --stat`
`git diff --name-only`

## RESPUESTA FINAL

Sólo cuando no existan gaps implementables: root causes, áreas
corregidas, archivos, suites, E2E por vista, navegador, seguridad,
resolución de los 6 dispositivos, PARTIAL restantes con prueba del
bloqueo y diff sin commit.

VERDICT permitido: - `READY FOR HUMAN REVIEW` si todo lo implementable
terminó y fue probado. - Si no: **CONTINUE WORKING** y sigue ejecutando;
no finalices.

**Esta orden reemplaza "auditar y reportar": si está a medias,
termínalo; si falta dentro del alcance, impleméntalo; si está roto,
corrígelo; si no tiene prueba, agrégala; vuelve a recorrer el producto
hasta que no queden gaps implementables.**
