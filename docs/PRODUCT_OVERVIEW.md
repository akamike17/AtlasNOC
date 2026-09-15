# Product overview
AtlasNOC es una cabina ISP/WISP para descubrir, correlacionar, monitorear y operar infraestructura y sus servicios comerciales con evidencia auditable.

El producto separa evidencia de red de capacidad de control: discovery LAB persiste dispositivos, interfaces, observaciones y enlaces; operaciones comerciales persisten Customer, Service, Asset, BillingAccount, tickets y créditos. Las vistas MVC y API requieren autenticación según rol.

El camino demostrable es setup/login, health, discovery, topology, alertas/incidentes, zonas/capacidad, clientes/servicios, assets/billing/support y recuperación LAB. Hardware físico no conectado no se presenta como controlado.

El operador inicia en Web; el Worker separado reclama leases y ejecuta polling/discovery. La API original de zonas es `POST /api/operations/zones` y el grafo se lee de `/api/topology/graph`; ninguna ruta alternativa sustituye esos contratos.

Configuración mínima: .NET 8, MySQL 8, `ConnectionStrings__DefaultConnection` en un almacén de secretos y `LabMode=true` sólo en Testing/LAB. En Production se exige HTTPS, Data Protection persistente y Web/Worker separados.

Evidencia reproducible: `dotnet test .\AtlasNOC.sln -c Release --no-build` y el script de backup/restore LAB. Si falta MySQL, el health/readiness falla de forma visible; el producto no simula disponibilidad comercial.
