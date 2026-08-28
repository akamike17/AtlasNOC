Continúa AtlasNOC desde el estado REAL actual del repositorio. NO reinicies el proyecto, NO reescribas componentes ya funcionales y NO reviertas cambios de la sesión anterior salvo que una prueba demuestre que están mal.

ESTADO YA IMPLEMENTADO Y ACEPTADO COMO BASELINE, PERO DEBES VERIFICARLO:

* Discovery real conectado con topología.
* Coordenadas deterministas.
* Interfaces descubiertas incorporadas a nodos.
* Correlación de dispositivos por IP, hostname único y MAC.
* Enlaces LLDP/CDP/ARP creados únicamente con evidencia inequívoca.
* Confianza y protocolo preservados.
* Enlaces ambiguos descartados.
* IDs de enlaces estables entre reconstrucciones.
* Seguridad de NotificationChannels:

  * secretos/configuración sensible cifrados con Data Protection;
  * respuestas/API redactadas;
  * headers HTTP aislados por request, sin contaminar DefaultRequestHeaders;
  * webhook exige destino HTTPS seguro;
  * canales no implementados fallan explícitamente y no reportan éxito falso.
* Polling:

  * parsing IF-MIB funcional;
  * contadores/estado de interfaces;
  * latencia SNMP;
  * transición Device Down al fallar;
  * apertura de una sola alerta correspondiente;
  * recuperación Device Up;
  * resolución automática de la alerta;
  * persistencia de muestras de ambos ciclos.
* Series históricas:

  * modelo persistente;
  * servicio/API paginada por dispositivo y rango;
  * índices;
  * retención acotada;
  * FK hacia Devices con cascade;
  * migración EF generada e inspeccionada;
  * migración anterior de métricas aplicada correctamente a MySQL.
* SNMPv3:

  * ya NO usa hashes irreversibles como credenciales;
  * recibe passphrases válidas;
  * valida longitud mínima;
  * secretos cifrados reversiblemente y no expuestos por JSON;
  * authPriv real;
  * SHA-256;
  * AES;
  * descubrimiento de Engine ID;
  * secretos sólo se descifran al utilizarlos;
  * fallos UDP no tumban el poller.
* Parser IF-MIB corregido: las claves recibidas contienen columnas/índices y ya no se buscan erróneamente OIDs completos.
* Parsing LLDP/CDP implementado y probado.
* Persistencia de ejecuciones de discovery y dispositivos descubiertos implementada para sobrevivir reinicios.

ÚLTIMA EVIDENCIA VERDE CONOCIDA ANTES DEL ÚLTIMO BLOQUE:

* Build: 0 errores, 0 warnings.
* Tests: 52/52 passing.
  Después se modificaron aproximadamente 26 archivos (+990/-147) para completar SNMPv3, LLDP/CDP y persistencia de discovery/topología. La sesión terminó por límite de uso JUSTO cuando iba a aplicar la nueva migración local y verificar el esquema. Por tanto NO asumas que esos últimos cambios están terminados solamente porque compilan parcialmente.

TU PRIMER LOOP ES OBLIGATORIO:

1. Lee git status y git diff completos.
2. Identifica exactamente los 26 archivos modificados y separa:
   A) cambios completos/coherentes;
   B) cambios incompletos;
   C) cambios sospechosos;
   D) migraciones pendientes.
3. Lee los archivos afectados antes de modificarlos.
4. Compila TODO.
5. Ejecuta TODOS los tests.
6. Si falla algo, lee el error real completo, corrige la causa y repite build + tests.
7. No declares ninguna funcionalidad terminada sin evidencia.

PRIMER OBJETIVO CONCRETO:
Termina el bloque que Codex dejó interrumpido:

* sincroniza completamente el modelo EF;
* revisa la nueva migración generada;
* confirma PK/FK/índices/nullability/cascade;
* aplica la migración a la base local configurada SI la conexión ya existe y la acción es segura;
* verifica que el esquema resultante corresponde al modelo;
* prueba persistencia y reconstrucción de discovery/topología después de reinicio lógico;
* añade tests faltantes si la persistencia nueva no está cubierta.

NO crees claves administrativas temporales, no levantes endpoints HTTP inseguros y no intentes evadir restricciones del entorno para hacer smoke tests. Sustituye esas comprobaciones por tests de integración/locales cuando sea posible y deja documentado cualquier smoke externo que realmente requiera autorización.

DESPUÉS DEL PRIMER OBJETIVO, CONTINÚA AUTOMÁTICAMENTE. NO TE DETENGAS A PEDIRME QUÉ SIGUE.

Haz una auditoría del roadmap y busca stubs, TODO, NotImplementedException, datos falsos, mocks usados en producción, métodos que devuelven éxito sin ejecutar trabajo, servicios registrados pero no utilizados, endpoints incompletos, persistencia faltante, errores tragados, secretos en claro, posibles SSRF, concurrencia incorrecta y datos de red inventados.

ORDEN DE PRIORIDAD:

1. Corrección y seguridad.
2. Persistencia/reinicio.
3. Discovery real.
4. Polling/monitorización.
5. Topología.
6. Alertas.
7. Histórico.
8. API.
9. UI.
10. Observabilidad y producción.

DISCOVERY:

* Nada de inventar vecinos.
* LLDP/CDP sólo con evidencia real.
* ARP no debe confundirse con adyacencia física.
* Dedupe robusto por MAC/IP/hostname con reglas deterministas.
* Mantener procedencia/evidencia/confianza.
* Evitar duplicados en ejecuciones repetidas.
* Manejar dispositivos sin SNMP.
* Manejar interfaces administratively down vs operationally down.
* IPv4/IPv6 cuando el modelo lo permita.
* Timeouts/cancelación.
* No bloquear toda la ejecución por un dispositivo.

POLLING:

* Verifica counters de 32/64 bits.
* Detecta wrap/reset/reboot antes de calcular rates.
* Calcula utilización sólo si speed y delta temporal son válidos.
* No generar tasas negativas o absurdas.
* Persistir samples de forma consistente.
* Retención segura y eficiente.
* Polling concurrente con límites.
* CancellationToken.
* evitar overlapping polls del mismo device.
* tratar timeout/auth failure/unreachable de manera distinta cuando sea posible.

SNMPv3:

* Verifica que las claves/passphrases nunca aparezcan en logs, excepciones, DTOs o respuestas.
* Comprueba compatibilidad real con SHA-256/AES implementada.
* Engine boots/time y engine ID deben manejarse correctamente según la librería utilizada.
* No persistir material derivado innecesariamente.
* Diferenciar authNoPriv/authPriv si el modelo lo soporta.
* Nada de degradar silenciosamente a SNMPv2.

TOPOLOGÍA:

* IDs deterministas.
* enlaces estables.
* sin duplicados A-B/B-A.
* mantener protocolo/evidencia/confianza.
* reconstrucción idempotente.
* persistencia histórica si existe modelo para ello.
* un endpoint debe poder consultar la topología vigente sin ejecutar discovery obligatoriamente.

ALERTAS:

* deduplicación;
* open/ack/resolved consistente;
* recovery automático;
* evitar crear una alerta en cada ciclo por la misma condición;
* timestamps coherentes;
* consultar alertas históricas;
* no perder alertas tras reinicio.

NOTIFICACIONES:
No deshagas las protecciones existentes. Añade pruebas si faltan:

* cifrado real en repositorio;
* redacción API;
* SSRF;
* HTTPS;
* header isolation;
* timeouts;
* cancellation;
* retry limitado si ya existe infraestructura apropiada;
* no registrar secretos;
* Email y otros canales no implementados deben permanecer explícitamente como no soportados hasta que realmente envíen.

API:

* validación de inputs;
* pagination consistente;
* límites máximos;
* cancellation;
* respuestas de error útiles sin filtrar secretos/stack traces;
* endpoints históricos por rango;
* topología actual;
* estado de dispositivos;
* interfaces;
* discovery runs;
* alertas;
* métricas;
* readiness/health.

PERSISTENCIA:

* índices reales para consultas frecuentes;
* FK correctas;
* cascades sólo donde tenga sentido;
* UTC;
* evitar N+1 evidentes;
* transacciones donde una operación lógica requiera atomicidad;
* migrations reproducibles desde cero.

OBSERVABILIDAD:

* logs estructurados;
* correlation/run IDs;
* tiempos de discovery/poll;
* errores por dispositivo sin derribar el ciclo;
* métricas internas si ya existe infraestructura;
* nunca secrets.

UI:
Sólo cuando backend/dominio estén sólidos. No maquilles datos inexistentes. La UI debe representar estado real, loading, errores y empty states.

LOOP OBLIGATORIO DE TRABAJO:
READ → EDIT/WRITE → READ PARA VERIFICAR → BUILD → TEST → ANALIZAR RESULTADO → SIGUIENTE BLOQUE.

En PowerShell usa ";" y no "&&".

Si encuentras un error y existe una corrección segura:
NO TE DETENGAS EN EL DIAGNÓSTICO.
Corrígelo, vuelve a leer el archivo, compila y prueba.

Si build/test falla:
LEE primero la salida real.
NO empieces a cambiar cosas al azar.
NO agregues paquetes ni cambies arquitectura para esconder el error si el SDK/framework/configuración actual puede resolverlo.

Si uno de tus propios cambios resulta incorrecto:
REVIÉRTELO o corrígelo inmediatamente.

NO reduzcas cobertura para conseguir verde.
NO borres pruebas correctas.
NO cambies asserts para acomodar un bug.
NO uses sleeps arbitrarios.
NO reemplaces implementación real por mocks en producción.

CADA BLOQUE NUEVO DEBE TERMINAR CON:

* qué defecto real se encontró;
* qué archivos se cambiaron;
* qué comportamiento quedó implementado;
* build exacto;
* tests exactos;
* migraciones/esquema si aplica;
* siguiente hueco real encontrado.

PERO NO TE DETENGAS DESPUÉS DE REPORTARLO. CONTINÚA EL SIGUIENTE LOOP AUTOMÁTICAMENTE.

META:
Llevar AtlasNOC lo más cerca posible de producción real, no de una demo.

No uses porcentajes inventados. El porcentaje sólo puede subir cuando desaparecen requisitos verificables.

Antes de declarar 100% realiza una auditoría final completa:

* build Release limpio;
* 0 warnings;
* todos los tests;
* búsqueda de TODO/FIXME/NotImplemented/stubs;
* búsqueda de secretos hardcoded;
* migrations desde base vacía;
* comportamiento tras reinicio;
* discovery repetido/idempotencia;
* polling repeated/down/recovery;
* topología sin duplicados;
* historial;
* alertas;
* auth/autorización;
* SSRF;
* límites/paginación;
* manejo de cancellation/timeouts;
* logs sin secretos;
* dependencias vulnerables si la herramienta local lo permite.

No declares 100% si queda cualquier stub funcional, migración pendiente, prueba fallida, warning, dependencia crítica, endpoint que finja éxito o requisito del roadmap sin implementar.

Cuando ya no encuentres trabajo seguro adicional:
genera un informe FINAL muy corto con:

1. Build.
2. Tests.
3. Estado de migraciones.
4. Funcionalidades verificadas.
5. Riesgos/deuda restante.
6. Archivos todavía modificados sin commit.
7. Recomendación exacta para la auditoría posterior de Codex.

NO HAGAS COMMIT ni PUSH salvo que yo te lo pida expresamente.

Empieza ahora leyendo el estado real del repositorio y continúa en loops hasta agotar los huecos verificables.
