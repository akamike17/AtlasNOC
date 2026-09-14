# ATLASNOC — E2E RECOVERY / EXECUTION ORDER

## MODO DE TRABAJO

Esta es una orden de EJECUCIÓN, no una solicitud de análisis teórico.

Debes trabajar de principio a fin sobre el estado ACTUAL del repositorio.

NO te detengas después de:
- analizar;
- encontrar una causa probable;
- modificar un archivo;
- compilar;
- ejecutar una prueba;
- encontrar un nuevo error.

Continúa automáticamente con:

READ → VERIFY → FIX → BUILD → TEST → DIAGNOSE → FIX → RETEST

hasta alcanzar los criterios de salida definidos al final.

---

# 0. REGLAS ABSOLUTAS

1. NO hagas commit.
2. NO hagas push.
3. NO cambies de rama.
4. NO resetees cambios existentes.
5. NO borres trabajo previo válido.
6. NO hagas refactors cosméticos.
7. NO agregues features.
8. NO debilites autenticación/autorización.
9. NO cambies seguridad sólo para hacer verde una prueba.
10. NO desactives tests.
11. NO cambies asserts para ocultar errores.
12. NO uses delays/retries arbitrarios para disfrazar race conditions.
13. NO reemplaces pruebas E2E por mocks para hacerlas pasar.
14. NO reutilices una BD E2E contaminada si la prueba requiere estado limpio.
15. NO asumas que login/setup funcionó: DEMUÉSTRALO.

IMPORTANTE:

NO describas repetidamente lo que "vas a hacer".

Ejecuta.

Si sabes cuál es el siguiente comando seguro:
EJECÚTALO.

No produzcas ciclos como:

"I will run..."
"We need to run..."
"Let's run..."
"We should ensure..."
"I will now..."

Una explicación breve antes de una operación es suficiente.

Después usa las herramientas.

---

# 1. PRIMER OBJETIVO — CONGELAR EL ESTADO

Antes de modificar cualquier cosa:

Ejecuta y registra:

- ruta actual;
- repositorio;
- rama;
- HEAD SHA;
- git status;
- archivos modificados;
- archivos sin seguimiento.

NO limpies nada.

NO hagas checkout/reset/stash automáticamente.

Determina qué cambios pertenecen al trabajo actual.

---

# 2. IDENTIFICAR EXACTAMENTE EL E2E FALLIDO

Localiza:

- test E2E que está fallando;
- fixture;
- InitializeAsync;
- SetupAndLogin;
- configuración de TestServer/WebApplicationFactory;
- configuración de base de datos E2E;
- flujo /Setup;
- flujo /Account/Login;
- ApiScopeAuthorizationHandler;
- policies relacionadas;
- endpoints usados por la prueba.

Lee primero la implementación REAL.

NO modifiques nada todavía.

Construye mentalmente el flujo:

TEST
 ↓
InitializeAsync
 ↓
crear BD
 ↓
migraciones
 ↓
¿existe admin?
 ↓
/Setup
 ↓
crear admin
 ↓
/Account/Login
 ↓
cookie/token
 ↓
endpoint autenticado
 ↓
ApiScopeAuthorizationHandler
 ↓
resultado esperado

Después VERIFICA cada transición ejecutando código/pruebas.

No supongas.

---

# 3. BASE DE DATOS E2E

Determina exactamente:

- qué proveedor usa;
- dónde está físicamente;
- cómo se crea;
- cómo se inicializa;
- qué migraciones aplica;
- si persiste entre ejecuciones;
- si puede contener esquema antiguo;
- si contiene usuarios;
- si contiene administrador inicial.

Existe evidencia previa de errores relacionados con
columnas/esquema faltante.

Por tanto:

COMPRUEBA si la BD E2E actual está contaminada/desactualizada.

Si el diseño de pruebas exige una BD desechable:

1. elimina SOLAMENTE la BD E2E;
2. créala desde cero;
3. aplica TODAS las migraciones actuales;
4. comprueba que no existan migraciones pendientes.

NUNCA borres una BD que no hayas demostrado que pertenece
exclusivamente a pruebas.

---

# 4. VERIFICAR ESTADO INICIAL

Con BD E2E limpia:

Arranca la aplicación/test server.

Verifica:

- aplicación inicia;
- /health responde correctamente;
- base de datos responde;
- migraciones están aplicadas.

Después comprueba:

¿EXISTE ADMIN?

No asumas que InitializeAsync lo crea.

Si NO existe:

comprueba el comportamiento REAL de:

GET /Account/Login

Si redirige a:

/Setup

registra el status y Location.

Esto es comportamiento esperado de una instalación sin configurar
hasta demostrar lo contrario.

---

# 5. INVESTIGAR InitializeAsync

Lee InitializeAsync completo.

Determina exactamente si:

A) crea solamente la BD;
B) aplica migraciones;
C) crea usuarios;
D) crea administrador;
E) ejecuta setup;
F) configura autenticación.

NO confundas "database initialized" con
"application initialized".

Si InitializeAsync únicamente crea/aplica migraciones:

documenta esa conclusión y continúa.

---

# 6. INVESTIGAR SetupAndLogin

Éste es un sospechoso principal.

Determina si SetupAndLogin:

- asume que admin ya existe;
- llama /Setup;
- sólo rellena formulario;
- comprueba respuesta de /Setup;
- sigue redirecciones automáticamente;
- comprueba resultado de login;
- comprueba cookie/token;
- continúa aunque login haya fallado.

NO permitas que continúe silenciosamente después de un fallo.

Debe existir una prueba verificable de autenticación antes de ejecutar
tests protegidos.

---

# 7. EJECUTAR EL SETUP REAL

Si una instalación nueva requiere /Setup:

usa el flujo REAL de la aplicación.

NO insertes directamente un admin en la BD salvo que el proyecto ya
tenga oficialmente un mecanismo test-only para ello.

Ejecuta:

GET /Setup

Obtén:
- formulario;
- antiforgery token si corresponde;
- campos requeridos.

Después:

POST /Setup

con datos válidos de administrador E2E.

Comprueba explícitamente:

- status HTTP;
- redirect esperado;
- ausencia de errores;
- administrador persistido en BD.

Si falla:

NO CONTINÚES AL LOGIN.

Diagnostica → corrige → recompila → repite setup.

---

# 8. LOGIN

Sólo cuando hayas demostrado que admin existe:

GET /Account/Login

POST /Account/Login

con las credenciales E2E correctas.

Comprueba:

- status;
- redirect;
- Set-Cookie o mecanismo equivalente;
- CookieContainer/cliente conserva autenticación.

NO interpretes un 302 como login exitoso automáticamente.

Inspecciona el destino del redirect.

Un redirect a:

/Account/Login
o
/Setup

NO constituye autenticación exitosa.

---

# 9. PROBAR AUTENTICACIÓN AISLADAMENTE

ANTES de ejecutar la prueba problemática:

llama un endpoint sencillo que:

- requiera autenticación;
- no dependa de ApiScopeAuthorizationHandler si existe uno apropiado.

Resultado obligatorio:

HTTP 200

o el status esperado que DEMUESTRE una identidad autenticada.

Si devuelve:

401
403
redirect a login
redirect a setup

la autenticación NO está lista.

NO continúes al handler.

Corrige primero setup/login/session.

---

# 10. ApiScopeAuthorizationHandler

SOLAMENTE después de demostrar:

CLEAN DB
→ MIGRATIONS OK
→ SETUP OK
→ ADMIN EXISTS
→ LOGIN OK
→ AUTH SESSION EXISTS
→ BASIC AUTH ENDPOINT OK

vuelve al test que involucra ApiScopeAuthorizationHandler.

Si ahora pasa:

NO MODIFIQUES EL HANDLER.

La causa estaba en fixture/setup/authentication.

Si todavía falla:

entonces investiga el handler.

Verifica:

- claims;
- scopes;
- authentication scheme;
- policy;
- requirement;
- endpoint metadata;
- identity;
- IsAuthenticated;
- expected scope;
- actual scope.

Compara producción contra E2E.

NO agregues bypass.

NO agregues:

if (environment.IsTesting())
    context.Succeed(...)

NO otorgues scopes automáticamente.

NO conviertas 403 en 200.

La prueba debe reproducir seguridad REAL.

---

# 11. CORRECCIÓN

Realiza la corrección MÍNIMA necesaria.

Preferencia:

fixture/test infrastructure
antes que
production authentication/security

si la evidencia demuestra que el defecto está en la prueba.

Cualquier cambio de producción debe estar respaldado por una falla
reproducible de producción, no solamente por una expectativa incorrecta
del test.

Después de cada cambio:

BUILD
→ TEST ESPECÍFICO

No acumules 20 cambios antes de probar.

---

# 12. BUILD

Ejecuta build Release.

Objetivo obligatorio:

0 errores.

No introduzcas nuevas advertencias.

Si build falla:

diagnostica;
corrige;
repite.

NO continúes con E2E mientras el build esté roto.

---

# 13. PRUEBA QUIRÚRGICA

Ejecuta SOLAMENTE el E2E originalmente fallido.

Ejecuta en foreground.

CAPTURA LA SALIDA REAL.

NO lances el proceso en background si después vas a quedarte esperando
sin observarlo.

NO repitas indefinidamente el mismo comando.

Si un comando falla:

LEE SU SALIDA.

Cambia la hipótesis o corrige la causa.

No ejecutes exactamente lo mismo esperando un resultado diferente.

---

# 14. GATE DE AUTENTICACIÓN

Antes de considerar solucionado el problema deben existir evidencias de:

[ ] BD E2E limpia
[ ] migraciones completas
[ ] aplicación inicia
[ ] /health OK
[ ] /Setup accesible cuando corresponde
[ ] POST /Setup exitoso
[ ] admin persistido
[ ] POST /Account/Login exitoso
[ ] sesión/cookie/token existente
[ ] endpoint autenticado responde correctamente
[ ] test ApiScopeAuthorizationHandler pasa

Si cualquier casilla falla:

CONTINÚA INVESTIGANDO.

---

# 15. PRUEBAS DE REGRESIÓN

Cuando el test específico pase:

ejecuta el grupo relacionado con:

- authentication;
- authorization;
- setup;
- API scopes;
- E2E infrastructure.

Si pasan:

ejecuta la suite E2E relevante.

Después ejecuta la suite completa si es razonable para el proyecto.

No declares éxito sólo porque pasó una prueba aislada.

---

# 16. PROTECCIÓN CONTRA LOOPS

Si notas que estás escribiendo el mismo razonamiento por segunda vez:

DETENTE DE ESCRIBIR.

Ejecuta la siguiente acción verificable.

Si una herramienta/comando queda bloqueado:

1. identifica PID/proceso;
2. determina si sigue trabajando;
3. captura salida disponible;
4. aplica timeout razonable;
5. termina SOLAMENTE el proceso creado por esta ejecución si es necesario;
6. continúa con diagnóstico.

NO gastes contexto narrando el bloqueo repetidamente.

Máximo una explicación corta entre comandos.

---

# 17. PROHIBIDO DECLARAR ROOT CAUSE SIN PRUEBA

No digas:

"root cause is X"

hasta demostrar causalidad.

Ejemplo de prueba válida:

ANTES:
no admin
→ login redirige /Setup
→ API no autenticada
→ test falla

CORRECCIÓN:
setup crea admin
→ login crea sesión
→ API autenticada
→ mismo test pasa

Eso sí demuestra causalidad.

---

# 18. NO INVENTAR ÉXITO

No escribas:

"should pass"
"likely fixed"
"appears fixed"
"probably resolved"

como conclusión.

EJECUTA LA PRUEBA.

La única evidencia aceptable es el resultado real.

---

# 19. CRITERIOS DE SALIDA

NO TE DETENGAS hasta alcanzar uno de estos estados:

## ESTADO A — RESUELTO

- causa raíz demostrada;
- corrección mínima aplicada;
- build Release exitoso;
- test original exitoso;
- pruebas relacionadas exitosas;
- seguridad NO debilitada.

Entonces entrega informe final.

## ESTADO B — BLOQUEO REAL

Sólo puedes detenerte si existe un bloqueo externo que no pueda
resolverse desde el repositorio, por ejemplo:

- credencial externa indispensable inexistente;
- servicio externo inaccesible indispensable;
- hardware indispensable;
- permiso del sistema que no puedes obtener.

"No sé qué hacer después" NO es un bloqueo.

"Encontré otro error" NO es un bloqueo.

"El test falló" NO es un bloqueo.

"Necesito investigar" NO es un bloqueo.

Continúa.

---

# 20. INFORME FINAL

Al terminar reporta SOLAMENTE:

## ROOT CAUSE

Causa demostrada.

## FILES CHANGED

Archivo → cambio → motivo.

## DATABASE

Cómo se garantizó aislamiento y migraciones.

## SETUP

Status y evidencia de creación del admin.

## LOGIN

Status y evidencia de sesión autenticada.

## SECURITY

Confirmación de que authorization/policies/scopes NO fueron debilitados.

## TEST RESULTS

Build:
Passed:
Failed:
Skipped:

Test original:
resultado exacto.

Suite relacionada:
resultado exacto.

## REMAINING ISSUES

Sólo problemas reales comprobados.

## GIT STATUS

Cambios restantes.

NO COMMIT.
NO PUSH.

---

# ORDEN FINAL

Empieza AHORA.

Inspecciona el estado real del repositorio y continúa ejecutando.

NO vuelvas a explicar este documento.

NO produzcas un plan adicional.

NO preguntes permiso entre pasos seguros.

NO te detengas después del primer error.

READ → VERIFY → FIX → BUILD → TEST → REPEAT

hasta RESUELTO o BLOQUEO REAL.