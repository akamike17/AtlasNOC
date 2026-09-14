using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Infrastructure.Persistence;
using AtlasNOC.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace AtlasNOC.Web.Controllers.Api;

[ApiController]
[Route("api/operations")]
[Authorize(AuthenticationSchemes = "Identity.Application,ApiKey", Policy = "Api.TopologyRead")]
[EnableRateLimiting("api")]
public sealed class OperationsApiController : ControllerBase
{
    private readonly IOperationsSnapshotService _snapshot;
    private readonly AtlasNOCDbContext _db;
    private readonly ITechnicianRoutePlanner _routes;
    public OperationsApiController(IOperationsSnapshotService snapshot, AtlasNOCDbContext db, ITechnicianRoutePlanner routes) { _snapshot = snapshot; _db = db; _routes = routes; }

    [HttpGet("snapshot")]
    public Task<OperationsSnapshotDto> Snapshot(CancellationToken ct) => _snapshot.GetAsync(ct);

    [HttpGet("customers")]
    public async Task<IActionResult> Customers(CancellationToken ct) => Ok(await _db.Customers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));

    [HttpGet("services")]
    public async Task<IActionResult> Services(CancellationToken ct) => Ok(await _db.CustomerServices.AsNoTracking().OrderByDescending(x => x.ActivatedAtUtc).ToListAsync(ct));

    [HttpGet("billing/{customerId:guid}")]
    public async Task<IActionResult> Billing(Guid customerId, CancellationToken ct)
    { var account = await _db.BillingAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == customerId, ct); if (account is null) return NotFound(); return Ok(new { account.Id, account.CustomerId, account.Balance, Entries = await _db.BillingEntries.AsNoTracking().Where(x => x.AccountId == account.Id).OrderByDescending(x => x.OccurredAtUtc).ToListAsync(ct) }); }

    [HttpGet("tickets")]
    public async Task<IActionResult> Tickets(CancellationToken ct) => Ok(await _db.SupportTickets.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct));

    [HttpGet("assets")]
    public async Task<IActionResult> Assets(CancellationToken ct) => Ok(await _db.InventoryAssets.AsNoTracking().OrderBy(x => x.AssetTag).ToListAsync(ct));

    [HttpGet("coverage")]
    public async Task<IActionResult> CoverageList(CancellationToken ct) => Ok(await _db.CoverageChecks.AsNoTracking().OrderByDescending(x => x.CheckedAtUtc).ToListAsync(ct));

    [HttpGet("visits")]
    public async Task<IActionResult> Visits(CancellationToken ct) => Ok(await _db.TechnicianVisits.AsNoTracking().OrderBy(x => x.ScheduledAtUtc).ToListAsync(ct));

    [HttpPost("visits/route")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> Route([FromBody] RouteRequest request, CancellationToken ct) => Ok(await _routes.PlanAsync(request.VisitIds, ct));

    [HttpPost("customers")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request, CancellationToken ct)
    { if (await _db.Customers.AnyAsync(x => x.ServiceCode == request.ServiceCode, ct)) return Conflict("ServiceCode ya existe."); var customer = new Customer(request.ServiceCode, request.Name, request.Phone, request.Email); _db.Customers.Add(customer); _db.BillingAccounts.Add(new BillingAccount(customer.Id)); await _db.SaveChangesAsync(ct); return Created($"/api/operations/customers/{customer.Id}", customer); }

    [HttpPost("plans")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest request, CancellationToken ct)
    { var plan = new ServicePlan(request.Name, request.MonthlyPrice, request.DownloadMbps, request.UploadMbps); _db.ServicePlans.Add(plan); await _db.SaveChangesAsync(ct); return Ok(plan); }

    [HttpPost("services")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> CreateService([FromBody] CreateServiceRequest request, CancellationToken ct)
    { if (!await _db.Customers.AnyAsync(x => x.Id == request.CustomerId, ct) || !await _db.ServicePlans.AnyAsync(x => x.Id == request.PlanId && x.IsActive, ct)) return BadRequest("Cliente o plan inválido."); var service = new CustomerService(request.CustomerId, request.PlanId, request.Address); _db.CustomerServices.Add(service); await _db.SaveChangesAsync(ct); return Ok(service); }

    [HttpPost("services/{id:guid}/activate")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct) => await ChangeService(id, s => s.Activate(), ct);
    [HttpPost("services/{id:guid}/suspend")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct) => await ChangeService(id, s => s.Suspend(), ct);
    [HttpPost("services/{id:guid}/reconnect")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> Reconnect(Guid id, CancellationToken ct) => await ChangeService(id, s => s.Reconnect(), ct);

    [HttpPost("billing/{customerId:guid}/charge")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public Task<IActionResult> Charge(Guid customerId, [FromBody] BillingRequest request, CancellationToken ct) => AddLedger(customerId, LedgerEntryType.Charge, request, ct);
    [HttpPost("billing/{customerId:guid}/payment")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public Task<IActionResult> Payment(Guid customerId, [FromBody] BillingRequest request, CancellationToken ct) => AddLedger(customerId, LedgerEntryType.Payment, request, ct);

    [HttpPost("billing/{customerId:guid}/monthly-charge")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> MonthlyCharge(Guid customerId, CancellationToken ct)
    {
        var service = await _db.CustomerServices.Where(x => x.CustomerId == customerId && x.Status != ServiceStatus.Cancelled).OrderByDescending(x => x.ActivatedAtUtc).FirstOrDefaultAsync(ct);
        if (service is null) return BadRequest("El cliente no tiene servicio activo.");
        var plan = await _db.ServicePlans.FindAsync(new object[] { service.PlanId }, ct);
        return plan is null ? BadRequest("El plan no existe.") : await AddLedger(customerId, LedgerEntryType.Charge, new BillingRequest(plan.MonthlyPrice, $"Mensualidad {plan.Name}"), ct);
    }

    [HttpPost("services/{id:guid}/pay-and-reconnect")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> PayAndReconnect(Guid id, [FromBody] BillingRequest request, CancellationToken ct)
    {
        var service = await _db.CustomerServices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (service is null) return NotFound();
        var payment = await AddLedger(service.CustomerId, LedgerEntryType.Payment, request, ct);
        var account = await _db.BillingAccounts.FirstAsync(x => x.CustomerId == service.CustomerId, ct);
        if (account.Balance > 0) return BadRequest(new { Message = "Pago registrado, pero el saldo sigue vencido.", account.Balance });
        service.Reconnect(); await _db.SaveChangesAsync(ct); return payment;
    }

    [HttpPost("tickets")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> Ticket([FromBody] TicketRequest request, CancellationToken ct) { if (!await _db.Customers.AnyAsync(x => x.Id == request.CustomerId, ct)) return BadRequest("Cliente inválido."); var ticket = new SupportTicket(request.CustomerId, request.Title, request.Description); _db.SupportTickets.Add(ticket); await _db.SaveChangesAsync(ct); return Ok(ticket); }
    [HttpPost("assets")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> Asset([FromBody] AssetRequest request, CancellationToken ct) { var asset = new InventoryAsset(request.AssetTag, request.Type, request.SerialNumber, request.MacAddress); _db.InventoryAssets.Add(asset); await _db.SaveChangesAsync(ct); return Ok(asset); }
    [HttpPost("coverage")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> Coverage([FromBody] CoverageRequest request, CancellationToken ct) { var check = new CoverageCheck(request.Address, request.Status, request.CapacityMbps); _db.CoverageChecks.Add(check); await _db.SaveChangesAsync(ct); return Ok(check); }
    [HttpPost("visits")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> Visit([FromBody] VisitRequest request, CancellationToken ct) { if (!await _db.Customers.AnyAsync(x => x.Id == request.CustomerId, ct)) return BadRequest("Cliente inválido."); var visit = new TechnicianVisit(request.CustomerId, request.ScheduledAtUtc, request.WorkType, request.EstimatedMinutes); _db.TechnicianVisits.Add(visit); await _db.SaveChangesAsync(ct); return Ok(visit); }

    private async Task<IActionResult> ChangeService(Guid id, Action<CustomerService> change, CancellationToken ct) { var service = await _db.CustomerServices.FirstOrDefaultAsync(x => x.Id == id, ct); if (service is null) return NotFound(); change(service); await _db.SaveChangesAsync(ct); return Ok(service); }
    private async Task<IActionResult> AddLedger(Guid customerId, LedgerEntryType type, BillingRequest request, CancellationToken ct) { var account = await _db.BillingAccounts.FirstOrDefaultAsync(x => x.CustomerId == customerId, ct); if (account is null || request.Amount <= 0) return BadRequest("Cuenta o monto inválido."); account.Apply(type, request.Amount); _db.BillingEntries.Add(new BillingEntry(account.Id, type, request.Amount, request.Description)); await _db.SaveChangesAsync(ct); return Ok(new { account.Id, account.Balance }); }
}

public sealed record CreateCustomerRequest(string ServiceCode, string Name, string? Phone, string? Email);
public sealed record CreatePlanRequest(string Name, decimal MonthlyPrice, int DownloadMbps, int UploadMbps);
public sealed record CreateServiceRequest(Guid CustomerId, Guid PlanId, string Address);
public sealed record BillingRequest(decimal Amount, string Description);
public sealed record TicketRequest(Guid CustomerId, string Title, string? Description);
public sealed record AssetRequest(string AssetTag, string Type, string? SerialNumber, string? MacAddress);
public sealed record CoverageRequest(string Address, CoverageStatus Status, int? CapacityMbps);
public sealed record VisitRequest(Guid CustomerId, DateTime ScheduledAtUtc, string WorkType, int EstimatedMinutes);
public sealed record RouteRequest(IReadOnlyList<Guid> VisitIds);
