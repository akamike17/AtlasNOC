# Architecture
Domain contiene reglas; Application contratos; Infrastructure EF/MySQL, probes, drivers y workers; Web MVC/API/auth. Las dependencias apuntan hacia el dominio.

Web arranca migraciones y health; Worker mantiene el procesamiento separado en producción. Testing puede ejecutar workers dentro de Web sólo cuando `RunWorkersInWebForTests=true`. EF usa MySQL con retry strategy y las transacciones manuales se ejecutan dentro de `CreateExecutionStrategy`.
