# AtlasNOC

Cabina operacional ISP/WISP basada en evidencia: discovery, inventario, topología, monitoreo, clientes, servicios, billing y soporte.

## Desarrollo

Requiere .NET 8 y MySQL 8. Configura `ConnectionStrings__DefaultConnection` fuera del repositorio, ejecuta `dotnet restore`, `dotnet build AtlasNOC.sln -c Release` y aplica migraciones controladas con `dotnet ef database update`. LabMode sólo se activa explícitamente en Testing/LAB.

Consulta `docs/` para instalación, operación, seguridad, migraciones y matriz de verdad del producto.
