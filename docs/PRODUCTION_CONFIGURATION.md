# Configuración de producción

Usa `ASPNETCORE_ENVIRONMENT=Production`, conexión MySQL externa, HTTPS terminado por proxy confiable, Data Protection persistente y secretos del almacén del sistema. Mantén `LabMode=false`. Los perfiles Discovery, Polling y Notifications son configurables. No expongas credenciales, API keys ni cadenas de conexión en logs, vistas o respuestas.

Production debe fallar rápido si falta la conexión, usar cookies seguras y HTTPS, y conservar claves de Data Protection fuera del árbol de publicación. El middleware de errores no devuelve stack traces; el middleware API conserva la excepción en logs correlacionados.

Web y Worker se despliegan como procesos separados con health/readiness. Los secretos se inyectan desde el almacén del sistema y los archivos versionados no contienen cadenas con credenciales.

Antes de arrancar se comprueba que la ruta de logs y el directorio de Data Protection sean escribibles por la identidad del servicio, que la conexión use un usuario MySQL de privilegio mínimo y que el proxy reenvíe HTTPS correctamente. `LabMode` no debe estar habilitado en Production.

El cambio de schema es: backup externo verificable → revisar `dotnet ef migrations list` → `dotnet ef database update` en ventana aprobada → health/readiness y smoke de login → conservar plan de rollback de aplicación. El rollback de schema no se promete automáticamente; requiere restaurar una copia aprobada.

Si falta conexión, secreto, certificado o clave persistente, el arranque debe fallar rápido y dejar el motivo en logs sanitizados. No se usan rutas absolutas del equipo de desarrollo ni credenciales en `appsettings` versionado.
