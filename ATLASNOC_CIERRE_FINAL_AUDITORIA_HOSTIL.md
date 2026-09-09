# ATLASNOC --- CIERRE FINAL + AUDITORÍA HOSTIL

## Misión

Trabaja sobre la rama `deepseek-rebuild` de `akamike17/AtlasNOC`.

**No declares terminado AtlasNOC por compilar ni porque pasen sólo los
Unit Tests.** La misión termina únicamente cuando el sistema quede
corregido, probado de extremo a extremo con infraestructura real de
pruebas, re-auditado desde cero y sin huecos críticos/altos conocidos.

Actúa como si después de ti un auditor hostil, desconfiado y
técnicamente competente intentara demostrar que el sistema está roto. No
confíes en comentarios, documentación, nombres de métodos, tests
existentes ni auditorías anteriores: **demuestra cada afirmación con
código y pruebas.**

No reescribas lo que ya funciona sin necesidad. Corrige de forma mínima,
segura y trazable.

------------------------------------------------------------------------

## 0. Reglas obligatorias de trabajo

1.  `read → reason → write → verify → build/test`.
2.  Antes de modificar algo, confirma su comportamiento real y sus
    consumidores.
3.  No marques un requisito `COMPLETE` sólo porque existe una clase,
    controlador, vista o test.
4.  Un test que se omite (`skip`) **NO demuestra funcionalidad**.
5.  No maquilles fallos cambiando asserts, eliminando tests, aumentando
    timeouts arbitrariamente o convirtiendo fallos en skips.
6.  No uses datos simulados para afirmar que discovery/polling/topología
    reales funcionan.
7.  No introduzcas secretos, passwords, API keys, communities SNMP ni
    cadenas de conexión en repo, excepciones o logs.
8.  No destruyas ni reinicialices una DB que no esté identificada
    inequívocamente como DB de pruebas.
9.  Conserva separación Web/Worker de producción.
10. Después de cada bloque: build Release + tests pertinentes.
11. Al final: `git status` limpio y commits pequeños/temáticos.
12. No hagas merge a `main`.
13. Si una prueba requiere infraestructura no disponible, déjala
    preparada y documenta exactamente qué falta; **no declares ese punto
    verificado**.
14. Cualquier hallazgo nuevo crítico/alto debe corregirse y volver a
    probarse antes del cierre.

------------------------------------------------------------------------

# 1. Hallazgos que DEBES corregir primero

## 1.1 CRÍTICO --- autorización API para sesiones cookie

Revisar `ApiScopeAuthorizationHandler`.

Actualmente una identidad humana autenticada por cookie puede satisfacer
una política de scope sin demostrar el rol necesario. Esto puede
permitir que un usuario autenticado de bajo privilegio invoque endpoints
mutables protegidos únicamente por scopes.

Ejemplo a revisar especialmente:

-   `POST /api/discovery/run`
-   `POST /api/discovery/runs/{id}/cancel`
-   y **todos** los demás `POST`, `PUT`, `PATCH`, `DELETE` bajo
    `/api/*`.

### Exigencia

Diseña una autorización coherente para ambos actores:

-   API key → scope explícito requerido.
-   Usuario humano/cookie → rol/permiso humano explícito equivalente.
-   Nunca interpretar "está autenticado" como "puede ejecutar cualquier
    scope".

Haz inventario completo de endpoints `/api/*` y genera una matriz:

  -------------------------------------------------------------------------------
  Endpoint   Verbo      Actor      Scope       Roles        Mutación   Test
                                   requerido   humanos                 
                                               permitidos              
  ---------- ---------- ---------- ----------- ------------ ---------- ----------

  -------------------------------------------------------------------------------

Prueba al menos:

-   anónimo → rechazado;
-   API key sin scope → rechazado;
-   API key correcta → permitido;
-   API key revocada → rechazado;
-   API key expirada → rechazado;
-   usuario ReadOnly → no puede mutar;
-   operador → sólo operaciones permitidas;
-   admin → operaciones administrativas permitidas;
-   cookie humana no puede "heredar" scopes arbitrarios.

Busca variantes del mismo error en todos los controladores, no sólo
Discovery.

------------------------------------------------------------------------

## 1.2 ALTO --- Data Protection debe fallar cerrado en Production

La configuración actual sólo falla si existe un thumbprint configurado
pero el certificado no aparece.

Si `DataProtection:KeyRingCertThumbprint` está vacío, Production puede
continuar.

### Exigencia

En `Production`:

-   thumbprint vacío/whitespace → startup debe FALLAR;
-   thumbprint inválido/no encontrado → startup debe FALLAR;
-   certificado válido → startup permitido;
-   Development/Test puede conservar comportamiento explícitamente
    permisivo si es necesario.

Agrega tests del contrato de arranque/configuración.

No metas certificados ni secretos reales al repo.

------------------------------------------------------------------------

## 1.3 MEDIO/SEGURIDAD --- nunca filtrar connection strings

`TestDatabaseConfiguration.ValidatePointsToTestDatabase` puede incluir
la cadena recibida en el texto de una excepción.

### Exigencia

Ninguna excepción/log debe imprimir:

-   password;
-   user id;
-   token;
-   API key;
-   SNMP community;
-   secretos de SNMPv3;
-   connection string completa.

El error puede mostrar únicamente información segura, por ejemplo nombre
de DB sanitizado.

Agrega test con password señuelo y demuestra que el mensaje resultante
NO lo contiene.

------------------------------------------------------------------------

# 2. No aceptar el cierre E2E actual

La frase anterior "flujo real end-to-end verificado" **no es válida
mientras Integration/Runtime/E2E hayan sido omitidos por falta de
`ATLASNOC_TEST_CONNECTION`.**

Prepara y ejecuta, cuando exista MySQL de pruebas, una DB dedicada cuyo
nombre contenga `_test` o `_e2e`.

Nunca uses la DB de producción/desarrollo para pruebas destructivas.

Verificar:

1.  migración desde DB vacía;
2.  migración sobre esquema existente compatible;
3.  creación/setup inicial;
4.  login válido;
5.  login inválido + lockout;
6.  usuario desactivado pierde sesión;
7.  matriz completa de roles;
8.  API key creación/uso/scopes/revocación/expiración;
9.  discovery;
10. cancelación de discovery;
11. lease/renovación/recuperación;
12. polling;
13. interfaces;
14. métricas;
15. vecinos;
16. correlación;
17. links canónicos;
18. rediscovery idempotente;
19. stale/refresh;
20. alertas;
21. recovery;
22. notification delivery/retry;
23. subscribers/endpoints;
24. Web + Worker separados;
25. reinicio de procesos sin corromper estado.

Los resultados deben distinguir claramente:

-   PASS real
-   FAIL real
-   SKIPPED por infraestructura
-   NOT TESTED

**SKIPPED jamás cuenta como PASS.**

------------------------------------------------------------------------

# 3. Auditoría hostil desde cero

Después de corregir los hallazgos anteriores, olvida las conclusiones de
`F11_GAP_AUDIT.md` y vuelve a auditar.

Piensa como alguien contratado para romper AtlasNOC.

## 3.1 Compilación y dependencias

Ejecutar como mínimo:

``` powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

Revisar:

-   0 errores;
-   objetivo 0 warnings;
-   paquetes vulnerables;
-   referencias duplicadas/innecesarias;
-   proyectos no incluidos correctamente en `.sln`;
-   tests "vacíos" o descubiertos incorrectamente;
-   tests que pasan sin ejecutar lógica relevante.

Inspecciona especialmente cualquier archivo de tests reportado por Git
con `0 additions/0 deletions` y confirma que realmente existe contenido
y que xUnit lo descubre.

------------------------------------------------------------------------

## 3.2 Seguridad

Intentar romper:

-   autenticación cookie;
-   API key;
-   scopes;
-   roles;
-   IDOR;
-   acceso directo a URLs ocultas por UI;
-   CSRF en MVC;
-   endpoints mutables;
-   mass assignment;
-   overposting;
-   escalamiento ReadOnly → operador/admin;
-   usuario desactivado con cookie vieja;
-   revocación de API key durante sesión/uso;
-   rate limiting;
-   spoofing de `X-Forwarded-For`;
-   forwarded headers;
-   headers de seguridad;
-   HTTPS/HSTS;
-   Data Protection;
-   exposición de secretos;
-   logs;
-   mensajes de error;
-   setup inicial;
-   último administrador;
-   rutas de health checks;
-   información sensible devuelta por API.

No confundas "botón oculto" con autorización.

------------------------------------------------------------------------

## 3.3 Discovery real

Auditar el flujo completo:

`target → ping/alcance → credencial → SNMP → identificación → interfaces → vecinos → persistencia → correlación → topología`

Probar límites:

-   `/32`;
-   `/31`;
-   CIDR inválido;
-   rango demasiado grande;
-   IPv4 inválida;
-   timeout;
-   host caído;
-   credencial incorrecta;
-   SNMP sin respuesta;
-   cancelación;
-   dos discoveries simultáneos;
-   Worker muerto a mitad;
-   lease vencido;
-   rediscovery.

No fabricar dispositivos, interfaces, vecinos, links ni métricas cuando
no existe evidencia.

------------------------------------------------------------------------

## 3.4 SNMP y drivers

Auditar realmente:

### SNMP

-   v2c;
-   v3;
-   noAuthNoPriv si está soportado;
-   authNoPriv;
-   authPriv;
-   SHA1/SHA256 según implementación;
-   AES/AES128 según implementación;
-   credenciales incorrectas;
-   timeouts;
-   secreto nunca logueado.

### Cisco

-   identificación;
-   sysObjectID;
-   ifTable;
-   LLDP;
-   CDP;
-   ausencia de CDP;
-   datos parciales/malformados.

### MikroTik

-   credencial aplicada;
-   uptime;
-   interfaces;
-   TLS/pinning;
-   respuestas incompletas.

### Ubiquiti

-   UniFi Controller;
-   AirOS;
-   TLS;
-   pinning;
-   certificado inválido;
-   respuesta JSON incompleta;
-   controlador inaccesible.

Un driver no está `COMPLETE` porque compile: debe manejar éxito, fallo y
datos parciales sin inventar resultados.

------------------------------------------------------------------------

# 4. Topología: atacar la lógica

Buscar falsos enlaces y duplicados.

Verificar:

-   A→B y B→A producen un único link canónico;
-   correlación por MAC;
-   correlación por IP;
-   hostname sólo cuando sea inequívoco;
-   vecinos ambiguos no generan link falso;
-   protocolo/evidencia/confianza se preservan;
-   links manuales no se destruyen por rediscovery;
-   reject/confirm funciona;
-   stale funciona;
-   desaparición temporal no destruye historia;
-   múltiples interfaces;
-   múltiples sites;
-   mismo hostname en dos equipos;
-   IP reutilizada;
-   MAC cambiada;
-   observaciones contradictorias;
-   CDP vs LLDP contradictorios;
-   complejidad razonable con inventario grande.

La UI debe permitir entender **por qué existe un enlace**. Si backend
guarda evidencia pero la UI no la muestra suficientemente, no marcar
`COMPLETE`.

------------------------------------------------------------------------

# 5. Polling y métricas

Verificar que jamás se confunda:

-   "no pude medir" con valor `0`;
-   "lo vi" con "lo sondeé correctamente";
-   timeout con métrica válida.

Atacar:

-   dispositivo offline;
-   interfaz desaparecida;
-   counter wrap/reset;
-   valores nulos;
-   concurrencia;
-   polling duplicado;
-   Worker reiniciado;
-   perfil por dispositivo;
-   timeout/retry;
-   timestamps;
-   persistencia;
-   UI sin datos.

No inventar gráficas bonitas con datos inexistentes.

------------------------------------------------------------------------

# 6. Alertas y notificaciones

Probar:

-   condición;
-   operador;
-   umbral;
-   consecutive faults;
-   apertura;
-   no duplicar incidente;
-   recovery;
-   retries;
-   delivery lease;
-   Worker cae durante envío;
-   webhook timeout;
-   webhook 4xx;
-   webhook 5xx;
-   duplicación;
-   máximo de intentos;
-   datos sensibles en payload/log.

Asegurar idempotencia razonable.

------------------------------------------------------------------------

# 7. UI/MVC

Recorrer **todas las vistas** y acciones, no sólo verificar archivos.

Revisar:

-   Dashboard;
-   Devices;
-   Interfaces;
-   Links;
-   Metrics;
-   Discovery;
-   Sites;
-   Subscribers;
-   Incidents;
-   Alerts;
-   Alert Rules;
-   Credentials;
-   API Keys;
-   Integrations;
-   Users;
-   Setup/Login;
-   Topology.

Buscar:

-   404;
-   500;
-   modelos Razor incorrectos;
-   enlaces rotos;
-   botones que llaman acciones inexistentes;
-   formularios sin antiforgery;
-   errores de autorización;
-   dropdowns vacíos;
-   IDs manipulables;
-   nulls;
-   listas vacías;
-   estados sin datos;
-   mensajes engañosos;
-   navegación según rol.

Probar URLs directamente, no sólo mediante botones.

------------------------------------------------------------------------

# 8. Persistencia y migraciones

Crear DB limpia y aplicar **todas** las migraciones en orden.

Revisar especialmente las nuevas migraciones de:

-   PollingProfiles;
-   DiscoveryRun leases;
-   topology FKs/canonical links;
-   NotificationDeliveries.

Buscar:

-   pérdida de datos;
-   FK imposibles;
-   índices faltantes;
-   índices únicos incorrectos;
-   cascade deletes peligrosos;
-   valores default incompatibles;
-   migraciones que sólo funcionan sobre DB vacía;
-   snapshot desincronizado.

Ejecutar consultas/invariantes que demuestren integridad.

------------------------------------------------------------------------

# 9. Web + Worker

Producción debe conservar responsabilidades claras.

Probar:

1.  Web solo;
2.  Worker solo;
3.  ambos;
4.  reinicio Web;
5.  reinicio Worker;
6.  Worker muerto durante discovery/polling/notificación;
7.  dos Workers si la arquitectura lo permite;
8.  lease impide trabajo duplicado.

No habilitar workers embebidos en Web en Production accidentalmente.

------------------------------------------------------------------------

# 10. Configuración de producción

Auditar `appsettings*.json`, variables de entorno y startup.

Production debe:

-   no contener secretos en repo;
-   exigir configuración crítica;
-   fallar cerrado cuando falte protección criptográfica obligatoria;
-   no aceptar `SkipCertificateValidation=true` de forma insegura sin
    advertencia/limitación;
-   no confiar en forwarded headers arbitrarios;
-   no usar LabMode;
-   no arrancar silenciosamente con DB inexistente/mal configurada;
-   registrar suficiente diagnóstico sin registrar secretos.

------------------------------------------------------------------------

# 11. Pruebas nuevas obligatorias

Además de conservar las existentes, agrega pruebas de regresión para
cada bug corregido.

Como mínimo:

1.  Cookie ReadOnly no puede ejecutar `DiscoveryRun`.
2.  Cookie sin rol adecuado no puede cancelar discovery.
3.  API key sin scope no puede ejecutar acción.
4.  API key con scope correcto sí.
5.  API key revocada falla.
6.  Production + thumbprint vacío → startup/config validation falla.
7.  Production + thumbprint inexistente → falla.
8.  mensaje de error de DB de tests no contiene password señuelo.
9.  DB no `_test/_e2e` → rechazo seguro.
10. URLs MVC/API críticas respetan matriz de roles.

No elimines cobertura existente para hacerlos pasar.

------------------------------------------------------------------------

# 12. Auditoría de los propios tests

Trata los tests como código sospechoso.

Buscar:

-   asserts triviales;
-   mocks que prueban el mock y no el sistema;
-   tests sin asserts;
-   tests que nunca son descubiertos;
-   `[Fact(Skip=...)]` injustificado;
-   skips dinámicos que ocultan bugs;
-   catches que comen excepciones;
-   tests dependientes del orden;
-   estado compartido;
-   sleeps;
-   puertos fijos;
-   matar procesos ajenos;
-   DB compartida;
-   credenciales hardcoded.

Reporta número real por proyecto:

  Suite     Descubiertos   Passed   Failed   Skipped Motivo skips
  ------- -------------- -------- -------- --------- --------------

------------------------------------------------------------------------

# 13. Criterio de severidad

Clasifica hallazgos:

-   **CRITICAL** --- auth bypass, pérdida/corrupción seria, secretos,
    ejecución no autorizada.
-   **HIGH** --- función principal rota, aislamiento deficiente,
    producción insegura.
-   **MEDIUM** --- comportamiento incorrecto recuperable,
    observabilidad/UX importante.
-   **LOW** --- deuda menor, limpieza, documentación.

No cierres mientras quede CRITICAL o HIGH conocido sin resolver.

------------------------------------------------------------------------

# 14. Entregables finales

Crear/actualizar un documento:

`docs/FINAL_HOSTILE_AUDIT.md`

Debe contener evidencia, no propaganda.

Incluye:

1.  commit inicial auditado;
2.  commit final;
3.  archivos modificados;
4.  bugs encontrados;
5.  causa raíz;
6.  corrección;
7.  test de regresión;
8.  matriz de autorización API/MVC;
9.  resultado build;
10. resultado de cada suite;
11. E2E ejecutados realmente;
12. pruebas omitidas y por qué;
13. migración DB limpia;
14. prueba Web/Worker;
15. pruebas de drivers disponibles;
16. limitaciones de laboratorio;
17. vulnerabilidades/dependencias;
18. hallazgos pendientes por severidad;
19. `git status`;
20. veredicto.

El veredicto sólo puede ser uno:

-   `NOT READY`
-   `READY WITH DOCUMENTED NON-BLOCKING LIMITATIONS`
-   `READY`

No escribir `READY` si algún requisito crítico depende exclusivamente de
un test omitido o de infraestructura que nunca se probó.

------------------------------------------------------------------------

# 15. Orden de ejecución

Sigue este orden y no te detengas después del diagnóstico si existe una
corrección segura:

``` text
A. Baseline y git status
B. Reproducir los 3 hallazgos conocidos
C. Corregir autorización API
D. Corregir Data Protection fail-closed
E. Corregir filtración de connection string
F. Build + Unit
G. Auditoría de todos los endpoints/roles
H. Auditoría security
I. Discovery/SNMP/drivers
J. Topología
K. Polling/métricas
L. Alertas/notificaciones
M. UI completa
N. Migraciones/DB
O. Web + Worker
P. Integration
Q. Runtime
R. E2E
S. Auditoría hostil final desde cero
T. FINAL_HOSTILE_AUDIT.md
U. Build/test final
V. git status
W. commits temáticos
```

------------------------------------------------------------------------

# 16. Condición final

No quiero una respuesta del tipo:

> "Todo compila, 123/123 unit tests, por lo tanto está listo."

Quiero demostrar que AtlasNOC hace lo que promete.

Si falta `ATLASNOC_TEST_CONNECTION`, prepara todo lo que pueda hacerse
sin ella y deja **una instrucción exacta y mínima** para ejecutar la
fase real posteriormente. No conviertas esa ausencia en éxito.

Cuando tengas infraestructura disponible, ejecuta las suites reales y
vuelve a auditar los resultados.

**Trabaja hasta que no queden defectos CRITICAL/HIGH conocidos y el
código + pruebas + evidencia sean coherentes entre sí. No hagas merge a
`main`.**
