# Architecture
Domain contiene reglas; Application contratos; Infrastructure EF/MySQL, probes, drivers y workers; Web MVC/API/auth. Las dependencias apuntan hacia el dominio.

Web arranca migraciones y health; Worker mantiene el procesamiento separado en producción. Testing puede ejecutar workers dentro de Web sólo cuando `RunWorkersInWebForTests=true`. EF usa MySQL con retry strategy y las transacciones manuales se ejecutan dentro de `CreateExecutionStrategy`.

El flujo de dependencia es Domain → Application → Infrastructure → Web/Worker. El DbContext es la frontera de persistencia; controllers no reciben credenciales de equipo ni ejecutan acciones físicas sin pasar por servicios/capability. Los logs son estructurados y el middleware API conserva la excepción raíz para diagnóstico.

El arranque aplica migraciones sólo según la política de entorno y expone `/health/live` y `/health/ready`. Para pruebas se inyecta una conexión aislada mediante `ATLASNOC_TEST_CONNECTION`; el fixture nunca usa una base sin marcador seguro.

Una transacción manual sobre MySQL debe abrirse dentro de `Database.CreateExecutionStrategy().ExecuteAsync`; de otro modo el retry strategy produce una excepción interna que no se debe enmascarar como 405. El middleware registra endpoint, excepción raíz/inner y error EF sin devolver secretos.

El resultado esperado es una unidad compilable con Web y Worker desplegables por separado. Un dispositivo sin capability o credencial no atraviesa la frontera de control físico y queda como `SIMULATED` o `UNSUPPORTED`.
