# ATLASNOC_TEST_CONNECTION.md

# $env:ATLASNOC_TEST_CONNECTION="Server=localhost;Port=3306;Database=atlasnoc_integration_test;User=<TEST_USER>;<SECRET>;"
dotnet test -c Release

## Estado real de AtlasNOC — cierre quirúrgico obligatorio

**Repositorio:** `akamike17/AtlasNOC`  
**Rama:** `deepseek-rebuild`  
**HEAD revisado:** `2699aeb796dca6b89e3fb5908a25dc99952d9c36`  
**Fecha de revisión:** 2026-09-09  
**Uso de este archivo:** este documento REEMPLAZA el veredicto anterior. Debe usarse como plan de corrección y, al terminar, actualizarse con evidencia real.  
**No hacer merge a `main` hasta cumplir todos los criterios de cierre.**

---

# 0. Veredicto actual

## NOT READY

El estado `READY WITH DOCUMENTED NON-BLOCKING LIMITATIONS` del documento anterior no es correcto todavía.

La revisión del HEAD `2699aeb` encontró una regresión de autorización introducida al corregir el bypass original:

- `ApiScopeAuthorizationHandler` ahora rechaza correctamente a humanos para requisitos de scope.
- Sin embargo, salvo `DiscoveryApiController`, el resto de los controladores API siguen usando directamente políticas `ApiScopes.*`.
- Resultado: usuarios humanos autenticados por cookie NO pueden satisfacer esas políticas.
- La matriz escrita en el `FINAL_HOSTILE_AUDIT.md` anterior afirma que humanos pueden usar varias APIs, pero el código actual no lo permite.
- Esto puede romper UI que consuma `/api/*` mediante cookie y contradice la auditoría final.

Además, la rama todavía no tiene Integration/Runtime/E2E ejecutados realmente: siguen en SKIP por falta de `ATLASNOC_TEST_CONNECTION`.

---

# 1. HALLAZGO HIGH — autorización humana rota en casi toda `/api/*`

## Dónde está

### Handler correcto pero incompleto en integración

`src/AtlasNOC.Web/Security/ApiScopeAuthorizationHandler.cs`

El comportamiento actual es correcto para un requisito de scope:

```csharp
// Humano (cookie): NO satisface scope.
return Task.CompletedTask;
```

Eso evita el bypass original.

### El problema

La corrección sólo creó políticas combinadas para Discovery:

`src/AtlasNOC.Web/Security/ApiKeyAuthenticationExtensions.cs`

- `DiscoveryRunPolicy`
- `DiscoveryReadPolicy`

Pero los demás controladores continúan usando scopes directos, por ejemplo:

`src/AtlasNOC.Web/Controllers/Api/TopologyApiController.cs`

```csharp
[Authorize(
    AuthenticationSchemes = "Identity.Application,ApiKey",
    Policy = ApiScopes.TopologyRead)]
```

`src/AtlasNOC.Web/Controllers/Api/SitesApiController.cs`

```csharp
[Authorize(Policy = ApiScopes.SitesRead)]
[Authorize(Policy = ApiScopes.SitesWrite)]
```

`src/AtlasNOC.Web/Controllers/Api/SubscribersApiController.cs`

```csharp
[Authorize(Policy = ApiScopes.SubscribersRead)]
[Authorize(Policy = ApiScopes.SubscribersWrite)]
```

Revisar igualmente:

- `DevicesApiController.cs`
- `MetricsApiController.cs`
- `AlertsApiController.cs`
- `IncidentsApiController.cs`
- `IntegrationsApiController.cs`
- `SystemApiController.cs`
- `TopologyApiController.cs`
- `SitesApiController.cs`
- `SubscribersApiController.cs`
- cualquier nuevo controller bajo `Controllers/Api`.

## Por qué falla

`ApiScopeRequirement` sólo puede aprobarse para principal `auth_type=api_key`.

Un usuario de cookie, aunque tenga `Administrator`, `NocOperator` o `ReadOnly`, no satisface `ApiScopes.TopologyRead`, `SitesRead`, `SitesWrite`, etc.

Por tanto, la frase del audit anterior:

> “Humanos nunca satisfacen scope requirements (usan HumanRoleAuthorizationHandler)”

es incompleta: `HumanRoleAuthorizationHandler` sólo sirve si la política realmente contiene `HumanRoleRequirement`, y la mayoría de los endpoints no lo contiene.

## Qué NO hacer

NO poner dos atributos como:

```csharp
[Authorize(Policy = ApiScopes.SitesWrite)]
[Authorize(Policy = "Human.NocOperatorOrAdmin")]
```

Eso aplica AND, no OR, y bloquearía tanto API keys como humanos.

## Corrección requerida

Implementar UN modelo genérico y centralizado de permiso API con semántica:

```text
(API key válida con scope requerido)
OR
(usuario cookie válido con uno de los roles permitidos)
```

No copiar `RequireAssertion` distinto en cada controller.

### Solución recomendada

Crear:

`src/AtlasNOC.Web/Security/ApiPermissionRequirement.cs`

con:

- `string Scope`
- `IReadOnlyCollection<string> HumanRoles`

Crear:

`src/AtlasNOC.Web/Security/ApiPermissionAuthorizationHandler.cs`

Reglas:

1. Si no autenticado → fail.
2. Si `auth_type=api_key`:
   - comprobar scope exacto;
   - aceptar `*` y `*.*` si esa es la semántica actual;
   - NO comprobar roles.
3. Si humano:
   - comprobar exclusivamente los roles permitidos;
   - NO tratarlo como portador de scopes.
4. No mezclar identidades.

Crear políticas centralizadas, por ejemplo:

```text
Api.TopologyRead
Api.MetricsRead
Api.DevicesRead
Api.SitesRead
Api.SitesWrite
Api.AlertsRead
Api.AlertsWrite
Api.IncidentsRead
Api.IncidentsWrite
Api.SubscribersRead
Api.SubscribersWrite
Api.IntegrationsRead
Api.IntegrationsWrite
Api.SystemRead
Api.DiscoveryRead
Api.DiscoveryRun
```

### Roles mínimos esperados

Lectura operacional:

```text
ReadOnly
NocOperator
Administrator
```

Mutaciones NOC:

```text
NocOperator
Administrator
```

Administración sensible:

```text
Administrator
```

Si la especificación existente define otra matriz, obedecerla y documentarla, pero no inventar permisos.

## Cuándo se considera corregido

Sólo cuando TODAS las rutas `/api/*` estén inventariadas y cada acción tenga prueba de:

- anónimo;
- cookie ReadOnly;
- cookie NocOperator;
- cookie Administrator;
- API key sin scope;
- API key con scope;
- API key revocada;
- API key expirada cuando aplique.

---

# 2. HALLAZGO HIGH — el audit final documenta permisos que el código no implementa

## Dónde

`docs/FINAL_HOSTILE_AUDIT.md`

La matriz dice, entre otros:

```text
/api/topology/graph + Humano + ReadOnly/NocOperator/Administrator
/api/sites GET/POST + Humano
/api/metrics + Humano
/api/devices + Humano
```

Pero el código actual de esos controllers usa scopes directos y los humanos no satisfacen scopes.

## Corrección

NO editar el documento sólo para acomodarlo al fallo.

Primero corregir autorización real.

Después generar la matriz desde una inspección completa de todos los controllers.

El documento final debe reflejar exactamente el código probado.

---

# 3. HALLAZGO MEDIUM/HIGH DE TEST — las pruebas de autorización validan handlers aislados, no endpoints reales

## Dónde

`tests/AtlasNOC.Tests.Unit/ApiScopeAuthorizationTests.cs`

Actualmente se prueba correctamente que:

- API key con scope → éxito;
- API key sin scope → fallo;
- humano NO satisface `ApiScopeRequirement`.

Pero no se prueba que el endpoint real acepte al humano mediante una política OR.

Por eso el test pasa mientras `/api/topology`, `/api/sites`, `/api/subscribers`, etc. pueden devolver 403 a cookies válidas.

## Corrección obligatoria

Agregar tests de política completa y E2E por endpoint representativo.

Como mínimo:

```text
GET  /api/topology/graph
GET  /api/devices
GET  /api/metrics
GET  /api/sites
POST /api/sites
GET  /api/subscribers
POST /api/subscribers
GET  /api/alerts
acción mutable de alerts si existe
GET  /api/incidents
acción mutable de incidents si existe
GET  /api/system/health
POST /api/discovery/run
POST /api/discovery/runs/{id}/cancel
```

Para cada permiso, validar la matriz real.

No basta con instanciar directamente el handler: usar `AuthorizationService` con políticas registradas y, para una muestra representativa, `WebApplicationFactory`.

---

# 4. HALLAZGO MEDIUM — `HumanRoleAuthorizationHandler` está prácticamente desacoplado de las APIs

## Dónde

`src/AtlasNOC.Web/Security/HumanRoleAuthorizationHandler.cs`

Se registran:

- `Human.Administrator`
- `Human.NocOperatorOrAdmin`
- `Human.ReadOnlyOrOperatorOrAdmin`

Pero la mayoría de los controllers API no usan estas políticas, y la combinación OR sólo existe manualmente para Discovery.

## Corrección

Después de crear `ApiPermissionRequirement`, decidir una de estas dos opciones:

### Preferida

Eliminar la duplicación conceptual y usar un único handler de `ApiPermissionRequirement`.

### Alternativa aceptable

Mantener handlers separados, pero construir una política OR real mediante un requirement/handler compuesto.

No mantener tres mecanismos paralelos:

- `ApiScopeRequirement`
- `HumanRoleRequirement`
- `RequireAssertion` manual por endpoint

porque se volverán a desalinear.

---

# 5. HALLAZGO MEDIUM — entorno `Testing` exige certificado Data Protection

## Dónde

`src/AtlasNOC.Web/Program.cs`

La lógica actual es:

```csharp
if (!builder.Environment.IsDevelopment())
{
    // certificado obligatorio
}
```

Esto incluye:

```text
Production
Staging
Testing
cualquier entorno distinto de Development
```

El requisito anterior pedía permitir un comportamiento explícitamente controlado para Development/Test.

Si `WebApplicationFactory` usa `Testing`, cuando por fin se active `ATLASNOC_TEST_CONNECTION` puede fallar el arranque antes de ejecutar E2E si no se inyecta también certificado/thumbprint.

## Qué hacer

Hacer la intención explícita.

### Producción

Debe fallar cerrado:

```text
Production:
thumbprint vacío -> FAIL
cert no encontrado -> FAIL
cert válido -> PASS
```

### Testing

Elegir una estrategia segura y reproducible:

- permitir Data Protection efímero sólo en `Testing`; o
- inyectar un protector/certificado de test; o
- configurar un mecanismo de test aislado.

No debilitar Production.

Agregar prueba de configuración/startup para:

```text
Development
Testing
Production sin thumbprint
Production thumbprint inválido
```

---

# 6. HALLAZGO MEDIUM — secret scan fue debilitado excluyendo documentos completos

## Dónde

`tests/AtlasNOC.Tests.Unit/VersionedConnectionStringSecretsTests.cs`

Los commits recientes agregaron exclusiones completas para:

```text
F11_GAP_AUDIT.md
FINAL_HOSTILE_AUDIT.md
```

porque esos documentos contienen ejemplos de connection string.

Esto crea un agujero: un secreto real podría entrar en esos archivos y el test no lo vería.

## Corrección

No excluir documentos completos.

Preferir:

1. ejemplos obviamente ficticios sin secretos utilizables;
2. patrón/redacción segura;
3. sanitización;
4. allowlist de una línea o token placeholder, no de todo el archivo.

Ejemplo documental permitido:

```text
Server=localhost;Database=atlasnoc_e2e_test;User=<TEST_USER>; ... <credencial en variable de entorno>
```

El scanner debe seguir inspeccionando `docs/*.md`.

---

# 7. HALLAZGO MEDIUM — el `FINAL_HOSTILE_AUDIT.md` está desfasado respecto al HEAD

## Evidencia

El documento dice:

```text
HEAD: 531bbff
```

pero la rama revisada está en:

```text
2699aeb796dca6b89e3fb5908a25dc99952d9c36
```

Y el propio documento se agregó en un commit posterior.

## Corrección

Al final del trabajo:

```powershell
git rev-parse HEAD
git status --short
```

Actualizar:

- HEAD inicial de esta pasada;
- HEAD final;
- commits nuevos;
- archivos tocados;
- resultados reales.

No dejar un audit que se audita a sí mismo desde un commit anterior.

---

# 8. BLOQUEANTE DE CIERRE — ejecutar MySQL real de pruebas

Ya existe infraestructura MySQL local disponible para el proyecto. No aceptar “falta MySQL” sin intentar usar una base DEDICADA de test.

## Dónde

Crear una base separada, nunca reutilizar la DB normal de AtlasNOC.

Nombre recomendado:

```text
atlasnoc_integration_test
```

o:

```text
atlasnoc_e2e_test
```

Debe contener `_test` o `_e2e`.

## Cuándo

DESPUÉS de corregir la autorización API y los tests de startup/Testing.

No ejecutar antes, porque la suite E2E debe validar el código ya corregido.

## Cómo

### 1. Crear DB dedicada

Usar el MySQL local ya configurado para AtlasNOC.

No guardar usuario/password en repo.

### 2. Definir variable sólo en la sesión PowerShell de pruebas

```powershell
$env:ATLASNOC_TEST_CONNECTION = "Server=localhost;Port=3306;Database=atlasnoc_integration_test;User=<TEST_USER>;" + $TU_PASSWORD_AQUI
```

### 3. Confirmar seguridad antes de ejecutar

El test/helper debe imprimir sólo:

```text
Database=atlasnoc_integration_test
```

Nunca la connection string completa.

### 4. Ejecutar por suite primero

```powershell
dotnet test tests/AtlasNOC.Tests.Integration/AtlasNOC.Tests.Integration.csproj -c Release
dotnet test tests/AtlasNOC.Tests.Runtime/AtlasNOC.Tests.Runtime.csproj -c Release
dotnet test tests/AtlasNOC.Tests.E2E/AtlasNOC.Tests.E2E.csproj -c Release
```

Arreglar fallos reales. No convertirlos en skip.

### 5. Ejecutar solución completa

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

Objetivo:

```text
Unit:         0 failed, 0 skipped injustificados
Integration:  8/8 PASS o nuevo conteo documentado
Runtime:      7/7 PASS o nuevo conteo documentado
E2E:         13/13 PASS o nuevo conteo documentado
```

Si cambia el número por nuevas pruebas, documentar el número nuevo.

---

# 9. E2E quirúrgico de autorización — obligatorio antes de READY

Con DB de test activa, crear usuarios reales de cada rol:

```text
ReadOnly
NocOperator
Administrator
```

Probar con cookie real, no `ClaimsPrincipal` artificial.

## Matriz mínima

| Endpoint | ReadOnly | NocOperator | Administrator | API key scope correcto | API key scope incorrecto |
|---|---:|---:|---:|---:|---:|
| GET topology | 200 | 200 | 200 | 200 | 403 |
| GET devices | 200 | 200 | 200 | 200 | 403 |
| GET metrics | 200 | 200 | 200 | 200 | 403 |
| GET sites | 200 | 200 | 200 | 200 | 403 |
| POST sites | 403 | permitido según matriz | permitido | 2xx | 403 |
| GET subscribers | 200 | 200 | 200 | 200 | 403 |
| POST subscribers | 403 | permitido según matriz | permitido | 2xx | 403 |
| GET discovery runs | 200 | 200 | 200 | 200 | 403 |
| POST discovery run | 403 | 2xx | 2xx | 2xx | 403 |
| POST discovery cancel | 403 | permitido | permitido | permitido | 403 |

Para Alerts/Incidents/Integrations/System completar la matriz según sus operaciones reales.

También:

- anónimo → 401/redirect según superficie;
- API key revocada → 401/403 según pipeline;
- API key expirada → 401/403;
- usuario `IsActive=false` → cookie rechazada.

---

# 10. Evidence UI sigue pendiente y no debe figurar como “completa”

El audit anterior reconoce:

```text
Evidence UI detalle link — pendiente
```

pero lo clasifica como LOW/no bloqueante sin verificar el requisito funcional original.

## Dónde revisar

- `src/AtlasNOC.Web/Controllers/LinksController.cs`
- `src/AtlasNOC.Web/Views/Links/Detail.cshtml`
- topología/detalle relacionado
- `NeighborObservation`
- `RawEvidenceHash`

## Qué debe mostrar

Para un link automático:

- protocolo: LLDP/CDP/ARP/manual;
- dispositivo e interfaz origen;
- dispositivo/interfaz destino si resuelta;
- timestamp de observación;
- estado de correlación;
- confianza;
- evidencia/hash útil sin exponer secretos.

No basta con que el backend persista evidencia.

Si la especificación exige trazabilidad visible, completar antes de `READY`.

---

# 11. `xunit.abstractions` — resolver o justificar con evidencia

El audit dice que `xunit.abstractions v2.0.3` es “preexistente” y no bloquea.

No dejarlo ambiguo.

Ejecutar:

```powershell
dotnet restore
dotnet list package
```

Si falta realmente:

- agregar referencia donde corresponda;
- restaurar;
- compilar;
- ejecutar tests.

Si NO falta y todo restaura correctamente:

- eliminarlo de pendientes del documento.

No conservar una deuda ficticia sólo porque un documento viejo la mencionó.

---

# 12. Revisión adicional — rate limiter

En `Program.cs` revisar esta selección de partition key:

```csharp
httpContext.User.Identity?.Name
    ?? httpContext.Request.Headers["X-Api-Key"].ToString()
    ?? httpContext.Connection.RemoteIpAddress?.ToString()
    ?? "unknown"
```

`Headers["X-Api-Key"].ToString()` produce string vacío cuando no existe, no `null`.

Consecuencia posible: solicitudes sin API key pueden caer en la misma partición `""` en vez de IP.

Además se usa la API key en claro como partition key en memoria.

## Corregir

Usar:

- identidad estable del API key (`api_key_id`) después de autenticación; o
- hash seguro;
- IP como fallback real cuando no exista key/identity.

Agregar test de particionado si la implementación se extrae a helper.

---

# 13. Orden exacto de trabajo

No improvisar. Ejecutar en este orden:

```text
1. Confirmar HEAD y git status.
2. Reproducir 403 de cookie humana en Topology/Sites/Subscribers.
3. Diseñar ApiPermissionRequirement genérico OR.
4. Migrar TODAS las políticas /api/* al modelo nuevo.
5. Eliminar duplicación/manual Discovery cuando el modelo genérico lo sustituya.
6. Agregar tests de políticas completas.
7. Corregir comportamiento Data Protection para Testing sin debilitar Production.
8. Corregir secret scan: no excluir documentos completos.
9. Corregir rate-limiter partition key.
10. Build Release + Unit.
11. Completar Evidence UI si la spec exige trazabilidad visible.
12. Resolver/verificar xunit.abstractions.
13. Crear/usar DB MySQL `_test`.
14. Ejecutar Integration.
15. Ejecutar Runtime.
16. Ejecutar E2E.
17. Corregir cualquier fallo real y repetir desde la suite afectada.
18. Ejecutar `dotnet restore`.
19. Ejecutar `dotnet build -c Release`.
20. Ejecutar `dotnet test -c Release`.
21. Auditar nuevamente controllers /api/* contra matriz.
22. Actualizar ESTE `docs/FINAL_HOSTILE_AUDIT.md` con resultados reales.
23. `git status` limpio.
24. Commits temáticos.
25. Push a `deepseek-rebuild`.
26. NO mergear a main.
```

---

# 14. Commits recomendados

Separar, mínimo:

```text
fix(auth): unify API scope-or-human-role authorization
test(auth): cover API permission matrix for cookie and API key
fix(testing): make Data Protection testing path explicit and safe
test(security): keep docs inside secret scan
fix(rate-limit): avoid raw api keys and empty shared partition
feat(ui): expose topology link evidence details
test(e2e): run real MySQL integration runtime and authorization flows
docs: finalize hostile audit with executed evidence
```

No es obligatorio usar exactamente estos mensajes, pero mantener separación temática.

---

# 15. Criterio final de READY

Sólo cambiar este documento a:

```text
READY
```

si se cumplen TODOS:

- 0 CRITICAL pendientes;
- 0 HIGH pendientes;
- autorización API humana/API key probada en endpoint real;
- `ReadOnly` no puede mutar;
- NocOperator/Admin respetan matriz;
- API key sin scope no pasa;
- revocada/expirada no pasa;
- Production Data Protection falla cerrado;
- Testing arranca de forma segura y reproducible;
- secret scan sigue cubriendo docs;
- MySQL Integration ejecutado realmente;
- Runtime ejecutado realmente;
- E2E ejecutado realmente;
- migraciones aplicadas a DB de test;
- build Release 0 errores;
- warnings explicados o 0;
- no skips usados para esconder fallos;
- evidence UI alineada a la spec o explícitamente demostrada como fuera de alcance;
- `git status` limpio;
- `FINAL_HOSTILE_AUDIT.md` apunta al HEAD final real.

Si cualquier suite crítica sigue SKIP:

```text
NOT READY
```

o, sólo si el código está completo y la limitación es verdaderamente externa y aceptada:

```text
READY WITH DOCUMENTED BLOCKING TEST LIMITATIONS
```

No usar `READY WITH DOCUMENTED NON-BLOCKING LIMITATIONS` para Integration/Runtime/E2E no ejecutados si esas suites son la evidencia principal del cierre.

---

# 16. Resumen para ejecutar sin releer todo

## Corregir YA

1. Autorización OR genérica en TODAS las APIs.
2. Tests de endpoint real para cookie + API key.
3. `Testing` y Data Protection.
4. Secret scan sin exclusiones completas de auditorías.
5. Rate-limit partition key sin API key cruda ni `""`.

## Después

6. Evidence UI.
7. xunit.abstractions real/falso pendiente.
8. MySQL `_test`.
9. Integration + Runtime + E2E reales.
10. Re-auditar y actualizar este mismo archivo.

## No hacer

- no merge a `main`;
- no declarar READY por Unit;
- no contar SKIP como PASS;
- no ocultar fallos con nuevos excludes;
- no poner dos `[Authorize]` esperando OR;
- no tocar DB normal de AtlasNOC con fixtures destructivas.
