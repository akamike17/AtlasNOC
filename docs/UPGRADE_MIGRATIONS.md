# Migraciones y upgrade

La cadena EF es ordenada por timestamp. `20260915133611_BillingEntryIdempotency` incorpora el modelo comercial y sus tablas porque las migraciones anteriores no las tenían; no debe ejecutarse sobre una base que ya marque esa migración como aplicada. La migración posterior de zonas agrega `NetworkZones` e índice único de código.

Para verificar: `dotnet ef migrations list`, aplicar 0→HEAD en DB LAB nueva, aplicar upgrade desde una copia de la versión anterior y revisar tablas/índices. Nunca usar DROP/TRUNCATE/RESET contra producción.

La ejecución de aceptación creó `atlasnoc_test_upgrade_lab`, aplicó hasta `20260915145841_AttachZonesToServices`, y después aplicó únicamente `20260915154954_AddInventoryAssetConcurrency`. La lista final no dejó migraciones pendientes.

La migración de concurrencia añade `InventoryAssets.RowVersion` como `timestamp(6)` generado por MySQL; no redefine tablas comerciales anteriores.
