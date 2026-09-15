# Seguridad

Las personas usan Identity/cookie; automatizaciones usan API keys con hash, scopes, expiración y revocación. Las credenciales de equipos se protegen con Data Protection. Las mutaciones requieren autorización, antiforgery donde aplica, validación, capability y auditoría. La aplicación añade rate limiting, CSP, HSTS/HTTPS fuera de Testing y respuestas sin stack trace.

La capacidad física de control sólo se clasifica como probada cuando existe driver, credencial, target explícito y verificación posterior.

Las mutaciones MVC usan antiforgery y autorización por rol; la API usa policies/API keys separadas. Las claves se almacenan como hash y validan scope, expiración y revocación. Las credenciales de equipos usan Data Protection.

Rate limiting protege login/setup y el middleware de excepción API registra endpoint, root/inner exception y DbUpdate sin enviar secretos. La prueba unitaria de secretos impide introducir cadenas de conexión con credenciales en archivos versionados.
