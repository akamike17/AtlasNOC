# Commercial readiness

Evaluación contra el HEAD actual. `PROVEN` significa comprobado en software local; `PROVEN IN LAB` significa comprobado con MySQL/simulador/host LAB; los dos estados restantes requieren una acción externa concreta.

| Área | Estado | Evidencia | Riesgo residual | Acción humana previa a venta |
|---|---|---|---|---|
| Completeness | PROVEN IN LAB | 28 rutas MVC y smoke/API operativo | Hardware real no ejercitado | Aceptar alcance del release |
| Reliability | PROVEN IN LAB | Unit 178/178, Integration 10/10, Runtime 9/9, E2E 18/18 | Carga de producción no medida | Ejecutar prueba de carga objetivo |
| Security | PROVEN | Identity, antiforgery, rate limit, CSP/HSTS, error sanitizado y secret scan | Pentest externo no realizado | Revisión de seguridad |
| Auth | PROVEN IN LAB | Login, lockout, roles y API-key scopes E2E | IdP corporativo no integrado | Configurar política corporativa |
| Data integrity | PROVEN IN LAB | FK/unique, billing idempotency concurrente (1 cargo + 1 replay) y RowVersion asset | Operación humana puede introducir datos erróneos | Capacitar operadores |
| Installability | PROVEN | Guía Windows .NET 8/MySQL y migraciones | Instalador MSI no incluido | Definir empaquetado |
| Configuration | PROVEN | Variables, fail-fast, `LabMode=false`, Web/Worker separados | Secret store depende del entorno | Provisionar secret store |
| Upgrade/migrations | PROVEN IN LAB | Fresh y upgrade a `20260915154954` sin reset | Rollback de schema requiere backup probado | Aprobar ventana/rollback |
| Backup/recovery | PROVEN IN LAB | mysqldump, restore, SHA-256 y conteo de tablas | Retención externa no configurada | Elegir destino/retención |
| Restart/recovery | PROVEN IN LAB | E2E mata/relanza Web y valida health/login | Orquestador externo no probado | Definir servicio Windows |
| Observability | PROVEN IN LAB | health live/ready, logs, workers, discovery/poll status | Alerting externo no conectado | Configurar monitorización |
| UX/browser | PROVEN IN LAB | Playwright de vistas, acciones, reload y errores | Accesibilidad formal no auditada | Revisión UX/a11y |
| Performance LAB | PROVEN IN LAB | 4 zonas y 100 clientes/servicios; 61 nodos/60 links | No es benchmark comercial | Medir SLA objetivo |
| DB requirements | PROVEN | MySQL 8, usuario dedicado y migration guard | HA/replicación no configurada | Diseñar HA |
| Hardware | NOT PROVEN — EXTERNAL DEPENDENCY | Drivers LAB simulados y estado Unsupported explícito | No hay equipo físico autorizado | Probar modelos/firmware |
| Integrations | PROVEN IN LAB | SNMP/fingerprint simulado, API keys y scopes | Sistemas externos no conectados | Validar cada integración |
| Supportability | PROVEN IN LAB | Runbook, error correlation, tickets/incidents/visits | Guardias reales no definidas | Crear proceso de soporte |
| Documentation | PROVEN | Docs técnicos, instalación, operación, recuperación y matriz UI | Debe versionarse con cada release | Aprobar documentación |
| Licensing | REQUIRES HUMAN/LEGAL REVIEW | Dependencias y assets listados en `LICENSE_INVENTORY.md` | Algunos notices/licencias exactos requieren confirmar metadata | Revisar redistribución legal |
| Demo readiness | PROVEN IN LAB | Dataset 4 zonas × mínimo 5; fixture real 100/25 por zona; browser smoke | Datos no son clientes reales | Preparar entorno demo |

## Configuración de producción

Production requiere .NET 8, MySQL externo, `ASPNETCORE_ENVIRONMENT=Production`, `LabMode=false`, HTTPS/cookies seguras, Data Protection persistente, secretos externos, Web y Worker separados, health/readiness y migraciones ejecutadas en ventana con backup previo. El repositorio no contiene rutas absolutas del desarrollador ni credenciales versionadas.

## Veredicto de alcance

La aplicación está demostrada para el alcance de software LAB descrito. Hardware físico y revisión legal de licencias siguen siendo decisiones externas explícitas; no se presentan como software faltante ni como capacidades probadas.
