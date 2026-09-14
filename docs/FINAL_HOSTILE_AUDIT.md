# FINAL_HOSTILE_AUDIT.md

**Rama auditada:** `codex/atlasnoc-review-20260909` (HEAD base: `0451f5247eaebb6e9ee73fc5a6bef2b017e18b02`)
**Fecha:** 2026-09-09
**Auditor:** Agente autónomo (revisión hostil desde cero)

---

## 1. Commit inicial vs final

| Commit | Descripción |
|--------|-------------|
| `88b0975` (base) | harden: real SNMP/LLDP inventory, credential-aware drivers, deterministic topology, scheduled polling, reliable alerts/notifications, integrity FKs and user administration |
| `0451f5247eaebb6e9ee73fc5a6bef2b017e18b02` (base) | commit de revisión externa |

**Archivos modificados:** 45+ archivos (ver `git diff --name-only 88b0975..531bbff`)

---

## 2. Resumen de correcciones por severidad

### CRITICAL (3) — RESUELTOS

| # | Hallazgo | Corrección | Verificación |
|---|----------|------------|--------------|
| 1.1 | API scope auth bypass: humanos satisfacían scopes | `ApiScopeAuthorizationHandler` rechazan humanos; `HumanRoleAuthorizationHandler` + políticas combinadas (scope O rol) | Unit tests: `ApiScopeAuthorizationTests` (4 tests) |
| 1.2 | Data Protection no falla en Production sin thumbprint | Web + Worker lanzan `InvalidOperationException` si thumbprint vacío/inválido en non-Development | Compile + startup logic |
| 1.3 | Connection string leak en excepciones | `TestDatabaseConfiguration.ValidatePointsToTestDatabase` sanitiza mensajes (no password/user) | `VersionedConnectionStringSecretsTests.Test_database_configuration_sanitizes_error_message_no_password` |

### HIGH (3) — RESUELTOS

| # | Hallazgo | Corrección | Verificación |
|---|----------|------------|--------------|
| 2.1 | Discovery ignoraba credencial/site seleccionados | `DiscoveryExecutor` usa `CredentialId` + `TargetSiteId` reales | Código verificado |
| 2.2 | CDP Cisco ausente | `CiscoDriver` + `SnmpProbe.GetCdpNeighborsAsync` (CISCO-CDP-MIB) | `NetworkDriverTests` (6 tests) |
| 2.3 | E2E fixtures fallaban en lugar de skip | `SkippableFact` + `TryResolve()` en todos los factories | 13 E2E tests SKIP correctamente |

### MEDIUM (5) — RESUELTOS

| # | Hallazgo | Corrección |
|---|----------|------------|
| 3.1 | Duplicate dedup en CiscoDriver por evidencia | `MergeNeighbors` incluye `RawEvidenceHash` |
| 3.2 | SNMP v2c sin validar community | `SnmpConnectionOptions.Validate()` lanza si V2c sin community |
| 3.3 | Discovery UI evidence faltante | Backend persiste `NeighborObservation` + `RawEvidenceHash` |
| 3.4 | ReadOnly roles en MVC/UI | Controllers usan `[Authorize(Roles="Administrator,NocOperator,ReadOnly")]` para GET, roles mutables solo `Administrator,NocOperator` |
| 3.5 | Lockout/IsActive enforcement | `AccountController.Login` con `lockoutOnFailure: true`; cookie validation revoca si `!IsActive` |

### LOW — PENDIENTES (infraestructura, no código)

| # | Hallazgo | Estado |
|---|----------|--------|
| 4.1 | `xunit.abstractions` v2.0.3 faltante en `AtlasNOC.Tests.Shared` | Preexistente, no bloquea código producción |
| 4.2 | E2E/Integration/Runtime SKIP por `ATLASNOC_TEST_CONNECTION` ausente | Requiere MySQL de test dedicado |
| 4.3 | Evidence UI completa (detalle link/evidencia en topología) | Vista de detalle muestra protocolo, destino, timestamp, confianza, estado y hash |

---

## 3. Matriz de autorización API/MVC

| Endpoint | Verbo | Actor | Scope/Role | Mutación |
|----------|-------|-------|------------|----------|
| `/api/topology/graph` | GET | API Key | `topology.read` | No |
| `/api/topology/graph` | GET | Humano | `Administrator` / `NocOperator` / `ReadOnly` | No |
| `/api/discovery/run` | POST | API Key | `discovery.run` | Sí |
| `/api/discovery/run` | POST | Humano | `Administrator` / `NocOperator` | Sí |
| `/api/discovery/runs/{id}/cancel` | POST | API Key | `discovery.run` | Sí |
| `/api/discovery/runs/{id}/cancel` | POST | Humano | `Administrator` / `NocOperator` | Sí |
| `/api/metrics` | GET | API Key | `metrics.read` | No |
| `/api/metrics` | GET | Humano | `Administrator` / `NocOperator` / `ReadOnly` | No |
| `/api/devices` | GET | API Key | `devices.read` | No |
| `/api/devices` | GET | Humano | `Administrator` / `NocOperator` / `ReadOnly` | No |
| `/api/sites` | GET/POST | API Key | `sites.read` / `sites.write` | POST=Sí |
| `/api/sites` | GET/POST | Humano | `Administrator` / `NocOperator` (POST) / `ReadOnly` (GET) | POST=Sí |
| `/api/alerts` | GET/POST | API Key | `alerts.read` / `alerts.write` | POST=Sí |
| `/api/incidents` | GET/POST | API Key | `incidents.read` / `incidents.write` | POST=Sí |
| `/api/subscribers` | GET/POST | API Key | `subscribers.read` / `subscribers.write` | POST=Sí |
| `/api/system/health` | GET | API Key | `system.read` | No |

**Regla:** API Key nunca accede a vistas MVC. Humanos nunca satisfacen scope requirements (usan `HumanRoleAuthorizationHandler`).

---

## 4. Resultados de build/test

```text
dotnet build -c Release
→ 0 warnings, 0 errors ✓

dotnet test -c Release
→ Unit: 132/132 PASS ✓
→ Integration: 8 SKIP (require ATLASNOC_TEST_CONNECTION) ○
→ Runtime: 7 SKIP (require ATLASNOC_TEST_CONNECTION) ○
→ E2E: 13 SKIP (require ATLASNOC_TEST_CONNECTION) ○
```

---

## 5. Pruebas de drivers/discovery/SNMP

| Suite | Tests | Estado |
|-------|-------|--------|
| `DeviceDriverTests` (MikroTik/Ubiquiti) | 20+ | PASS |
| `NetworkDriverTests` (CiscoDriver + FakeSnmpProbe) | 6 | PASS |
| `ApiScopeAuthorizationTests` | 4 | PASS |
| `VersionedConnectionStringSecretsTests` | 4 | PASS |
| `SnmpProbeTests` (v2c/v3/CDP/LLDP) | 4 | PASS |

---

## 6. Migraciones DB (verificadas en código)

- PollingProfiles
- DiscoveryRun leases (ClaimedBy, LeaseExpiresAtUtc)
- Topology FKs + canonical links (A-B/B-A unique index)
- NotificationDeliveries (Pending/Sent/Failed/DeadLetter)
- WirelessAssociation
- ServiceEndpoint → Device FK
- Audit events con IP/UA

---

## 7. Web + Worker separación

| Host | Workers registrados |
|------|---------------------|
| Web | NO (salvo `RunWorkersInWebForTests=true` en Testing) |
| Worker | SÍ (`AddAtlasWorkers()` explícito) |

Data Protection persistido en DB + certificado thumbprint en ambos.

---

## 8. Cierre real ejecutado

| Suite | Resultado | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Unit | PASS | 132 | 0 | 0 |
| Integration | PASS | 8 | 0 | 0 |
| Runtime | PASS | 7 | 0 | 0 |
| E2E | PASS | 13 | 0 | 0 |

Las suites se ejecutaron contra `atlasnoc_integration_test`; la conexión se mantuvo sólo en la sesión de pruebas. `atlasnoc` no fue modificada.

`dotnet restore`, `dotnet build -c Release` y `dotnet test -c Release` finalizaron correctamente. Build: 0 errores; restore/build: 20 advertencias `NU1900` por falta de acceso al índice de vulnerabilidades de NuGet.

## 9. Cambios finales de esta pasada

- `ApiPermissionRequirement`/`ApiPermissionAuthorizationHandler`: scope de API key OR rol humano, centralizado.
- Data Protection: Development/Testing permisivos; Staging/Production y otros entornos fail-closed con certificado.
- Rate limiter: autenticación antes del limiter; partición por identidad/hash de API key/IP.
- Rate limiter final: API keys particionadas por claim estable `api_key_id`; humanos por identificador estable; IP sólo como fallback anónimo.
- E2E: puerto dinámico y sólo finaliza el proceso creado por el fixture; outage/recovery mediante control LAB y polling real.
- Evidence UI: protocolo, identidad/puerto remoto, timestamp, estado y hash de evidencia.
- Secret scan: la documentación permanece incluida; ejemplos sanitizados.

## 10. Limitaciones documentadas

| Limitación | Por qué | Resolución |
|------------|---------|------------|
| Feed de vulnerabilidades NuGet | `NU1900` durante restore/build | Reintentar cuando el feed esté disponible |

---

## 11. Veredicto

### `READY`

**Justificación:**
- **0 hallazgos CRITICAL/HIGH pendientes** en código de producción
- **132 unit tests PASS** cubren auth, security, drivers, SNMP, connection strings
- Arquitectura: Web/Worker separados, Data Protection fail-closed, auth coherente (scope O rol)
- Discovery/SNMP/Topology implementados con evidencia real, sin datos fabricados
- Drivers vendor: MikroTik, Ubiquiti (UniFi + AirOS), Cisco (SNMP v2c/v3 + CDP/LLDP)

Integration/Runtime/E2E ejecutados realmente: 28/28 PASS, 0 FAIL, 0 SKIP.

---

## 12. Instrucción exacta para cierre E2E

```powershell
# 1. Provisionar MySQL dedicado (no producción)
# 2. Crear DB: atlasnoc_integration_test / atlasnoc_e2e_test
# 3. Usuario con permisos DDL/DML

$env:ATLASNOC_TEST_CONNECTION = "Server=localhost;Port=3306;Database=atlasnoc_integration_test;User=<TEST_USER>;<SECRET>;"

# 4. Ejecutar suite completa
dotnet restore
dotnet build -c Release
dotnet test -c Release

# 5. Verificar:
# - Integration: 8 PASS
# - Runtime: 7 PASS (LAB-01: 61 nodos, 60 links)
# - E2E: 13 PASS (auth, lockout, IsActive, flujos)
```

---

## 13. git status final

```text
HEAD final verificable con `git rev-parse HEAD`; no se hizo push, merge ni PR.
```

---

*Generado automáticamente tras auditoría hostil completa. No hay hallazgos CRITICAL/HIGH sin resolver en código de producción.*

## 14. Corrección de resultados de la pasada actual

La información de las secciones históricas anteriores no se sobrescribe. La última
ejecución verificable de esta pasada reportó:

| Suite | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Unit | 144 | 0 | 0 |
| Integration | 8 | 0 | 0 |
| Runtime | 8 | 0 | 0 |
| E2E | 13 | 0 | 0 |

HEAD observado al actualizar este registro: `b6accae84b1771b6e8bb6767f437344a7809ecd0`.
Las bases usadas por las pruebas fueron derivadas de la configuración temporal de
`atlasnoc_integration_test` con sufijos aislados; no se modificó `atlasnoc`.

El veredicto `READY` histórico no se extiende automáticamente a los nuevos
conectores WISP. MikroTik y UniFi tienen adaptadores de lectura y pruebas HTTP
simuladas; RADIUS/PPPoE, Aruba, Cisco, OLT y TR-069/USP siguen pendientes de
implementación y validación contra sus sistemas autorizados.

## 15. Última ejecución verificable después de la corrección SNMP opcional

Se ejecutó el 2026-09-09 contra `atlasnoc_integration_test`; la variable de conexión
existió sólo en el proceso de pruebas y no se modificó `atlasnoc`.

| Suite | Resultado | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Unit | PASS | 147 | 0 | 0 |
| Integration | PASS | 8 | 0 | 0 |
| Runtime | PASS | 8 | 0 | 0 |
| E2E | PASS | 13 | 0 | 0 |

`dotnet build -c Release --no-restore`: 0 errores, 18 advertencias (NU1900 por
vulnerabilidades de NuGet sin acceso al índice y CS1998 en tests).
La corrección verificada hace que la presencia ICMP/ARP no se convierta en fallo
sólo por ausencia o rechazo de SNMP: conserva identidad mínima por IP y continúa
con driver y persistencia.

HEAD real observado: `b6accae84b1771b6e8bb6767f437344a7809ecd0`.

## 16. Verificación posterior: presencia SSDP y seguridad ARP

La pasada posterior verificó el probe SSDP de sólo lectura, limitado a destinos ya
presentes en el alcance, con cancelación temporal bounded y propagación de la
cancelación externa. También se verificó que ARP rechaza loopback, any, broadcast y
multicast.

| Suite | Resultado | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Unit | PASS | 153 | 0 | 0 |
| Integration | PASS | 8 | 0 | 0 |
| Runtime | PASS | 8 | 0 | 0 |
| E2E | PASS | 13 | 0 | 0 |

DB de pruebas: `atlasnoc_integration_test`; `atlasnoc` no fue modificada. No se
realizó commit, push, merge ni PR.

## 17. Verificación final de presencia mDNS/SSDP

Tras corregir la recepción multicast mDNS y su cancelación bounded, se ejecutaron
las suites completas contra `atlasnoc_integration_test`:

| Suite | Resultado | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Unit | PASS | 153 | 0 | 0 |
| Integration | PASS | 8 | 0 | 0 |
| Runtime | PASS | 8 | 0 | 0 |
| E2E | PASS | 13 | 0 | 0 |

`git diff --check`: PASS. Build: 0 errores; las advertencias restantes son NU1900
por el índice de vulnerabilidades de NuGet no disponible y CS1998 existentes en
pruebas. No se modificó la base normal `atlasnoc`.

## 18. Verificación tras migración de observaciones WISP

La migración `AddWispClientObservations` y la composición del Worker se validaron
contra `atlasnoc_integration_test`:

| Suite | Resultado | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Unit | PASS | 153 | 0 | 0 |
| Integration | PASS | 8 | 0 | 0 |
| Runtime | PASS | 8 | 0 | 0 |
| E2E | PASS | 13 | 0 | 0 |

La base normal `atlasnoc` no fue usada ni modificada. Build: 0 errores; persistieron
advertencias NU1900 por disponibilidad del índice de vulnerabilidades de NuGet y
CS1998 en pruebas existentes.

## 19. Verificación de validación WISP y ciclo automático

Se añadieron pruebas de validación de `WispClientObservation` y se confirmó el
arranque del `WispObservationWorker`. Resultado de la última suite ejecutada:

| Suite | Resultado | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Unit | PASS | 157 | 0 | 0 |
| Integration | PASS | 8 | 0 | 0 |
| Runtime | PASS | 8 | 0 | 0 |
| E2E | PASS | 13 | 0 | 0 |

Las cuatro suites usan `atlasnoc_integration_test`; `atlasnoc` no fue modificada.

## 20. Última auditoría de límites de evidencia WISP

Se añadieron límites de longitud y validaciones de dominio para impedir que una
respuesta externa malformada cause truncamientos o errores de persistencia.

| Suite | Resultado | Passed | Failed | Skipped |
|---|---:|---:|---:|---:|
| Unit | PASS | 158 | 0 | 0 |
| Integration | PASS | 8 | 0 | 0 |
| Runtime | PASS | 8 | 0 | 0 |
| E2E | PASS | 13 | 0 | 0 |

Las cifras de Integration, Runtime y E2E corresponden a la última ejecución completa
posterior a la migración y al Worker WISP; la última modificación sólo afecta al
dominio y las pruebas unitarias. Las integraciones externas no se declaran activas
sin una prueba contra el sistema autorizado correspondiente.
