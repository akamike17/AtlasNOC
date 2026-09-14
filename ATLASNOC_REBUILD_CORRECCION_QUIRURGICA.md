# ATLASNOC-REBUILD — CORRECCIÓN QUIRÚRGICA Y CIERRE REAL

## ESTADO CRÍTICO

La ejecución anterior tomó una decisión INCORRECTA:

Se eliminaron atributos `[Authorize]` de los controladores API de:

`C:\AtlasNOC-Rebuild\src\AtlasNOC.Web\Controllers\Api`

Esto NO era el objetivo.

El permiso otorgado al agente para ejecutar comandos con privilegios elevados
NO significa que deba eliminar la autorización/autenticación de la aplicación.

IMPORTANTE:

**NO CONFUNDIR permisos del agente/terminal con seguridad de AtlasNOC.**

Eliminar `[Authorize]` para conseguir que una prueba pase es una modificación
funcional y de seguridad inaceptable.

La aplicación debe conservar su modelo de autenticación/autorización.

---

# OBJETIVO

Auditar el estado actual del repositorio, identificar exactamente todos los
cambios realizados durante esta sesión, restaurar la seguridad eliminada,
encontrar la causa REAL de los fallos y dejar AtlasNOC-Rebuild funcional
sin debilitar seguridad ni falsear pruebas.

NO considerar la tarea terminada simplemente porque el proyecto compila.

---

# REGLAS ABSOLUTAS

1. NO eliminar `[Authorize]`.
2. NO agregar `[AllowAnonymous]` para hacer pasar pruebas.
3. NO deshabilitar autenticación.
4. NO deshabilitar autorización.
5. NO cambiar políticas de seguridad para satisfacer tests.
6. NO eliminar tests fallidos.
7. NO cambiar asserts para convertir fallos en éxitos.
8. NO marcar tests como Skip para conseguir verde.
9. NO sustituir integración real por mocks solamente para pasar pruebas.
10. NO modificar lógica funcional sin demostrar primero la causa.
11. NO hacer cambios masivos con `sed`, regex o reemplazos globales.
12. NO ejecutar `git reset --hard`, `git clean`, checkout masivo ni comandos destructivos que puedan eliminar trabajo previo.
13. NO hacer commit.
14. NO hacer push.
15. NO asumir que "build successful" significa "sistema correcto".
16. NO declarar éxito mientras existan pruebas omitidas relevantes sin investigar la razón.
17. NO modificar archivos que no estén directamente relacionados con un defecto demostrado.
18. Preservar TODO trabajo previo válido del repositorio.

---

# CONTINUIDAD

Continúa trabajando sobre el estado ACTUAL del repositorio.

NO reinicies la tarea desde cero.
NO descartes trabajo válido previo.
Primero audita el diff actual y repara exclusivamente las regresiones introducidas por esta sesión.

Después continúa TODAS las fases de este documento en orden hasta llegar al INFORME FINAL.

NO COMMIT.
NO PUSH.

---

# FASE 1 — DETENER CAMBIOS Y AUDITAR

ANTES DE EDITAR MÁS ARCHIVOS ejecutar:

```bash
git status
git diff --stat
git diff
git diff --name-only
```

Examinar TODO el diff.

Clasificar cada cambio actual como:

- cambio previo válido
- cambio realizado por esta sesión
- cambio dudoso
- regresión introducida por esta sesión

NO revertir automáticamente el repositorio completo.

---

# FASE 2 — RESTAURAR `[Authorize]` CORRECTAMENTE

Determinar exactamente qué atributos `[Authorize]` fueron eliminados.

Usar Git/diff/historial para conocer su ubicación ORIGINAL.

NO inventar dónde deberían estar.

Restaurar únicamente los atributos eliminados por esta sesión y exactamente
en las clases/métodos donde estaban antes.

Revisar especialmente:

`src/AtlasNOC.Web/Controllers/Api`

Después comprobar:

```bash
git diff
```

El diff NO debe mostrar eliminación accidental de autorización.

---

# FASE 3 — INVESTIGAR EL HTTP 405 ORIGINAL

El problema observado originalmente era aproximadamente:

`POST /api/operations/customers`

Expected:

`201 Created`

Actual:

`405 Method Not Allowed`

Un HTTP 405 normalmente indica que la ruta existe pero el método HTTP solicitado no está permitido.

NO intentar resolver un 405 eliminando autorización.

Localizar:

- `OperationalClosureSmokeTests.cs`
- `OperationsApiController.cs`
- configuración de routing
- atributos `[Route]`
- `[HttpPost]`
- rutas de métodos
- DTO utilizado
- middleware relacionado
- configuración de endpoints

Construir la ruta efectiva del controlador y compararla EXACTAMENTE con la URL utilizada por el smoke test.

Determinar:

1. qué endpoint espera el test;
2. qué endpoint expone realmente el controlador;
3. qué verbo acepta;
4. si existe diferencia de ruta;
5. si existe diferencia singular/plural;
6. si falta `[HttpPost]`;
7. si el test está desactualizado;
8. si el endpoint fue reemplazado por otro flujo;
9. si autenticación produce un problema independiente;
10. si el test está golpeando otro endpoint por routing.

NO MODIFICAR NADA hasta poder explicar la causa concreta.

---

# FASE 4 — DETERMINAR QUIÉN ESTÁ EQUIVOCADO

No asumir que el test tiene razón.

No asumir que el controlador tiene razón.

Usar:

- arquitectura existente
- otros endpoints
- contratos/DTO
- pruebas existentes
- documentación
- llamadas desde UI
- convenciones del proyecto

para determinar cuál es el contrato correcto.

Si el test está obsoleto: CORREGIR EL TEST.

Si el controlador está incorrecto: CORREGIR EL CONTROLADOR.

Si ambos están inconsistentes: restaurar un contrato único coherente y actualizar solamente las partes necesarias.

Explicar la evidencia antes de realizar el cambio.

---

# FASE 5 — MYSQL

La ejecución anterior indicó que:

- MySQL80 está ejecutándose.
- integración/E2E se omiten por configuración faltante.

Investigar esto realmente.

NO aceptar simplemente: "tests skipped because MySQL configuration is missing".

Buscar:

- `appsettings*.json`
- configuración de tests
- fixtures
- environment variables
- connection strings
- scripts SQL
- migrations
- `docker-compose*`
- README/documentación
- setup de integración

Determinar qué necesitan exactamente las pruebas.

NO borrar bases.
NO destruir datos.
NO ejecutar migraciones destructivas sin comprobarlas.
NO inventar credenciales.

Si existe configuración local segura ya prevista por el proyecto, utilizarla.

Si falta un secreto/credencial que no puede deducirse legítimamente, documentar exactamente qué falta.

---

# FASE 6 — BUILD

Ejecutar desde la raíz:

```bash
dotnet restore
dotnet build -c Release
```

Objetivo:

- 0 errores
- 0 warnings relevantes introducidos por nuestros cambios

Si falla: INVESTIGAR → CORREGIR CAUSA → REPETIR.

---

# FASE 7 — TESTS

Ejecutar TODA la solución:

```bash
dotnet test -c Release --no-build
```

Después identificar proyectos de:

- Unit
- Integration
- Smoke
- E2E

Ejecutarlos explícitamente cuando corresponda.

NO aceptar un resumen general si oculta tests skipped.

Reportar PASS / FAIL / SKIP por proyecto.

Para cada SKIP explicar exactamente por qué.

---

# FASE 8 — PRUEBA DEL 405

Reproducir específicamente el fallo original.

La validación final debe demostrar que:

`POST /api/operations/customers`

o la ruta correcta determinada por el contrato YA NO devuelve 405 cuando la solicitud es válida y autenticada correctamente.

No eliminar autenticación para conseguirlo.

Validar también comportamiento sin autorización.

Una solicitud no autorizada debe continuar siendo rechazada según el diseño del sistema.

---

# FASE 9 — REGRESIÓN DE SEGURIDAD

Buscar:

```text
Authorize
AllowAnonymous
UseAuthentication
UseAuthorization
AddAuthentication
AddAuthorization
```

Comprobar que la corrección NO debilitó el modelo de seguridad.

Revisar específicamente todos los controladores API modificados.

No debe existir una exposición accidental causada por esta sesión.

---

# FASE 10 — DIFF FINAL

Ejecutar:

```bash
git status
git diff --stat
git diff
```

Revisar archivo por archivo.

Para CADA archivo modificado explicar:

- por qué cambió;
- qué defecto corrige;
- evidencia del defecto;
- cómo fue validado.

Eliminar únicamente modificaciones accidentales generadas POR ESTA SESIÓN.

NO borrar trabajo previo del usuario.

---

# CRITERIO DE TERMINACIÓN

NO terminar hasta cumplir, o demostrar técnicamente por qué no puede cumplirse:

- seguridad restaurada;
- `[Authorize]` preservado donde corresponde;
- causa real del 405 identificada;
- causa real corregida;
- build Release correcto;
- unit tests ejecutados;
- integration tests ejecutados cuando la infraestructura esté disponible;
- smoke tests ejecutados;
- E2E ejecutados cuando sea técnicamente posible;
- ningún test alterado artificialmente para pasar;
- ningún cambio masivo injustificado;
- diff final auditado;
- ninguna regresión de seguridad conocida.

Si algo está bloqueado externamente, NO improvisar una solución insegura.

Reportar el bloqueo con evidencia.

---

# INFORME FINAL OBLIGATORIO

Al terminar entregar:

## ROOT CAUSE
Causa exacta del 405.

## SECURITY REPAIR
Qué `[Authorize]` fueron restaurados y cómo se verificó su ubicación original.

## CHANGES
Archivo → modificación → razón.

## BUILD
Resultado exacto.

## TESTS
Proyecto → Passed / Failed / Skipped.

## MYSQL / INTEGRATION
Estado y configuración encontrada.

## SECURITY VALIDATION
Resultado de autenticación/autorización después de la reparación.

## RESIDUAL RISKS
Únicamente riesgos reales pendientes.

## GIT STATUS
Archivos modificados restantes.

## VERDICT
Uno solamente:

READY
PARTIALLY READY
BLOCKED

NO COMMIT.
NO PUSH.

Detente al presentar este informe para revisión humana.
