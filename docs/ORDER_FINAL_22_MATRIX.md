# Matriz de verificación de la Orden Final (puntos 1–22)

Esta matriz separa el requisito de su evidencia. Un conteo de pruebas nunca sustituye la revisión del apartado. Las pruebas LAB usan bases con sufijo seguro y el host E2E selecciona un puerto loopback libre.

| Punto | Evidencia implementada y verificable |
|---:|---|
| 1 | Migraciones EF Core versionadas, snapshot actualizado, fixture fresh/upgrade y modelo Customer/Service/BillingAccount con servicios múltiples. |
| 2 | Idempotencia de ledger, referencias de pago, unicidad de asset y transacciones serializables; pruebas unitarias, integración y smoke. |
| 3 | Controllers, servicios, persistencia, validación, autorización y vistas para los módulos operativos; navegación primaria E2E. |
| 4 | Discovery conserva evidencia y estados de verdad; workers persisten observaciones y correlación no inventa links. |
| 5 | Fixture Runtime demuestra cuatro zonas y escala de clientes/servicios; smoke REST demuestra cuatro zonas originales. |
| 6 | NetworkZone, estados, capacidad, cobertura y reservas/release de capacidad en activación, cancelación y cambio de plan. |
| 7 | Flujo Customer→Plan→Service→Asset/CPE→activación con provisioning explícitamente Unsupported cuando no hay hardware controlable. |
| 8 | Billing ledger, cargos idempotentes, pagos, promesas, suspensión y recibos cubiertos por dominio e integración/E2E. |
| 9 | Tickets, interacciones, incidentes raíz, visitas y créditos requieren evidencia de cliente afectado. |
| 10 | Identity, roles, API keys con hash/scope/revoke/expiry, antiforgery, rate limits, cookies y middleware de errores sanitizado. |
| 11 | Configuración de producción, HTTPS fuera de Testing, health, workers, graceful shutdown y script LAB de backup/restore. |
| 12 | Documentación técnica versionable: arquitectura, dominio, instalación, operación, seguridad, backup, migraciones y estrategia de pruebas. |
| 13 | Innovation brief describe problema, propuesta, evidencia, límites de hardware y demo sin inventar capacidades. |
| 14 | Commercial readiness recoge criterios de completitud, confiabilidad, instalación, recuperación, observabilidad, UX y dependencias. |
| 15 | Dependencias NuGet/JS y assets locales quedan inventariadas en la documentación de proyecto; no se depende de CDN para LAN. |
| 16 | Guardas de conexión y script de backup rechazan bases sin marcador LAB/test; no se ejecuta reset de base real. |
| 17 | Solución completa ejecutada con variable LAB: Unit 178/178, Integration 8/8, Runtime 9/9, E2E 16/16; build limpio. |
| 18 | Playwright cubre setup, login, vistas operativas, acciones, persistencia, errores y captura de fallos de página/red. |
| 19 | El bucle READ→TRACE→REPRODUCE→IMPLEMENT→BUILD→TEST→E2E se aplicó a la causa raíz EF/retry y al contrato 39/39. |
| 20 | Hardware físico y servicios externos no se simulan como controlados: se etiquetan como estados explícitos de capability. |
| 21 | Git sin force/reset/clean destructivo; commits lógicos publicados en `origin/atlasnoc`; artefactos temporales fuera del índice. |
| 22 | Funciones, defectos, migraciones, billing, escala LAB, discovery, seguridad, E2E, recuperación y documentación tienen rutas de verificación reproducibles. |

## Comandos reproducibles

```powershell
$env:ATLASNOC_TEST_CONNECTION='Server=127.0.0.1;Port=3306;Database=atlasnoc_test_all;User=Admin;Password=<secret>;SslMode=None;'
dotnet build .\AtlasNOC.sln -c Release --no-restore
dotnet test .\AtlasNOC.sln -c Release --no-build
.\scripts\Invoke-AtlasNocLabBackupRestore.ps1 -Server 127.0.0.1 -User Admin -Password $env:MYSQL_LAB_PASSWORD -SourceDatabase atlasnoc_test -RestoreDatabase atlasnoc_lab_restore_test
```

La contraseña se inyecta desde el entorno y nunca forma parte del repositorio, argumentos guardados ni documentación ejecutable.
