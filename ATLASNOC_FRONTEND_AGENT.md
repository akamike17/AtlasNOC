# ATLASNOC — IMPLEMENTACIÓN COMPLETA DEL FRONTEND NOC
## Instrucción de ejecución para agente de programación (DeepSeek / Nemotron / Codex)

### OBJETIVO

Construir e integrar un **frontend NOC funcional, profesional y operable** sobre el backend existente de AtlasNOC.

No se busca una maqueta, un tema visual ni páginas estáticas. El resultado debe permitir a un operador de red **ver el estado de la infraestructura, descubrir equipos, consultar topología, revisar métricas, alertas e incidentes y administrar los componentes existentes del backend**.

El backend actual ya contiene lógica y endpoints para dispositivos, descubrimiento, topología, métricas, alertas, incidentes, CVE, credenciales, auditoría y API keys. El frontend debe aprovechar lo que existe; **no duplicar lógica de dominio ni reemplazar servicios que ya funcionan**.

El sistema debe poder arrancar localmente desde Visual Studio y `dotnet run`, conectarse a MySQL y presentar una interfaz útil desde el navegador.

---

# 1. REGLA PRINCIPAL DE TRABAJO

No dediques una larga fase a describir lo que piensas hacer.

Máximo al inicio:

1. inspecciona la solución;
2. identifica las rutas/controladores/servicios/modelos disponibles;
3. identifica qué piezas faltan;
4. empieza a implementar.

No te detengas después del diagnóstico cuando exista una corrección segura.

Secuencia obligatoria por cambio:

**read → edit/write → read de verificación → build/test → ejecución real cuando aplique**

Si una compilación falla:

- lee primero el error real;
- corrige la causa;
- recompila;
- no cambies arquitectura o paquetes al azar para forzar el build.

No declares terminado un módulo sólo porque compila.

---

# 2. RESTRICCIONES

- Mantener **.NET 8**.
- Mantener el backend y servicios existentes.
- Mantener **MySQL/Pomelo**.
- No introducir React, Angular, Vue, Node, Vite ni otro pipeline SPA salvo que la evidencia del repositorio demuestre que ya existe y es necesario.
- Preferir **ASP.NET Core MVC/Razor Views + JavaScript moderno** integrado al proyecto existente.
- No introducir servicios de pago.
- No depender de SaaS para que la interfaz básica funcione.
- No almacenar secretos en Git.
- No escribir contraseñas, API keys ni cadenas reales en `appsettings.json`, `launchSettings.json`, JavaScript, HTML o repositorio.
- No eliminar autenticación del API para “hacer funcionar el frontend”.
- No degradar seguridad de Production.
- No deshabilitar validaciones existentes.
- No alterar migraciones o esquema salvo que una función del frontend realmente lo requiera.
- Si necesitas una librería gráfica para topología, usar una librería madura y ligera, preferentemente Cytoscape.js o equivalente; fijar versión y evitar dependencias innecesarias.

---

# 3. ARQUITECTURA DEL FRONTEND

Construir el frontend dentro del mismo proyecto ASP.NET Core.

Estructura recomendada, adaptándola al repositorio real:

```text
Controllers/
    Ui/
        DashboardController.cs
        DevicesUiController.cs
        TopologyUiController.cs
        DiscoveryUiController.cs
        AlertsUiController.cs
        IncidentsUiController.cs
        MetricsUiController.cs
        CvesUiController.cs
        AdministrationUiController.cs

Views/
    Shared/
        _Layout.cshtml
        _Sidebar.cshtml
        _Topbar.cshtml
        _StatusBadge.cshtml
        _Pagination.cshtml
    Dashboard/
    Devices/
    Topology/
    Discovery/
    Alerts/
    Incidents/
    Metrics/
    Cves/
    Administration/

wwwroot/
    css/
        atlasnoc.css
    js/
        atlasnoc.js
        dashboard.js
        topology.js
        devices.js
        discovery.js
        alerts.js
        incidents.js
        metrics.js
```

No copies esta estructura ciegamente si el repositorio ya tiene convenciones mejores.

La UI debe consumir **servicios de aplicación/dominio existentes** desde controladores MVC cuando sea razonable. No hagas llamadas HTTP internas desde el mismo servidor hacia su propia API sólo para reutilizar endpoints.

Para actualizaciones dinámicas del navegador se puede usar `fetch` a endpoints apropiados, pero el diseño de autenticación debe ser seguro.

---

# 4. AUTENTICACIÓN DEL FRONTEND

El backend utiliza autenticación por API key y una política global de autorización.

No expongas una API key administrativa embebida en JavaScript ni HTML.

Antes de construir la UI:

1. inspecciona `ApiKeyAuthenticationHandler`;
2. inspecciona `ApiKeyStore`;
3. inspecciona roles/policies existentes;
4. diseña una entrada segura para la UI.

Solución esperada:

- pantalla de acceso local al NOC;
- el usuario introduce una API key válida;
- el servidor la valida con el mecanismo existente;
- después de validar, establecer una sesión/cookie HttpOnly y segura para la UI **o una solución equivalente server-side**;
- no persistir la API key en `localStorage`;
- no escribirla en logs;
- no devolverla al navegador después de autenticar;
- respetar roles:
  - Administrator;
  - NocOperator;
  - ReadOnly.

Si la arquitectura actual no permite esto limpiamente, implementa el puente mínimo y documentado entre autenticación API-key y sesión UI sin debilitar el API.

Production debe continuar protegido.

---

# 5. DISEÑO VISUAL

La interfaz debe parecer un **Network Operations Center**, no un CRUD genérico.

Características:

- tema oscuro por defecto;
- alto contraste;
- responsive para escritorio y laptop;
- sidebar colapsable;
- topbar con estado global;
- indicadores de salud;
- tablas compactas;
- badges de severidad;
- tarjetas de estado;
- tooltips;
- estados vacíos útiles;
- spinners o skeletons durante carga;
- mensajes de error visibles pero sin filtrar stack traces;
- navegación consistente.

No llenar todo de animaciones.

Priorizar legibilidad de información operacional.

---

# 6. PANTALLA 1 — DASHBOARD NOC

Crear `/` como tablero principal.

Debe mostrar, usando datos reales cuando existan:

- total de dispositivos;
- dispositivos Up;
- dispositivos Down;
- dispositivos Unknown/Degraded;
- alertas activas;
- alertas críticas;
- incidentes abiertos;
- incidentes críticos;
- descubrimientos recientes;
- CVE críticas relevantes;
- salud de base de datos;
- salud general del sistema;
- último ciclo de polling;
- última actualización de datos.

Agregar un panel de actividad reciente con caída/recuperación de dispositivo, alerta abierta/resuelta, incidente creado y descubrimiento ejecutado.

Visualizar tendencias de latencia, disponibilidad, pérdida y CPU/memoria **sólo cuando esos datos existan**. No inventar datos. Si una métrica no existe, mostrar “sin datos”.

Actualizar información operacional sin recargar toda la página. Evitar polling agresivo; usar intervalos razonables y acordes al backend.

---

# 7. PANTALLA 2 — DISPOSITIVOS

Crear vista operacional completa.

Tabla con:

- nombre;
- IP;
- tipo;
- fabricante/modelo si existe;
- estado;
- última respuesta;
- latencia;
- disponibilidad;
- referencia de credencial si la política lo permite, nunca el secreto;
- acciones.

Funciones:

- búsqueda;
- filtros por estado/tipo;
- ordenamiento;
- paginación;
- crear;
- editar;
- ver detalle;
- activar/desactivar si el modelo lo soporta;
- ejecutar polling/prueba individual si ya existe backend apropiado;
- ir a métricas;
- localizar en topología.

Detalle de dispositivo:

- identidad;
- conectividad;
- estado actual;
- métricas;
- alertas asociadas;
- incidentes asociados;
- CVE relacionadas si aplica;
- historial reciente.

No mostrar contraseñas ni community strings en claro.

---

# 8. PANTALLA 3 — TOPOLOGÍA DE RED

Ésta es una función central de AtlasNOC.

Construir una vista gráfica real de los nodos y enlaces que entregue `ITopologyService` / `TopologyController`.

Requisitos:

- nodos reales del backend;
- enlaces reales del backend;
- zoom;
- pan;
- centrar;
- fit-to-screen;
- selección de nodo;
- tooltip;
- panel lateral de detalle;
- búsqueda por hostname/IP;
- filtros;
- leyenda;
- estados diferenciados visualmente;
- refresco sin reconstruir innecesariamente toda la página.

Estados visuales sugeridos: Up, Down, Degraded, Unknown.

Categorías cuando existan: router, switch, access point, servidor, firewall, host, otro.

Al seleccionar un nodo mostrar nombre, IP, tipo, estado, última respuesta, métricas principales, alertas y enlace a detalle.

No dibujar conexiones ficticias. Si todavía no hay información suficiente para inferir ciertos enlaces, mostrar nodos sin conexión o indicar “topología incompleta”.

---

# 9. PANTALLA 4 — DESCUBRIMIENTO

Construir flujo de descubrimiento con parámetros **realmente soportados por el backend**.

Mostrar:

- ejecución actual;
- progreso si existe información;
- estado;
- inicio/fin;
- dispositivos encontrados;
- errores;
- historial de descubrimientos.

El usuario debe poder revisar resultados antes de confundir “descubierto” con “monitoreado”, según la lógica real existente.

No lanzar escaneos automáticos indiscriminados al abrir la pantalla.

---

# 10. PANTALLA 5 — ALERTAS

Vista operacional con severidad, estado, dispositivo, mensaje, origen, fecha, edad y acciones.

Filtros por severidad, estado, dispositivo y fecha.

Acciones según servicios existentes: reconocer, resolver, abrir dispositivo, crear/ver incidente relacionado.

---

# 11. PANTALLA 6 — INCIDENTES

Mostrar folio/id, título, severidad, estado, dispositivos involucrados, fecha de creación, actualización y responsable si existe.

Detalle con descripción, timeline, alertas relacionadas, evidencia/datos asociados y cambios de estado.

No inventar workflow que contradiga entidades/servicios actuales.

---

# 12. PANTALLA 7 — MÉTRICAS

Crear página de métricas e historial por dispositivo y período.

Mostrar sólo datos realmente disponibles: disponibilidad, latencia, pérdida, CPU, memoria, interfaces/tráfico cuando existan.

Permitir rangos de tiempo sólo si el servicio los soporta de forma razonable.

No descargar cantidades masivas de puntos al navegador sin agregación.

---

# 13. PANTALLA 8 — CVE

Aprovechar el módulo CVE existente.

Mostrar CVE, severidad/CVSS, descripción resumida, fecha, dispositivo/producto asociado cuando exista y estado relevante.

Diferenciar claramente entre CVE conocida, posible coincidencia y vulnerabilidad confirmada. No afirmar vulnerabilidad confirmada por coincidencia vaga.

---

# 14. ADMINISTRACIÓN

Sólo para Administrator donde corresponda.

### Credenciales
- listar metadatos permitidos;
- crear;
- editar;
- eliminar/deshabilitar;
- nunca revelar secreto existente.

### API Keys
- listar owner/rol/estado/fecha;
- crear;
- revocar;
- mostrar una key nueva sólo una vez si ése es el comportamiento seguro del backend;
- nunca recuperar el valor secreto después.

### Auditoría
- tabla de eventos;
- actor;
- acción;
- recurso;
- fecha;
- filtros;
- detalle seguro.

---

# 15. HEALTH / DIAGNÓSTICO

Crear una pequeña pantalla de estado accesible desde el NOC:

- aplicación;
- MySQL;
- Redis si está configurado;
- polling service;
- notificaciones si se puede determinar;
- CVE background service si se puede determinar.

Usar `/health/live`, `/health/ready` y servicios existentes. No convertir detalles internos sensibles en información pública.

---

# 16. SWAGGER Y DEVELOPMENT

Existe Swagger y la aplicación tiene una `FallbackPolicy` que requiere usuario autenticado.

Verificar que en **Development** `/swagger` pueda cargar para desarrollo/pruebas sin eliminar seguridad de los endpoints API.

La API debe seguir exigiendo `X-API-Key` según sus políticas.

Production no debe quedar accidentalmente abierto.

---

# 17. USER SECRETS Y ARRANQUE

Existe una configuración del proyecto con:

```xml
<GenerateAssemblyInfo>false</GenerateAssemblyInfo>
```

Eso puede impedir que el `UserSecretsId` del `.csproj` se convierta automáticamente en `UserSecretsIdAttribute`.

Antes de cambiar nada:

- inspecciona el estado actual del proyecto;
- preserva/corrige el mecanismo de User Secrets de forma compatible;
- no generes múltiples UserSecretsId;
- no pongas secretos reales en archivos versionados.

Prueba obligatoria:

```powershell
dotnet run --launch-profile http
```

Debe arrancar usando Development y configuración local válida.

---

# 18. PÁGINA DE INICIO

F5 desde Visual Studio debe abrir o permitir abrir una página útil.

La ruta `http://localhost:5267/` debe mostrar el dashboard o redirigir al acceso de la UI.

No debe devolver una pantalla vacía.

Configurar `launchBrowser` y/o `launchUrl` sólo si es apropiado, sin introducir secretos.

---

# 19. EXPERIENCIA SIN DATOS

AtlasNOC debe funcionar también en una base nueva.

Cada pantalla debe tener estados vacíos útiles, por ejemplo “No hay dispositivos monitoreados”, botón “Agregar dispositivo” y botón “Iniciar descubrimiento”.

La ausencia de datos no debe provocar null reference, gráfica rota, pantalla blanca, JavaScript exception ni loops infinitos.

---

# 20. ERRORES DE RED Y BACKEND

Todas las llamadas dinámicas deben manejar 400, 401, 403, 404, 409, 429, 500, timeout y pérdida temporal de conexión.

Mostrar mensajes útiles al operador.

Nunca mostrar connection strings, stack traces de Production, passwords, API keys, tokens o secretos de credenciales.

---

# 21. ACCESIBILIDAD Y UX

Mínimo:

- navegación por teclado razonable;
- labels de formularios;
- aria en botones sólo-icono;
- contraste suficiente;
- foco visible;
- confirmación para operaciones destructivas;
- no depender sólo del color para severidad/estado;
- fechas legibles;
- zonas horarias consistentes.

---

# 22. SEGURIDAD

Prohibido:

- API key en código JS;
- API key en repo;
- password en HTML;
- credenciales en query string;
- secretos en logs;
- `AllowAnonymous` global;
- quitar `FallbackPolicy`;
- CORS abierto en Production sólo para facilitar UI;
- deshabilitar CSRF si se usa autenticación por cookie;
- renderizar HTML no confiable sin encoding.

Aplicar anti-forgery a operaciones MVC basadas en cookie cuando corresponda.

Roles deben impedir tanto la UI como la acción real; ocultar un botón no sustituye autorización server-side.

---

# 23. DATOS EN TIEMPO REAL

No construir una falsa UI “real time”.

Primero inspeccionar cómo AtlasNOC obtiene y persiste polling, métricas, descubrimiento, alertas y topología.

Después escoger polling HTTP moderado para primera versión o SignalR sólo si aporta valor real y puede integrarse limpiamente.

No introducir SignalR sólo por apariencia.

---

# 24. ORDEN DE IMPLEMENTACIÓN

Trabaja en este orden para producir valor visible rápidamente:

### Fase A — Base funcional
1. mecanismo de acceso UI;
2. layout;
3. navegación;
4. dashboard;
5. ruta `/`;
6. estado vacío.

### Fase B — NOC operativo
7. dispositivos;
8. topología;
9. descubrimiento;
10. métricas.

### Fase C — Operación
11. alertas;
12. incidentes;
13. CVE.

### Fase D — Administración
14. credenciales;
15. API keys;
16. auditoría;
17. health/diagnóstico.

### Fase E — endurecimiento
18. roles;
19. manejo de errores;
20. accesibilidad;
21. responsive;
22. pruebas completas.

No esperes hasta el final para ejecutar la aplicación.

---

# 25. PRUEBAS OBLIGATORIAS

Ejecutar y registrar resultados reales.

## Compilación

```powershell
dotnet clean
dotnet restore AtlasNOC.slnx
dotnet build AtlasNOC.slnx -c Release
```

## Tests

Ejecutar todos los proyectos de pruebas existentes. No afirmar “tests pass” sin ejecutar.

## Runtime

```powershell
dotnet run --launch-profile http
```

Verificar:

```text
http://localhost:5267/
http://localhost:5267/health/live
http://localhost:5267/health/ready
http://localhost:5267/swagger
```

## UI smoke tests

Comprobar:

1. login/acceso válido;
2. acceso inválido;
3. Dashboard;
4. Devices;
5. Device Detail;
6. Topology;
7. Discovery;
8. Metrics;
9. Alerts;
10. Incidents;
11. CVE;
12. Credentials;
13. API Keys;
14. Audit;
15. Health.

## Roles

Probar Administrator, NocOperator y ReadOnly. ReadOnly no debe poder ejecutar mutaciones aunque llame directamente al endpoint.

---

# 26. PRUEBAS DE TOPOLOGÍA

Construir casos de prueba con datos válidos de laboratorio o fixtures existentes:

- 1 router + 1 switch + 2 hosts;
- nodos sin enlaces conocidos;
- un dispositivo Down;
- red con suficientes nodos para comprobar zoom/pan/layout.

Verificar que la UI no invente relaciones.

---

# 27. PRUEBA DE FALLOS

Probar al menos:

- MySQL temporalmente no disponible;
- endpoint devuelve 401;
- endpoint devuelve 403;
- endpoint devuelve 429;
- dispositivo sin métricas;
- topología vacía;
- datos incompletos/null;
- fallo de JavaScript/API;
- API lenta.

La UI debe fallar de forma controlada.

---

# 28. CRITERIOS DE ACEPTACIÓN

NO declarar completado hasta cumplir:

- `/` muestra una UI real.
- Visual Studio F5 arranca sin excepción de configuración.
- User Secrets funcionan en Development.
- MySQL conecta.
- `/health/live` responde Healthy.
- `/health/ready` responde Healthy cuando dependencias están disponibles.
- Swagger funciona en Development de forma compatible con la seguridad.
- La UI no contiene secretos.
- Existe Dashboard NOC.
- Existe inventario de dispositivos.
- Existe topología gráfica funcional.
- Existe flujo de descubrimiento.
- Existen métricas.
- Existen alertas.
- Existen incidentes.
- Existe CVE.
- Existe administración según roles.
- ReadOnly no puede mutar.
- No hay errores JavaScript bloqueantes.
- Build Release correcto.
- Tests existentes correctos.
- Aplicación probada realmente en navegador.

---

# 29. LO QUE NO CUENTA COMO TERMINADO

No aceptar como final:

- “El código debería funcionar”.
- “Build exitoso” sin runtime.
- sólo Swagger.
- sólo API.
- sólo HTML estático.
- mockups.
- tarjetas con números hardcoded.
- topología con nodos ficticios.
- datos demo presentados como reales.
- botones sin acción.
- vistas creadas pero no accesibles.
- endpoint creado pero no probado.
- TODOs que bloquean funciones principales.

---

# 30. CONTROL DE CAMBIOS

Antes de modificar:

```powershell
git status
git branch --show-current
git log -1 --oneline
```

No borres cambios locales del usuario.

Hay una corrección local potencial relacionada con User Secrets/AssemblyInfo. **Inspecciónala antes de hacer reset, checkout, restore o reemplazos masivos.**

No ejecutar `git reset --hard` ni `git clean -fd` sin necesidad explícita. No force push.

---

# 31. ENTREGA FINAL DEL AGENTE

Al terminar, devolver solamente un informe conciso con:

```text
IMPLEMENTADO
- ...

ARCHIVOS CLAVE CREADOS/MODIFICADOS
- ...

PRUEBAS EJECUTADAS
- comando → resultado

RUNTIME
- / → resultado
- /health/live → resultado
- /health/ready → resultado
- /swagger → resultado

SEGURIDAD
- autenticación UI → resultado
- Administrator → resultado
- NocOperator → resultado
- ReadOnly → resultado
- secretos expuestos → NO

PENDIENTES REALES
- únicamente lo que no haya sido posible completar

COMMIT
- SHA y mensaje, sólo si se solicitó crear commit
```

No llenar el cierre con explicaciones sobre lo que “harías después”.

---

# 32. INSTRUCCIÓN FINAL

Tienes autonomía para **leer, diseñar, crear, modificar, compilar, ejecutar y probar** lo necesario dentro de AtlasNOC para cumplir este objetivo.

No interpretes esta tarea como “ayúdame a programar el frontend”. Interprétala como:

> **entrega una primera versión operativa y segura de la consola web NOC de AtlasNOC sobre el backend existente.**

Investiga el repositorio antes de cambiarlo, reutiliza la lógica existente, toma decisiones técnicas razonables y ejecuta el trabajo.

Si encuentras un defecto que bloquea el frontend y la corrección es segura y verificable, corrígelo y vuelve a probar.

No te detengas para pedir instrucciones sobre decisiones menores que puedes resolver leyendo el código.

No declares éxito hasta ver la aplicación funcionando realmente.
