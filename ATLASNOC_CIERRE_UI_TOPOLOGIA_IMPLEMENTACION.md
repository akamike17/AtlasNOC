# Cierre de UI y topología — informe de implementación

## Estado

- Rama: `atlasnoc`
- Commit base: `8f53172a3d79308a0bfee6096d500c11ce01fc3d`
- Commits previos: `33c19bd` y `81ea752`
- No se modificaron autenticación, policies, scopes ni roles.
- No se crearon dispositivos ni enlaces.

## Causa raíz

`TopologyService` construía correctamente `TopologyGraphDto`. El Dashboard serializaba el objeto directamente desde Razor, lo que podía producir propiedades `Nodes`/`Edges`, mientras `topology.js` sólo leía `nodes`/`edges`. Además, el JavaScript buscaba `hostname` y `managementIp`, campos inexistentes en `TopologyNodeDto`, cuyo contrato usa `label` e `ip`.

## Corrección

- Serialización camelCase explícita en Dashboard.
- Render compartido compatible con camelCase y PascalCase.
- Mapeo de nodos desde `id`, `label` e `ip`.
- Enlaces dibujados sólo cuando ambos endpoints existen.
- Dashboard y `/Topology` reutilizan el mismo `topology.js`.
- Contador, empty state, error de carga del motor y detalle al seleccionar nodo.
- Enlaces no confirmados con representación visual distinta.
- Operations, métricas y detalle de dispositivo ya no dependen de JavaScript
  inline bloqueado por CSP; Chart.js y Cytoscape se sirven localmente.

## Cadena de verdad

`DB Devices/DeviceInterfaces/NetworkLinks` → `TopologyService` → `TopologyGraphDto` → Razor/API JSON → `topology.js` → Cytoscape.

La capa de servicio ya conservaba los dispositivos sin enlace; la pérdida estaba en la presentación del Dashboard.

## Validación ejecutada

- `dotnet build AtlasNOC.sln -c Release --no-restore`: 0 errores; quedan 8
  warnings preexistentes `CS1998` en tests de drivers sin `await`.
- E2E API key: 7 SKIP por configuración externa.
- Runtime: 1 SKIP por configuración externa de base de datos.
- `git diff --check`: correcto.

### Ejecución completa con MySQL local

Se usó una base dedicada `atlasnoc_integration_test` mediante variable de
entorno efímera, con TLS deshabilitado únicamente para esta instancia local.
No se almacenó la credencial.

| Suite | Passed | Failed | Skipped | Total |
|---|---:|---:|---:|---:|
| Unit | 174 | 0 | 0 | 174 |
| Integration | 8 | 0 | 0 | 8 |
| Runtime | 8 | 0 | 0 | 8 |
| E2E / Playwright | 14 | 0 | 0 | 14 |

La E2E adicional verifica que el conteo de nodos/enlaces del API coincide con
Cytoscape y que la selección ofrece IP y navegación al detalle.

La E2E completa valida login, discovery LAB, `/Topology`, coincidencia entre
API y Cytoscape, contador, selección de nodo/IP, detalle de dispositivo,
métricas, operaciones y preview de acción, alertas, incidentes, recuperación,
API keys y auditoría. También registra errores de página, requests fallidos y
respuestas locales 401/403/404.

Se añadió `ServiceCreditCalculator` con cálculo proporcional y tope mensual,
más un preview que exige incidente resuelto y usa la tarifa persistida del plan.

La prueba directa de persistencia sobre `atlasnoc_rebuild` reportó 0
dispositivos, 0 interfaces, 0 enlaces, 0 observaciones y 0 sitios. Las
fixtures E2E/Runtime crean datos LAB controlados y los eliminan al finalizar;
no existe evidencia de seis dispositivos persistidos en la base inspeccionada.

## Pendiente

La verificación automatizada de navegador real queda cubierta por Playwright.
No hay seis dispositivos persistidos en `atlasnoc_rebuild` para enumerar. La
matriz exhaustiva de todas las vistas sigue siendo un pendiente de producto,
no se marca como validada por la existencia de sus `.cshtml`. Estado de esta
corrección de topología: `READY`; estado global del producto: `PARTIALLY READY`.
