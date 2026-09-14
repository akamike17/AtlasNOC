using AtlasNOC.Application.Probes;
using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Web.Controllers.Api;

[ApiController]
[Route("api/lab/control")]
[Authorize(Roles = "Administrator")]
public sealed class LabControlApiController : ControllerBase
{
    private readonly IWebHostEnvironment _environment;
    private readonly ILabNetworkControl _control;
    private readonly AtlasNOCDbContext _db;

    public LabControlApiController(IWebHostEnvironment environment, ILabNetworkControl control, AtlasNOCDbContext db)
    {
        _environment = environment;
        _control = control;
        _db = db;
    }

    [HttpPost("{ipAddress}/reachable/{reachable:bool}")]
    public IActionResult Set(string ipAddress, bool reachable)
    {
        if (!_environment.IsEnvironment("Testing")) return NotFound();
        if (!IPAddress.TryParse(ipAddress, out var address)
            || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return BadRequest("La dirección LAB debe ser una IPv4 válida.");
        _control.SetReachability(ipAddress, reachable);
        return NoContent();
    }

    [HttpPost("scenario/operations")]
    public async Task<IActionResult> OperationsScenario(CancellationToken ct)
    {
        if (!_environment.IsEnvironment("Testing")) return NotFound();
        var plan = new ServicePlan("LAB-100", 100, 100, 20);
        var customer = new Customer($"LAB-{Guid.NewGuid():N}"[..16], "Cliente laboratorio", "5550000000");
        _db.ServicePlans.Add(plan); _db.Customers.Add(customer); _db.BillingAccounts.Add(new BillingAccount(customer.Id));
        var service = new CustomerService(customer.Id, plan.Id, "Domicilio LAB");
        service.Activate();
        _db.CustomerServices.Add(service);
        _db.InventoryAssets.Add(new InventoryAsset($"LAB-{Guid.NewGuid():N}"[..12], "CPE", null, "AA:BB:CC:DD:EE:01"));
        _db.CpeAuthorizationCases.Add(new CpeAuthorizationCase("AA:BB:CC:DD:EE:02", "AP-LAB", "192.0.2.20", -60, 25, service.Id));
        await _db.SaveChangesAsync(ct);
        return Ok(new { CustomerId = customer.Id, ServiceId = service.Id, PlanId = plan.Id, Message = "Escenario LAB creado; no modifica dispositivos físicos." });
    }
}
