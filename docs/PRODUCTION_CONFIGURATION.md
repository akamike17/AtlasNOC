# Configuración de producción

Usa `ASPNETCORE_ENVIRONMENT=Production`, conexión MySQL externa, HTTPS terminado por proxy confiable, Data Protection persistente y secretos del almacén del sistema. Mantén `LabMode=false`. Los perfiles Discovery, Polling y Notifications son configurables. No expongas credenciales, API keys ni cadenas de conexión en logs, vistas o respuestas.

Production debe fallar rápido si falta la conexión, usar cookies seguras y HTTPS, y conservar claves de Data Protection fuera del árbol de publicación. El middleware de errores no devuelve stack traces; el middleware API conserva la excepción en logs correlacionados.

Web y Worker se despliegan como procesos separados con health/readiness. Los secretos se inyectan desde el almacén del sistema y los archivos versionados no contienen cadenas con credenciales.
