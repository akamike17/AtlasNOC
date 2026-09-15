# Operations runbook
Validar configuración, migraciones, health live/ready, logs sin secretos, workers y último discovery/poll. Ante fallo, aislar el item, revisar evidencia y conservar historial.

Arranque: revisar entorno, aplicar migraciones, verificar `/health/live` y `/health/ready`, comprobar versión/schema y abrir `/setup` sólo durante instalación inicial. Mantener Web y Worker separados en Production.

Operación: revisar leases de DiscoveryRun, métricas recientes, alertas e incidentes. Topology debe mostrar nodos aislados cuando no hay observación; nunca añadir un link manual como confirmado.

Recuperación LAB: usar el script de backup/restore con base marcada; registrar SHA-256, conteo de tablas y health posterior. No resetear ni truncar bases reales.

Configuración y arranque: cargar secretos, confirmar `ASPNETCORE_ENVIRONMENT`, revisar la versión de schema, arrancar Web y Worker con identidades separadas y comprobar que ambos ven la misma base. El resultado esperado es live 200 y ready 200 sólo cuando DB y dependencias están sanas.

Durante operación, revisar leases, tiempos de polling, errores de discovery, alertas sin reconocer, cola de notificaciones y correlación de logs. Un error 405 en un POST debe investigarse como posible excepción interna preservando endpoint y `InnerException`; nunca se corrige añadiendo una ruta espejo.

En una incidencia, congelar evidencia, identificar Customer/Service/Asset afectado, evitar reintentos manuales no idempotentes y registrar la acción. Cualquier control físico requiere capability, credencial y verificación posterior explícitas.
