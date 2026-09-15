# Instalación Windows

Instala .NET 8 Hosting Bundle y MySQL 8. Crea una base dedicada y un usuario de privilegio mínimo para esa base; nunca pongas la contraseña en Git. Define `ConnectionStrings__DefaultConnection` como secreto del servicio. Ejecuta `dotnet ef database update` desde un entorno de mantenimiento, inicia Web y Worker como servicios separados y valida `/health/live`, `/health/ready` y `/setup`.

Fresh install y upgrade se ejecutan sólo contra una base nueva o una copia de LAB; no se debe borrar una base real.
