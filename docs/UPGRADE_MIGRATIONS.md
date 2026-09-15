# Migraciones y upgrade

La cadena EF es ordenada por timestamp. `20260915133611_BillingEntryIdempotency` incorpora el modelo comercial y sus tablas porque las migraciones anteriores no las tenían; no debe ejecutarse sobre una base que ya marque esa migración como aplicada. La migración posterior de zonas agrega `NetworkZones` e índice único de código.

Para verificar: `dotnet ef migrations list`, aplicar 0→HEAD en DB LAB nueva, aplicar upgrade desde una copia de la versión anterior y revisar tablas/índices. Nunca usar DROP/TRUNCATE/RESET contra producción.
