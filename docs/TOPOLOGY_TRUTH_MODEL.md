# Topology truth model
LLDP/CDP confirma físico; MAC/ARP/wireless son evidencia; manual e inferido nunca se presentan como confirmado. Un nodo sin link permanece visible como aislado.

Cada nodo expone identidad canónica, IP, tipo, vendor, estado y site. Los filtros separan confirmado/no confirmado, offline y assets sin enlace; `unlinkedNodeCount` hace visible la ausencia de evidencia. La prueba de discovery asegura que un rescan no duplica nodos ni enlaces.
