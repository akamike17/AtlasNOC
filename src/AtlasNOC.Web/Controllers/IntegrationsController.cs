using AtlasNOC.Domain.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AtlasNOC.Web.Controllers;

// Landing de integraciones: credenciales, API keys, canales de notificación, drivers.
// Lectura disponible para operadores y read-only; la gestión concreta de cada recurso
// vive en sus controllers propios (Credentials, ApiKeys) con su propia autorización.
[Authorize(Roles = ApplicationRole.Administrator + "," + ApplicationRole.NocOperator + "," + ApplicationRole.ReadOnly)]
public class IntegrationsController : Controller
{
    [HttpGet("integrations")]
    public IActionResult Index() => View();
}