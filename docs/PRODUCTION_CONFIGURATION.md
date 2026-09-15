# Configuración de producción

Usa `ASPNETCORE_ENVIRONMENT=Production`, conexión MySQL externa, HTTPS terminado por proxy confiable, Data Protection persistente y secretos del almacén del sistema. Mantén `LabMode=false`. Los perfiles Discovery, Polling y Notifications son configurables. No expongas credenciales, API keys ni cadenas de conexión en logs, vistas o respuestas.
