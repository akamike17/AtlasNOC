# ATLASNOC — ESPECIFICACIÓN MAESTRA QUIRÚRGICA
## Cabina operativa ISP/WISP/NOC orientada a grafo, control real, clientes, cobranza, soporte, inventario y operación de campo

**Repositorio objetivo:** `akamike17/AtlasNOC`  
**Rama de trabajo conocida:** `codex/atlasnoc-review-20260909`  
**Baseline revisado:** `0f60df6d1b86a1be048b8f23f550e5703ce96758`  
**Objetivo:** reconvertir AtlasNOC de “mapa bonito de red” a **cabina operacional completa de ISP/WISP**, manteniendo verdad técnica, seguridad, auditabilidad y una UX que pueda usar personal con conocimientos básicos de redes.

---

# 0. REGLA PRINCIPAL

**AtlasNOC NO es un visor de topología.**

Debe permitir operar una red real desde un grafo funcional:

- descubrir,
- identificar,
- inventariar,
- mapear,
- monitorear,
- diagnosticar,
- autorizar,
- rechazar,
- provisionar,
- suspender,
- reconectar,
- respaldar,
- restaurar,
- controlar equipos compatibles,
- administrar clientes y servicios,
- llevar pagos/cobranza,
- gestionar incidencias,
- planear visitas técnicas,
- calcular capacidad,
- detectar fraude/anomalías,
- y mantener evidencia/auditoría.

Si al terminar solamente se ven nodos bonitos unidos por líneas, **LA TAREA NO ESTÁ TERMINADA**.

---

# 1. PRINCIPIOS NO NEGOCIABLES

## 1.1 Verdad técnica
Nunca afirmar soporte de una marca, protocolo, acción o capacidad si no existe evidencia comprobable en código/pruebas/laboratorio.

Estados obligatorios de verdad:

- `OBSERVED`: detectado.
- `MONITORED`: hay estado/métricas.
- `MANAGEABLE`: hay driver + credencial válida.
- `CONTROLLED`: existen acciones reales probadas.
- `SIMULATED`: comportamiento de laboratorio/simulador.
- `UNSUPPORTED`: no soportado.
- `UNKNOWN`: no determinado.

## 1.2 No inventar topología
Los enlaces deben llevar tipo y evidencia:

- LLDP/CDP = enlace físico confirmado.
- MAC/FDB = observación de capa 2.
- ARP = relación lógica.
- Wireless association = relación radio observada.
- Manual = declarado por usuario.
- Inferido = sólo si está claramente marcado y nunca como “confirmado”.

## 1.3 La complejidad vive debajo
El usuario básico no debe decidir PPPoE vs DHCP, address-list vs ACL, VLAN interna, comando RouterOS, endpoint REST u OID SNMP.

El operador debe ver acciones de negocio/red:

- `Activar`
- `Rechazar`
- `Suspender`
- `Reconectar`
- `Cambiar plan`
- `Editar`
- `Respaldar`
- `Restaurar`
- `Reemplazar equipo`
- `Diagnosticar`

El backend decide cómo ejecutar cada acción según el equipo, el driver y la política técnica.

---

# 2. MODELO DE RED OBJETIVO

No imponer una topología rígida. Atlas debe soportar cualquier combinación válida:

```text
INTERNET / UPSTREAM
        |
   CENTRAL / CORE
        |
   +----+-----------------------+
   |                            |
  LAN                     BACKHAUL / WAN
                                |
                +---------------+----------------+
                |               |                |
              FIBRA            RADIO            COBRE
                |               |                |
              GPON            ENLACE           DSL/Ethernet
                |               |                |
          MINICENTRAL/SITIO/TORRE/POP
                        |
                AP / SECTOR / OLT / SWITCH
                        |
                 CPE / ONU / ONT
                        |
                     ROUTER
                        |
                    CLIENTE
```

Un tramo puede continuar fibra→fibra, fibra→radio, fibra→cobre, radio→radio, GPON→ONT, switch→AP, AP→CPE, etc.

Cada nodo y cada enlace debe conocer:

- qué es,
- dónde está,
- quién lo alimenta,
- qué depende de él,
- capacidades,
- configuración,
- estado,
- evidencias,
- métricas,
- historial,
- incidencias,
- y acciones permitidas.

---

# 3. EL GRAFO ES LA INTERFAZ PRINCIPAL

El mapa/grafo debe ser funcional, no decorativo.

Al seleccionar cualquier nodo se abre un panel lateral o detalle operativo con:

1. Resumen
2. Red
3. Interfaces
4. Wireless
5. Clientes
6. Tráfico
7. Capacidad
8. Alertas
9. Configuración
10. Backups
11. Incidencias
12. Historial
13. Acciones

Mostrar sólo pestañas que tengan sentido para ese tipo de nodo.

Ejemplos:
- AP: clientes asociados, RSSI/SNR/CCQ, capacidad y acciones radio.
- Switch: puertos, estado, velocidad, VLAN, PoE, vecinos, errores.
- CPE: señal, AP, cliente asignado, historial, ubicación.
- Router core: WAN, rutas, interfaces, tráfico, dependencias e impacto.
- Cliente: contrato, servicio, pagos, equipo, incidencias y estado.

---

# 4. JERARQUÍA OPERATIVA Y GEOGRÁFICA

Atlas debe permitir organización por:

- País
- Estado
- Ciudad/Municipio
- Zona
- Colonia
- Central
- Minicentral/POP
- Sitio/Torre
- Sector/Cobertura
- Enlace
- AP
- CPE
- Cliente

Ejemplo de identificador de servicio:

```text
Col_Esp_00001
Col_Esp_00002
Col_San_00001
```

Separar:

- `CustomerId` interno global e inmutable.
- `ServiceCode` humano por zona.
- `ServiceAddress`.
- `ServiceLocation`.
- `NetworkAssignment`.

Si el cliente cambia de zona, conserva su identidad global y cambia el servicio/ubicación con historial completo.

---

# 5. CLIENTE ≠ SERVICIO ≠ DOMICILIO ≠ EQUIPO ≠ CUENTA

## 5.1 Cliente
Tipos:

- Cliente normal/no fiscal.
- Persona física.
- Persona moral.

Campos base:

- CustomerId
- Número de cliente
- Nombre / Razón social
- Tipo de cliente
- Teléfono
- Email
- Datos fiscales cuando correspondan
- Estado
- Fecha de alta
- Notas controladas
- Indicadores de deuda
- Indicadores de equipo pendiente
- Indicadores de fraude/revisión

## 5.2 Identidad/documentación
Para alta normal:

- nombre,
- clave de elector,
- domicilio,
- teléfono,
- email,
- comprobante de domicilio validado,
- aceptación del aviso de privacidad.

No almacenar imágenes de INE salvo decisión explícita posterior.

Validar duplicidad y consistencia:

- misma clave de elector en dos clientes distintos,
- mismo cliente creado varias veces,
- direcciones sospechosamente diferentes,
- teléfonos/email repetidos de forma anómala.

No bloquear automáticamente por diferencias legítimas: mandar a revisión.

## 5.3 Servicio
Un cliente puede tener uno o más servicios.

El servicio contiene:

- ServiceId
- ServiceCode (`Col_Esp_00001`)
- CustomerId
- PlanId
- Zona
- Domicilio
- Coordenadas
- Tecnología
- Fecha de activación
- Ciclo de cobro
- Estado
- Equipo asignado
- Ruta de red
- Historial

---

# 6. DOMICILIO Y COBERTURA

Antes de vender, Atlas debe resolver cobertura por:

1. domicilio capturado,
2. normalización de dirección,
3. coordenadas,
4. zona,
5. infraestructura disponible,
6. medio de acceso,
7. capacidad disponible,
8. evidencia de cobertura.

Estados de cobertura:

- `AVAILABLE`
- `PROBABLY_AVAILABLE`
- `REQUIRES_FIELD_VALIDATION`
- `NO_COVERAGE_CONFIRMED`
- `CAPACITY_LIMITED`
- `SATURATED`
- `PLANNED`

No contestar “no hay cobertura” sólo porque una colonia no esté exacta en una tabla.

Mostrar infraestructura cercana, tecnología disponible, distancia, sector, capacidad, clientes actuales y margen disponible.

---

# 7. PROSPECTOS OPCIONALES

Flujo:

```text
Prospecto
 -> Validar cobertura
 -> Seguimiento
 -> Si acepta:
      documentación
      pago
      contrato
      instalación
      alta como cliente
```

El prospecto no es todavía cliente.

Al convertir:

```text
Prospect -> Customer -> Service
```

Conservar relación histórica.

La demanda no cubierta debe servir para planeación.

---

# 8. PLANES Y CAPACIDAD

Cada plan debe permitir:

- nombre,
- tecnología aplicable,
- velocidad bajada,
- velocidad subida,
- precio,
- ciclo,
- reglas comerciales,
- prioridad/QoS si aplica,
- capacidad mínima requerida.

Antes de alta/cambio de plan, Atlas valida la ruta completa:

```text
Cliente
 -> CPE
 -> AP/Sector
 -> Backhaul
 -> Minicentral
 -> Core
 -> Upstream
```

Calcular:

- capacidad teórica,
- capacidad reservada,
- consumo promedio,
- pico,
- oversubscription,
- margen disponible,
- clientes aguas abajo.

Alertas configurables, por ejemplo:
- 70% advertencia,
- 85% alta,
- 95% crítica.

---

# 9. CAMBIO DE PLAN

1. verificar cobertura/capacidad,
2. calcular diferencia,
3. mostrar impacto,
4. confirmar,
5. aplicar,
6. auditar.

Si sube de plan a mitad de ciclo:
- aplicar nuevo perfil,
- calcular diferencia proporcional por días restantes,
- cargar diferencia al siguiente pago.

---

# 10. PAGO ADELANTADO

Configuración inicial sugerida:

- 3 meses → +5 días gratis
- 6 meses → +10 días gratis
- 9 meses → +15 días gratis
- 12 meses → +1 mes gratis

Debe ser configurable.

Al marcar pago adelantado mostrar fecha inicial, fecha final, bonificación y total; pedir confirmación.

---

# 11. COBRANZA Y CUENTA DEL CLIENTE

No implementar “pagos sueltos”. Implementar cuenta corriente:

- cargos,
- mensualidades,
- descuentos,
- créditos por indisponibilidad,
- reconexiones,
- instalaciones,
- equipo,
- pagos,
- convenios,
- promesas de pago,
- saldo.

Estados:

- al corriente,
- vence pronto,
- vencido,
- 1 mes,
- 2 meses,
- 3+ meses,
- suspendido,
- convenio,
- promesa incumplida,
- cancelado con saldo.

Cobranza debe poder filtrar y llamar a clientes morosos.

---

# 12. SUSPENSIÓN Y RECONEXIÓN

Regla inicial:

- vencimiento,
- 3 días de gracia,
- suspensión automática.

Permitir suspensión manual antes con permiso, motivo y auditoría.

Cuota inicial de reconexión: `$50 MXN`, configurable, exceptuable y condonable con motivo.

Pago electrónico confirmado: reconectar automáticamente si cubre lo requerido.

Pago en efectivo: cuando usuario autorizado valida el ingreso, reconectar inmediatamente.

---

# 13. PROMESA DE PAGO Y CONVENIOS

Persistir:

- monto,
- saldo,
- fecha prometida,
- usuario que autorizó,
- condiciones,
- vencimiento,
- estado.

Si incumple:
- conservar saldo,
- conservar recargos anteriores,
- aplicar cargos configurados,
- volver a suspensión si corresponde,
- nunca borrar historial.

---

# 14. PAGOS

Crear abstracción neutral:

```csharp
IPaymentProvider
```

Proveedores posibles:
- Manual
- Efectivo
- Transferencia
- SPEI
- Tarjeta
- OXXO
- Mercado Pago
- Stripe
- Conekta
- otros futuros

Pago debe conciliar por:
- CustomerId
- ServiceId
- PaymentReference
- Amount
- Date
- Provider
- ExternalTransactionId
- CapturedBy
- Evidence/metadata

---

# 15. RECIBOS Y FACTURACIÓN

Primera fase:
- notas/recibos internos,
- estados de cuenta,
- comprobantes de pago.

Arquitectura preparada para:
- persona física,
- persona moral,
- CFDI,
- SAT,
- proveedores fiscales futuros.

No hardcodear reglas fiscales cambiantes.

---

# 16. CONTRATO

Generar contrato de servicio con:
- sin plazo forzoso,
- mensualidad,
- condiciones,
- equipo entregado,
- obligaciones,
- suspensión,
- reconexión,
- pagos,
- devolución de equipo,
- aviso de privacidad,
- pagaré/documento por equipo propiedad de la empresa,
- firma/aceptación.

El contenido legal final debe ser revisable/configurable.

---

# 17. INSTALACIÓN

Política inicial:

## Dentro de ciudad
- instalación gratis,
- requiere un mes adelantado.

## Fuera de ciudad
- instalación: $350,
- router: $350,
- valores configurables.

---

# 18. INVENTARIO / STOCK / PROPIEDAD

Todo equipo debe registrar:
- AssetId
- tipo
- marca
- modelo
- serial
- MAC
- firmware
- propietario
- estado
- ubicación
- costo/valor
- garantía
- historial

Propiedad:
- empresa,
- cliente.

Estados:
- UnknownDetected
- Stock
- Reserved
- InProduction
- Assigned
- Recovered
- PendingInspection
- Tested
- Repair
- Damaged
- Retired
- PendingRecovery

Equipo detectado nuevo: preguntar si se agrega a stock.

---

# 19. ROUTER DEL CLIENTE

No instalar “software Atlas” arbitrario.

Al detectar router:

```text
Router detectado
Marca: X
Modelo: Y
MAC: Z
¿Configurar para monitoreo/acceso remoto?
```

Si se acepta:
1. pedir credenciales de forma segura,
2. verificar capacidad real,
3. aplicar sólo configuración soportada,
4. activar monitoreo/acceso compatible,
5. validar resultado,
6. crear backup,
7. auditar.

No casarse con TP-Link/Mercusys.

---

# 20. REEMPLAZO DE EQUIPO

```text
Equipo anterior -> Failed/Retired
Detectar nuevo equipo
 -> validar tipo/modelo/capacidades
 -> Reemplazar
 -> asociarlo al mismo lugar lógico
 -> cargar config compatible
 -> validar conectividad
 -> preservar historial
```

Si config incompatible:
- no aplicar a ciegas,
- mostrar diferencias,
- requerir Admin.

---

# 21. BACKUP, VERSIONADO Y ROLLBACK

Crear `ConfigurationRevision` con:
- revision,
- timestamp,
- user,
- reason,
- beforeHash,
- afterHash,
- source,
- backupLocation,
- knownGood,
- deviceId.

Acciones:
- Backup
- Compare
- Restore
- MarkKnownGood
- CloneToReplacement

Antes de cambios pesados, snapshot/backup cuando el dispositivo lo permita.

---

# 22. ROLES

## Básico / Administrativo
Clientes, pagos, recibos, consulta, prospectos, documentación y cobranza limitada.

## Operador
Red, altas/bajas, autorización, suspensión/reconexión, control permitido e incidentes.

## Soporte
Mapa, diagnóstico, tickets, clientes afectados, programación de visita e historial.

## Administrador
Configuraciones pesadas, drivers, credenciales, backups, restore, políticas, overrides y acciones críticas.

---

# 23. ALTA DE CLIENTE WISP / CPE DESCONOCIDA

Una CPE nueva aparece en un AP.

Atlas muestra:

```text
NUEVO DISPOSITIVO / NO AUTORIZADO
AP: AP-NORTE-01
Sector: Norte
MAC: XX:XX:XX:XX:XX:XX
IP observada: X.X.X.X
Vendor: ...
RSSI: ...
SNR: ...
Primera detección: ...
```

Acciones:
- Identificar
- Relacionar con orden/cliente
- Aceptar
- Rechazar/Expulsar

Si técnico llama a cabina y coincide:
- aceptar,
- crear/asociar servicio,
- registrar inicio,
- aplicar política del equipo,
- activar Internet,
- auditar.

Si no corresponde:
- rechazar/expulsar,
- marcar no autorizado,
- conservar evidencia.

---

# 24. CAMBIO DE CPE / ANTENA

Si un cliente ya tenía CPE y aparece otra:

```text
El servicio Col_Esp_00001 tenía CPE-A MAC XX
Ahora se detecta CPE-B MAC YY
¿Es reemplazo autorizado?
```

Antes de dar acceso:
- validar,
- pedir motivo,
- confirmar,
- registrar usuario,
- conservar historial.

No crear otro cliente.

---

# 25. FRAUDE / MOVIMIENTO SOSPECHOSO

Detectar:
- CPE en AP inesperado,
- cambio extraño de zona,
- MAC nueva no reportada,
- ubicación incompatible,
- duplicidad,
- comportamiento anómalo.

Primero: `ALERTA DE POSIBLE FRAUDE`.

Mostrar evidencia y explicación. Acciones: revisar, aceptar cambio legítimo, bloquear, expulsar, abrir caso.

---

# 26. WISP OPERACIONAL

Reusar entidades existentes:
- RadioSector
- WirelessAssociation
- Subscriber
- ServiceEndpoint
- NetworkSite

Añadir:

```csharp
IWispOperationsService
```

Correlacionar:

```text
Customer
 -> Service
 -> Router/CPE
 -> WirelessAssociation
 -> AP
 -> Sector
 -> Site
 -> Backhaul
 -> Core
```

Mostrar señal, calidad, uptime, tráfico, plan, consumo, AP/sector, capacidad, alertas e historial.

---

# 27. WAN / LAN / GPON / DSL / FIBRA / RADIO

No asumir una sola tecnología.

## WAN
Crear `IWanStatusService` / `WanStatusDto`.
No asumir `ether1`.
Resolver WAN por evidencia y permitir confirmación manual.

## LAN
Mostrar puertos, velocidad, duplex, estado, VLAN, PoE, vecinos, tráfico, errores y MAC/FDB.

## GPON
Preparar soporte para OLT, PON, ONU/ONT, serial, estado, señal óptica, distancia y perfiles cuando el driver lo permita.

## DSL
Preparar sincronización, upstream/downstream, SNR, atenuación, errores y estado.

## Fibra
Modelar enlaces, tramos, cajas/NAP/splitters cuando se decida, puertos, origen/destino, capacidad, evidencia y fallas.

---

# 28. ALINEACIÓN DE ANTENAS — OPCIONAL

Si existen coordenadas A y B, calcular:
- distancia,
- azimut,
- elevación aproximada,
- orientación sugerida,
- opcionalmente Fresnel/perfil si hay datos.

No venderlo como instrumento de precisión. Comparar después con RSSI, SNR, CCQ y throughput real.

---

# 29. CONTROL REAL — CONTRATO SEPARADO

Mantener `IDeviceDriver` como lectura/observación.

Crear:

```csharp
IDeviceControlDriver
```

Ejemplo:

```csharp
Task<DeviceControlCapabilities> GetControlCapabilitiesAsync(...);
Task<DeviceActionResult> ExecuteAsync(DeviceActionRequest request, ...);
```

Acciones neutrales iniciales:
- Reboot
- EnableInterface
- DisableInterface
- SetInterfaceDescription
- PoeOn
- PoeOff
- DisconnectWirelessClient
- EnableSubscriber
- DisableSubscriber
- BackupConfiguration
- RestoreConfiguration

Agregar sólo acciones probadas.

---

# 30. REGISTRO DE CAPACIDADES

Crear:

```csharp
IDeviceCapabilityService
IControlDriverRegistry
```

UI sólo muestra acciones comprobadas. Nunca simular éxito.

---

# 31. ORQUESTADOR DE ACCIONES

Crear:

```csharp
INetworkActionService
```

Toda acción mutante debe pasar por:
1. autorización,
2. capability check,
3. credencial,
4. driver,
5. análisis de impacto,
6. preview,
7. confirmación,
8. ejecución,
9. auditoría,
10. refresh,
11. verificación post-acción.

---

# 32. RIESGO DE ACCIONES

Niveles:
- ReadOnly
- Low
- Medium
- High

Acciones High requieren confirmación reforzada.

---

# 33. IMPACTO ANTES DE CAMBIOS

Crear:

```csharp
INetworkImpactService
```

Ejemplo:

```text
Deshabilitar interface sfp1
Impacto estimado:
- 2 sitios
- 5 AP
- 73 servicios
- 4 incidencias abiertas

Topología incompleta: Impacto parcial.
```

---

# 34. MIKROTIK — PRIMER DRIVER REAL

Crear `MikroTikControlDriver.cs`.

Fase inicial:
- Reboot
- EnableInterface
- DisableInterface
- SetInterfaceDescription

Después, sólo si se prueba:
- DHCP
- routes
- bridge
- VLAN
- queue/profile
- address-list
- wireless

---

# 35. UBIQUITI / AIRMAX / UNIFI / OTROS

No decir “Ubiquiti soportado” de forma genérica.
Separar familias y capacidades por protocolo/modelo.

---

# 36. CISCO / SNMP

Cisco/Generic SNMP inicialmente observación.
No habilitar SNMP SET genérico sin OID exacto, tipo, vendor/model, rollback y prueba de laboratorio.

---

# 37. DIAGNÓSTICO AUTOMÁTICO

Crear:

```csharp
INetworkDiagnosticService
```

Para un cliente comprobar CPE/router, AP, asociación, señal, interfaz, backhaul, central, gateway, WAN, DNS, pérdida/latencia e incidentes.

Resultado humano:

```text
Probable causa:
Enlace AP-NORTE -> MC-02 sin conectividad.

Afectados: 31 servicios.
No se recomienda visita domiciliaria.
```

Estados: Observado / Probable / No determinado.

---

# 38. INCIDENTES RAÍZ

Si cae un enlace:

```text
FIBRA MC2 CAÍDA
   |
   +-- 2 sitios
   +-- 5 AP
   +-- 73 clientes
```

Crear un incidente raíz y relacionar tickets, medir afectación por servicio y verificar recuperación.

---

# 39. SOPORTE TELEFÓNICO

Flujo:

```text
Cliente llama
 -> localizar cliente/servicio
 -> diagnóstico Atlas
 -> abrir incidencia
 -> intentar solución remota
 -> si resuelve: cerrar
 -> si no: valorar visita
 -> cliente acepta visita
 -> crear orden
```

Guardar motivo, síntomas, hora, diagnóstico, acciones, operador, resultado, duración y relación con incidente raíz.

---

# 40. VISITA TÉCNICA

Si soporte no resuelve:
- acordar con cliente,
- registrar ventana horaria,
- crear orden,
- asociar zona,
- tipo de trabajo,
- equipo/material esperado,
- prioridad.

No incluir visita si el cliente no la acepta.

---

# 41. PLANIFICADOR DE RUTAS

Crear:

```csharp
ITechnicianRoutePlanner
```

Considerar:
- prioridad,
- zona,
- coordenadas,
- ventana del cliente,
- duración estimada,
- traslado,
- horario laboral,
- buffer,
- dependencia/incidente raíz,
- equipo/material.

Tiempos iniciales configurables:
- diagnóstico domicilio: 45 min
- router: 30 min
- cambio CPE: 45 min
- realineación: 60 min
- RJ45/RJ11: 45 min
- instalación WISP: 90 min
- WISP compleja: 120 min
- fibra/ONT existente: 120 min
- fibra nueva: 180–240 min
- AP/sitio: 90 min
- backhaul: 90 min

Añadir buffer 10–20% configurable.

---

# 42. APRENDIZAJE DE TIEMPOS

Registrar previsto, real, traslado, tipo de trabajo, zona, técnico y resultado.
Con históricos suficientes usar estadísticas robustas para mejorar estimaciones.
No necesita IA en primera versión.

---

# 43. RECÁLCULO DE ETA

Si una visita se retrasa, recalcular siguientes llegadas y mostrar ETA nueva a soporte.

---

# 44. PRIORIDAD DE INCIDENCIAS

Atlas propone prioridad automática por impacto.

Baseline:
- P1 Crítica
- P2 Alta
- P3 Media
- P4 Baja

Permitir reclasificación manual con motivo y auditoría.

---

# 45. SLA / SLO INTERNO

Valores iniciales configurables:
- P1: respuesta 15 min / objetivo 4 h
- P2: 30 min / 8 h
- P3: 1 h / 24 h
- P4: 4 h / 48 h

No tratarlos como garantía contractual por defecto.

---

# 46. CRÉDITOS POR FALLA

No descontar por cualquier ping.
Crear evento de indisponibilidad atribuible al proveedor.

Calcular periodo, precio, tiempo afectado, crédito sugerido y total ajustado.
Relacionarlo con incidentes reales.

---

# 47. CANCELACIÓN

```text
Solicitud
 -> saldo final
 -> equipos empresa
 -> recuperación
 -> desaprovisionamiento
 -> cierre técnico
 -> servicio cancelado
```

No borrar cliente.
Mostrar status de saldo/equipo/convenio pendiente.

---

# 48. EQUIPO RECUPERADO

```text
Recuperado
 -> Pendiente revisión
 -> Probado
 -> Disponible
```

Si falla: reparación o baja.

---

# 49. MAPA GEOGRÁFICO

Además del grafo lógico, agregar mapa geográfico con centrales, POP, torres, AP, sectores, enlaces, clientes, cobertura, demanda, incidencias y capacidad.

---

# 50. ALERTAS DE CAPACIDAD Y PLANEACIÓN

Ejemplo:

```text
COLONIA LAS FLORES
Cobertura: PARCIAL
Clientes: 54
Prospectos: 23
Backhaul: 300 Mbps
Pico: 287 Mbps / 95.7%
ESTADO: CRÍTICO
ACCIONES: No recomendado para nuevas altas / Crear ampliación
```

---

# 51. NOTIFICACIONES

Arquitectura desacoplada:

```csharp
INotificationProvider
```

Proveedores futuros: InApp, Email, SMS, WhatsApp, Push, Webhook, Telegram, Teams, cualquier otro.

---

# 52. SEGURIDAD Y CREDENCIALES

Nunca guardar passwords en config plana.
Usar `ITargetCredentialProvider` o equivalente.

Requisitos:
- cifrado,
- referencias seguras,
- secretos fuera de logs,
- permisos por rol,
- rotación,
- auditoría.

---

# 53. AUDITORÍA

Toda acción crítica registra usuario, rol, fecha, dispositivo, cliente/servicio, acción, motivo, parámetros sanitizados, resultado y antes/después cuando corresponda.

---

# 54. SIMULADOR

Debe permitir probar:
- central,
- routers,
- switches,
- enlaces,
- AP,
- sectores,
- CPE,
- clientes,
- fallas,
- saturación,
- clientes no autorizados,
- pagos,
- cortes,
- reconexiones,
- cambio de CPE,
- fraude,
- incidentes,
- recuperación,
- rutas técnicas.

Separar visualmente `SIMULATED` vs `REAL`.

---

# 55. DRIVER DE CONTROL SIMULADO

Crear `SimulatedDeviceControlDriver` para E2E y modificar estado simulado de forma verificable.

---

# 56. POLLING / TIEMPO REAL

Primera implementación: polling AJAX cada 10–15 segundos para snapshot operacional.
Endpoint conceptual: `/api/operations/snapshot`.
No agregar SignalR sólo por moda.

---

# 57. VISTAS PRINCIPALES

- `/` Centro de red
- `/network/{id}` Dispositivo
- `/wisp` WISP
- `/customers` Clientes
- `/support` Soporte
- `/inventory` Inventario
- `/coverage` Cobertura
- `/settings` Configuración

Evitar docenas de pantallas inútiles.

---

# 58. VALIDACIONES OBLIGATORIAS

- IP válida
- máscara válida
- gateway coherente
- VLAN válida
- MAC válida
- duplicidad IP
- duplicidad serial
- duplicidad cliente
- zona válida
- plan compatible
- capacidad
- equipo compatible
- driver soportado
- credencial válida
- acción permitida
- rol permitido

Nunca mandar configuración inválida al dispositivo.

---

# 59. WORKFLOWS IMPORTANTES

## Nuevo cliente
```text
Prospecto opcional
 -> cobertura
 -> documentación
 -> contrato
 -> pago
 -> instalación
 -> detectar CPE/router
 -> cabina valida
 -> activar
 -> cliente activo
```

## Cliente no autorizado
```text
Dispositivo detectado
 -> no reconocido
 -> alerta
 -> identificar
 -> aceptar o expulsar
```

## Cambio de CPE
```text
CPE vieja
 -> nueva detectada
 -> confirmar reemplazo
 -> conservar servicio
 -> actualizar equipo
 -> auditar
```

## Morosidad
```text
Vencimiento
 -> 3 días
 -> alerta cobranza
 -> suspensión
 -> pago
 -> reconexión
```

## Falla masiva
```text
Backhaul falla
 -> incidente raíz
 -> dependencias
 -> clientes afectados
 -> tickets ligados
 -> visita sitio
 -> reparación
 -> validación
 -> créditos
```

---

# 60. PRUEBAS UNITARIAS MÍNIMAS

Cobertura, customer/service separation, duplicados, billing, pago adelantado, reconexión, promesa, créditos, planes, capacidad, stock, replacement, backup, rollback, authorization, capability checks, audit, impact, incident correlation, route planning, fraud detection, WISP correlation.

---

# 61. PRUEBAS DE DRIVER

MikroTik:
- request exacto,
- auth,
- timeout,
- TLS,
- errores,
- capability false,
- unsupported action,
- success,
- idempotencia cuando aplique.

---

# 62. E2E WEB OBLIGATORIO

1. descubrir red simulada,
2. ver topología,
3. detectar CPE desconocida,
4. crear/relacionar cliente,
5. aceptar,
6. darle servicio,
7. generar cobro,
8. vencer,
9. suspender,
10. pagar,
11. reconectar,
12. provocar falla,
13. crear incidente raíz,
14. crear ticket,
15. programar visita,
16. calcular ruta,
17. recuperar servicio,
18. calcular crédito,
19. generar estado de cuenta.

---

# 63. LAB REAL

Pruebas reales opt-in:

```text
ATLAS_REAL_CONTROL_TESTS=1
ATLAS_REAL_CONTROL_TARGET=<ip>
```

Nunca seleccionar un equipo real automáticamente.

---

# 64. NO BORRAR FUNCIONALIDAD ÚTIL EXISTENTE

Preservar discovery, topology, metrics, alerts, incidents, credentials, drivers, polling, audit, sites y entidades WISP existentes. Refactorizar, no destruir.

---

# 65. ORDEN DE IMPLEMENTACIÓN RECOMENDADO

1. Dominio y contratos.
2. Centro operacional/grafo.
3. WISP.
4. Billing/cobranza.
5. Soporte/incidencias/rutas.
6. Inventario/config/backup.
7. Control real de equipos.
8. Cobertura/mapa geográfico.
9. Hardening/E2E/lab.

---

# 66. CRITERIO DE “TERMINADO”

No declarar terminado hasta comprobar:
- discovery real/simulado,
- topology con evidencia,
- graph interactivo,
- customer/service model,
- WISP correlation,
- unauthorized CPE workflow,
- activate/reject,
- billing,
- payments,
- suspension/reconnection,
- inventory,
- backups,
- replacement,
- incidents,
- root cause correlation,
- support tickets,
- visit planner,
- route optimization,
- capacity,
- coverage,
- primer driver real de control,
- audit,
- roles,
- simulator,
- E2E,
- opt-in lab harness.

---

# 67. PRODUCT TRUTH FINAL

Antes de cerrar, imprimir matriz honesta:

| Área | Estado |
|---|---|
| Discovery | READY/PARTIAL |
| Topology | READY/PARTIAL |
| Monitoring | READY/PARTIAL |
| WISP correlation | READY/PARTIAL |
| Billing | READY/PARTIAL |
| Payments | READY/PARTIAL |
| Support | READY/PARTIAL |
| Inventory | READY/PARTIAL |
| Coverage | READY/PARTIAL |
| MikroTik control | READY/PARTIAL |
| Ubiquiti control | READY/PARTIAL |
| GPON | READY/PARTIAL |
| DSL | READY/PARTIAL |
| Physical lab | PROVEN/NOT PROVEN |

No poner READY sin evidencia.

---

# 68. INSTRUCCIONES PARA CODEX

1. Leer solución completa antes de tocar.
2. Revisar entidades/servicios existentes.
3. No duplicar dominios.
4. Crear plan de cambios antes de implementar.
5. Implementar por capas: Domain → Application → Infrastructure → Web → Tests.
6. Compilar después de cada bloque.
7. Ejecutar pruebas.
8. No esconder warnings/fallos.
9. No cambiar verdades de producto para “hacer verde” una prueba.
10. No usar datos falsos en vistas productivas.
11. No convertir simulación en “real”.
12. No afirmar soporte de fabricantes no probado.
13. No parar después del diagnóstico si existe una corrección segura.
14. Al final: build Release, tests, E2E, diff review, seguridad y product-truth matrix.
15. Entregar commit y SHA.
16. Indicar claramente lo que queda sin prueba física.

---

# 69. PROHIBICIONES

- No dashboard-only.
- No CRUD masivo sin operación real.
- No botones muertos.
- No datos hardcodeados en producción.
- No topología inventada.
- No acciones fake.
- No “Ubiquiti supported” genérico.
- No SNMP control genérico.
- No secretos en logs.
- No plaintext passwords.
- No borrar clientes para resolver deuda.
- No descuentos por simples pings perdidos.
- No reconexión sin validación de pago.
- No restore incompatible a ciegas.
- No migraciones destructivas sin análisis.
- No cerrar tarea porque “se ve bonito”.

---

# 70. PRUEBA DE HUMO FINAL OBLIGATORIA

En simulador:

1. Crear central.
2. Crear backhaul.
3. Crear sitio.
4. Crear AP.
5. Crear 3 CPE.
6. Registrar 2 clientes.
7. Dejar 1 CPE no autorizada.
8. Atlas debe detectarla.
9. Aceptar una CPE.
10. Rechazar otra.
11. Crear plan.
12. Activar servicio.
13. Generar mensualidad.
14. Marcar vencido.
15. Suspender.
16. Registrar pago.
17. Reconectar.
18. Saturar backhaul.
19. Mostrar alerta crítica.
20. Derribar enlace.
21. Crear incidente raíz.
22. Asociar clientes afectados.
23. Abrir ticket.
24. Programar visita.
25. Generar ruta.
26. Restaurar enlace.
27. Cerrar incidente.
28. Calcular crédito.
29. Generar estado de cuenta.
30. Confirmar auditoría completa.

Si alguno de estos pasos no existe realmente, el producto todavía no está terminado.

---

# 71. CIERRE

> AtlasNOC debe permitir que una persona de cabina opere una red ISP/WISP completa desde un grafo funcional, con controles reales sólo cuando están probados, clientes y servicios correctamente separados, cobranza y soporte integrados, diagnóstico de dependencias, inventario y configuración versionada, y evidencia suficiente para saber qué pasó, a quién afectó, quién cambió qué y cómo recuperarlo.

**SI AL FINAL SÓLO HAY NODOS BONITOS, LA TAREA NO ESTÁ TERMINADA.**
