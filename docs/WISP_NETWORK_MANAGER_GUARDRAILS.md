# AtlasNOC WISP + Network Manager: guardrails de implementación

## Objetivo

Unificar descubrimiento de red, topología y operación WISP en una experiencia de baja complejidad para el usuario final, sin fabricar dispositivos/enlaces ni ejecutar cortes ambiguos.

## Formas principales de fallo y protección

| Riesgo | Protección obligatoria |
|---|---|
| Web y Worker apuntan a bases distintas | Health/readiness visible con identidad de instancia y base sólo por nombre |
| Dos Workers procesan la misma corrida | Lease atómico, renovación y auditoría de propietario |
| ICMP bloqueado por el equipo | ICMP no puede ser puerta única; usar ARP/DHCP/mDNS/SSDP/MNDP cuando aplique |
| SNMP ausente o community incorrecta | SNMP es enriquecimiento opcional; guardar presencia ICMP/L2 sin marcar fallo de host |
| Credencial activa pero mal formada | Validar versión y secretos requeridos antes de guardar y antes de probar |
| API no habilitada o permisos insuficientes | Capability discovery de sólo lectura; no intentar escritura automáticamente |
| Equipo detrás de NAT | Requerir CPE/AP/RADIUS/TR-069/USP u otro canal autorizado; no atravesar NAT por técnicas de ataque |
| IP reutilizada por otro cliente | Correlacionar MAC, sesión, CPE, puerto, timestamp y evidencia; nunca por IP aislada |
| MAC aleatoria en Wi-Fi | Marcar identidad como inestable y evitar suspensión automática |
| DHCP lease obsoleto | Guardar observed-at, TTL y origen; no usar leases expirados para acciones |
| Hostname duplicado | No inferir enlace ni cliente sólo por hostname |
| LLDP/CDP ausente | Mantener nodo sin enlace demostrado; distinguir inferido de confirmado |
| Router con múltiples VRF/VLAN | Descubrimiento por contexto de interfaz; no asumir que una subred representa toda la red |
| NAT/CGNAT del WISP | Usar sesión RADIUS/PPPoE y CPE como identidad; no mapear clientes por IP pública |
| RADIUS caído | Modo lectura y alerta; bloquear acciones masivas |
| Suspensión al cliente equivocado | Requerir coincidencia fuerte cliente+cuenta+CPE+sesión y confirmación crítica |
| Corte irreversible | Sólo perfiles/VLAN/CoA/API documentada, con rollback y registro de auditoría |
| Timeout confundido con error | Registrar etapa, target, protocolo, duración, tipo de excepción y causa separada |
| Escaneo demasiado agresivo | Límites por red, concurrencia, rate limit, exclusiones y ventana configurable |
| IoT sin SNMP/API | Clasificación por presencia, MAC/OUI, puertos y mDNS/SSDP sin inventar fabricante |
| Secreto expuesto en logs | Redacción centralizada; nunca community, password, API key o connection string |
| Configuración automática peligrosa | Preview, explicación simple, dry-run y aprobación explícita |

## Arquitectura objetivo

```text
LocalNetworkProfile
  ├─ interfaces, gateways, rutas, VLANs conocidas
  ├─ L2: ARP, DHCP, MNDP, LLDP/CDP
  ├─ Presence: ICMP, mDNS, SSDP
  ├─ Enrichment: SNMP, RouterOS API, RESTCONF, Aruba Central, UniFi
  ├─ WISP: RADIUS/PPPoE, CPE, TR-069/USP, OLT/ONT
  └─ Correlation: IP + MAC + session + CPE + evidence + confidence
```

## Reglas de producto

1. El primer arranque sólo detecta y explica; no modifica equipos.
2. Un dispositivo puede existir sin credencial y sin enlace.
3. Un enlace sólo se dibuja con evidencia o se marca explícitamente como inferido.
4. Un cliente WISP sólo se suspende desde el sistema de autorización/provisión del operador.
5. La suspensión requiere una coincidencia fuerte, vista previa, confirmación y rollback.
6. Las integraciones se activan por capacidades, no por asumir que un fabricante usa una API concreta.
7. Cuando falta información, la interfaz debe decir “no confirmado” y guiar al usuario.

## Fases de implementación

1. Perfil automático de red local y readiness del Worker.
2. Descubrimiento L2/presencia sin credenciales.
3. Clasificación y correlación con confianza/evidencia.
4. Enriquecimiento SNMP/API de lectura.
5. Modelo WISP: sitios, torres, sectores, CPE, clientes y sesiones.
6. Integración MikroTik/RADIUS como primera ruta operativa.
7. Integraciones Aruba, Cisco, UniFi y OLT/ONT.
8. TR-069/USP para CPE detrás de NAT.
9. Acciones remotas reversibles, auditadas y desactivadas por defecto.

## Estado verificable de conectores

- MikroTik RouterOS: implementado en modo lectura para consultar `/rest/ip/arp`
  cuando el operador proporciona IP de gestión y credenciales mediante configuración
  segura. Produce evidencia CPE con IP, MAC, interfaz, origen, timestamp y confianza;
  no ejecuta suspensión ni cambios.
- Ubiquiti UniFi: implementado en modo lectura para consultar clientes asociados
  (`stat/sta`) mediante API key; produce evidencia de MAC, hostname y AP asociado,
  y no ejecuta cambios. HTTPS es obligatorio por defecto; `AllowInsecureHttp` sólo
  debe usarse explícitamente en un laboratorio aislado.
- RADIUS/PPPoE, Aruba Central, Cisco RESTCONF, OLT/ONT y TR-069/USP: contratos y
  catálogo preparados, pero no deben mostrarse como conectados hasta que exista una
  implementación y prueba contra el sistema autorizado correspondiente.
- SSDP: implementado como presencia LAN de sólo lectura; sólo acepta respuestas cuya
  IP ya pertenece al alcance solicitado, por lo que no amplía el escaneo fuera del
  segmento autorizado. No identifica por sí solo fabricante, cuenta WISP ni CPE.

### Configuración segura de MikroTik

No se deben guardar credenciales RouterOS en `appsettings*.json` versionados. Para
desarrollo local se pueden usar User Secrets:

```powershell
dotnet user-secrets set "MikroTik:ManagementIp" "IP_DEL_ROUTER" --project src/AtlasNOC.Web
dotnet user-secrets set "MikroTik:Username" "USUARIO_LECTURA" --project src/AtlasNOC.Web
dotnet user-secrets set "MikroTik:Password" "CONTRASENA" --project src/AtlasNOC.Web
```

En despliegues, usar el almacén de secretos del entorno para las claves
`MikroTik__ManagementIp`, `MikroTik__Username` y `MikroTik__Password`. El conector
realiza únicamente `GET /rest/ip/arp`; si falta cualquiera de esos valores, permanece
no configurado y no intenta autenticarse.

## Criterio de no avance

Si una acción no puede demostrar con evidencia qué dispositivo, cliente, sesión y sistema de autorización afectará, AtlasNOC debe bloquearla y mostrar qué dato falta.
