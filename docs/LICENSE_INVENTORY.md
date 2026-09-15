# Inventario de licencias y dependencias

Este inventario se deriva de `Directory.Packages.props` y de los assets locales de `src/AtlasNOC.Web/wwwroot/lib`. La versión es la fijada por el repositorio; la licencia debe verificarse contra el paquete exacto antes de redistribución comercial.

| Dependencia | Versión fijada | Uso | Verificación de licencia |
|---|---:|---|---|
| Microsoft.EntityFrameworkCore / Relational / Design | 8.0.13 | ORM, migraciones, tooling | revisar metadata NuGet del paquete exacto |
| Pomelo.EntityFrameworkCore.MySql | 8.0.3 | proveedor MySQL | revisar metadata NuGet del paquete exacto |
| Microsoft.AspNetCore.Identity.EntityFrameworkCore | 8.0.13 | usuarios/roles | revisar licencia Microsoft del paquete |
| Microsoft.AspNetCore.DataProtection.EntityFrameworkCore | 8.0.13 | claves persistentes | revisar licencia Microsoft del paquete |
| Microsoft.Extensions.Hosting/Http/Resilience | 8.0.1/8.0.1/8.10.0 | workers, HTTP, resiliencia | revisar metadata NuGet |
| Lextm.SharpSnmpLib | 12.5.7 | probe SNMP | revisar licencia del paquete exacto |
| Serilog.AspNetCore / Sinks.File | 8.0.2 / 6.0.0 | logging estructurado | revisar metadata NuGet |
| Swashbuckle.AspNetCore | 6.7.3 | OpenAPI | revisar metadata NuGet |
| Microsoft.Playwright | 1.50.0 | E2E browser | dependencia de test; revisar metadata |
| Chart.js / Cytoscape | assets locales | gráficas y topology UI | conservar notices de distribución en publicación |

No se usan CDNs obligatorios para la LAN: Chart.js y Cytoscape están bajo `wwwroot/lib`. La aplicación no incorpora código de licencia desconocida deliberadamente; cualquier licencia que no pueda confirmarse en la metadata del paquete debe bloquear la venta hasta revisión humana legal/comercial.
