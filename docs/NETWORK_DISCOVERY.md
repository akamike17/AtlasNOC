# Network discovery
El pipeline valida alcance, ejecuta ICMP/SNMP/fingerprint, interfaces, LLDP/CDP, ARP/wireless, reconciliación idempotente y correlación; fallos parciales no abortan el lote.

La suite LAB verifica 61 nodos, 60 links (10 físicos y 50 wireless), IPs/IDs únicos y rescan idempotente. Los estados diferencian observado, monitoreado, manejable, controlado, simulado y unsupported; un link no se crea sólo por coincidencia textual.

Los workers reclaman `DiscoveryRun` mediante lease y el executor registra progreso/errores; los drivers simulados sólo se habilitan en LAB/Testing.
