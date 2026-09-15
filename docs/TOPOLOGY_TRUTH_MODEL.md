# Topology truth model
LLDP/CDP confirma físico; MAC/ARP/wireless son evidencia; manual e inferido nunca se presentan como confirmado. Un nodo sin link permanece visible como aislado.

Cada nodo expone identidad canónica, IP, tipo, vendor, estado y site. Los filtros separan confirmado/no confirmado, offline y assets sin enlace; `unlinkedNodeCount` hace visible la ausencia de evidencia. La prueba de discovery asegura que un rescan no duplica nodos ni enlaces.

El contrato JS acepta `nodes`, `edges`, `groups` y `unlinkedNodeCount` en la forma serializada por ASP.NET. `topology.js` filtra endpoints desconocidos antes de crear Cytoscape, muestra conteo y detalle, y usa layout grid cuando hay nodos sin edges.

El asset Cytoscape se sirve localmente. Un grafo de seis dispositivos y cero links produce seis nodos visibles y cero edges; no se agregan edges por hostname, IP o proximidad.
