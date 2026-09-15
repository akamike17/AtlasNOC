# Migraciones y upgrade

La cadena EF es ordenada por timestamp. `20260915133611_BillingEntryIdempotency` incorpora el modelo comercial y sus tablas porque las migraciones anteriores no las tenían; no debe ejecutarse sobre una base que ya marque esa migración como aplicada. Las migraciones posteriores agregan `NetworkZones`, concurrencia de assets y el vínculo de `SupportTicket` con `CustomerService`.

Para verificar: `dotnet ef migrations list`, aplicar 0→HEAD en DB LAB nueva, aplicar upgrade desde una copia de la versión anterior y revisar tablas/índices. Nunca usar DROP/TRUNCATE/RESET contra producción.

La ejecución fresh creó `atlasnoc_test_remate_fresh_20260915` y llegó a `20260915230648_AttachSupportTicketsToServices`. La ejecución upgrade creó `atlasnoc_test_remate_upgrade_20260915`, aplicó primero hasta `20260915154954_AddInventoryAssetConcurrency` y después aplicó únicamente `20260915230648_AttachSupportTicketsToServices`; ambas quedaron sin migraciones pendientes.

La migración de concurrencia añade `InventoryAssets.RowVersion` como `timestamp(6)` generado por MySQL. La migración final añade `SupportTickets.CustomerServiceId` nullable, índice y FK `SET NULL`; no redefine ni borra tickets históricos.
