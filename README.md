# AtlasNOC

Cabina operacional ISP/WISP basada en evidencia: discovery, inventario, topología, monitoreo, clientes, servicios, billing y soporte.

## Desarrollo

Requiere .NET 8 y MySQL 8. Configura `ConnectionStrings__DefaultConnection` fuera del repositorio, ejecuta `dotnet restore`, `dotnet build AtlasNOC.sln -c Release` y aplica migraciones controladas con `dotnet ef database update`. LabMode sólo se activa explícitamente en Testing/LAB.

Consulta `docs/` para instalación, operación, seguridad, migraciones y matriz de verdad del producto.

## Flujo operativo

Web autentica operadores y expone MVC/API; Worker ejecuta discovery, polling y alertas. El flujo comercial persistente es Customer → ServicePlan → CustomerService → NetworkZone → InventoryAsset → Billing/Payment → Support. Un servicio puede cancelarse sin borrar al cliente ni sus otros servicios.

## Verificación local

Para validar el código usa `dotnet restore`, `dotnet build .\AtlasNOC.sln -c Release` y `dotnet test .\AtlasNOC.sln -c Release --no-build` con `ATLASNOC_TEST_CONNECTION` apuntando sólo a una base marcada `_test`, `_integration`, `_runtime` o `_e2e`. Se espera build sin warnings y cero fallos; un error de conexión o una migración pendiente indica configuración LAB incompleta, no éxito parcial.

El alcance probado, el backup/restore, el restart y las limitaciones de hardware/legal están en `docs/COMMERCIAL_READINESS.md`. La revisión humana debe aprobar secretos, privilegios DB, HTTPS, retención de backups, licencias y cualquier equipo físico antes de venta.
