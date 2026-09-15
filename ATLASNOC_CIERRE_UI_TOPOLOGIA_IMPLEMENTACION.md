# Cierre de UI y topología — informe de implementación

## Estado

- Rama: `atlasnoc`
- Commit base: `8f53172a3d79308a0bfee6096d500c11ce01fc3d`
- Commit de implementación: `33c19bd`
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

## Cadena de verdad

`DB Devices/DeviceInterfaces/NetworkLinks` → `TopologyService` → `TopologyGraphDto` → Razor/API JSON → `topology.js` → Cytoscape.

La capa de servicio ya conservaba los dispositivos sin enlace; la pérdida estaba en la presentación del Dashboard.

## Validación ejecutada

- `dotnet build AtlasNOC.sln -c Release --no-restore`: 0 errores, 9 warnings no bloqueantes.
- Pruebas filtradas: 7 unitarias PASS.
- E2E API key: 7 SKIP por configuración externa.
- Runtime: 1 SKIP por configuración externa de base de datos.
- `git diff --check`: correcto.

## Pendiente

Falta la verificación manual con navegador real, la enumeración de los seis dispositivos en MySQL activa y la matriz completa de las demás pantallas. Estado global: `PARTIALLY READY`.
