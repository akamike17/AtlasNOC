# Test strategy
Unit valida reglas; Integration EF/MySQL; Runtime workers/simulador/restart; E2E navegador y API. Cada suite registra fallos, respuestas inesperadas, errores JS y secretos.

Las suites reciben `ATLASNOC_TEST_CONNECTION`, derivan sufijos seguros y no pueden apuntar accidentalmente a producción. Integration valida índices, migraciones, setup, credenciales, API keys y concurrencia de assets; Runtime valida discovery, polling, alertas, restart y escala; E2E valida setup/login, browser, REST original y restart.

La aceptación requiere build Release sin warnings y cero fallos en la ejecución LAB; una dependencia externa sólo se marca no probada después de completar el software preparatorio.

El comando de regresión usa `dotnet restore`, `dotnet build .\\AtlasNOC.sln -c Release` y `dotnet test .\\AtlasNOC.sln -c Release --no-build`. Las bases se derivan con sufijos `_integration`, `_runtime` y `_e2e`; sólo se recrean bases de prueba.
