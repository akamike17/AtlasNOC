# Instalación Windows

Instala .NET 8 Hosting Bundle y MySQL 8. Crea una base dedicada y un usuario de privilegio mínimo para esa base; nunca pongas la contraseña en Git. Define `ConnectionStrings__DefaultConnection` como secreto del servicio. Ejecuta `dotnet ef database update` desde un entorno de mantenimiento, inicia Web y Worker como servicios separados y valida `/health/live`, `/health/ready` y `/setup`.

Fresh install y upgrade se ejecutan sólo contra una base nueva o una copia de LAB; no se debe borrar una base real.

El procedimiento operativo es: crear DB y usuario con privilegio mínimo, inyectar la cadena por el almacén de secretos del servicio, ejecutar migraciones, arrancar Web/Worker separados y comprobar live/ready. Web usa `RunWorkersInWebForTests` únicamente en Testing.

Antes de upgrade se genera backup, se verifica checksum, se revisa la lista de migraciones y se valida schema/health después. Production no usa `LabMode` ni rutas absolutas del desarrollador.
