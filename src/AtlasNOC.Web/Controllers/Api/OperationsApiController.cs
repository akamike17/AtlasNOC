using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Application.Devices;
using AtlasNOC.Infrastructure.Persistence;
using AtlasNOC.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.RegularExpressions;

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
    private readonly INetworkActionService _networkActions;
    public OperationsApiController(IOperationsSnapshotService snapshot, AtlasNOCDbContext db, ITechnicianRoutePlanner routes,
        INetworkActionService networkActions) { _snapshot = snapshot; _db = db; _routes = routes; _networkActions = networkActions; }

    [HttpGet("snapshot")]
    public Task<OperationsSnapshotDto> Snapshot(CancellationToken ct) => _snapshot.GetAsync(ct);

    [HttpGet("devices/{deviceId:guid}/actions/{deviceAction}/preview")]
    public async Task<IActionResult> PreviewAction(Guid deviceId, [FromRoute] DeviceAction deviceAction, [FromQuery] string? interfaceName,
        CancellationToken ct) => Ok(await _networkActions.PreviewAsync(deviceId, deviceAction, interfaceName, ct));

    [HttpGet("incidents")]
    public async Task<IActionResult> Incidents(CancellationToken ct) => Ok(await _db.Incidents.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct));

    [HttpGet("sla/{priority}")]
    public IActionResult Sla(IncidentPriority priority) => Ok(new SlaPolicy().For(priority));

    [HttpPost("incidents/root")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> CreateRootIncident([FromBody] RootIncidentRequest request, CancellationToken ct)
    {
        if (request.RootCauseDeviceId is not null && !Guid.TryParse(request.RootCauseDeviceId, out _)) return BadRequest("RootCauseDeviceId inválido.");
        var incident = new Incident(request.Title, User.Identity?.Name ?? "unknown", request.Description, request.RootCauseDeviceId);
        incident.MarkRootCauseCandidate();
        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync(ct);
        return Ok(incident);
    }

    [HttpPost("incidents/{id:guid}/priority")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> ReclassifyIncident(Guid id, [FromBody] IncidentPriorityRequest request, CancellationToken ct) { var incident = await _db.Incidents.FindAsync(new object[] { id }, ct); if (incident is null) return NotFound(); incident.Reclassify(request.Priority, request.Reason); await _db.SaveChangesAsync(ct); return Ok(incident); }

    [HttpGet("customers/list")]
    public async Task<IActionResult> Customers(CancellationToken ct) => Ok(await _db.Customers.AsNoTracking().OrderBy(x => x.Name).ToListAsync(ct));

    [HttpGet("services")]
    public async Task<IActionResult> Services(CancellationToken ct) => Ok(await _db.CustomerServices.AsNoTracking().OrderByDescending(x => x.ActivatedAtUtc).ToListAsync(ct));

    [HttpGet("billing/{customerId:guid}")]
    public async Task<IActionResult> Billing(Guid customerId, CancellationToken ct)
    { var account = await _db.BillingAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == customerId, ct); if (account is null) return NotFound(); return Ok(new { account.Id, account.CustomerId, account.Balance, Entries = await _db.BillingEntries.AsNoTracking().Where(x => x.AccountId == account.Id).OrderByDescending(x => x.OccurredAtUtc).ToListAsync(ct) }); }

    [HttpGet("billing/{customerId:guid}/receipts")]
    public async Task<IActionResult> Receipts(Guid customerId, CancellationToken ct) => Ok(await _db.PaymentReceipts.AsNoTracking().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.IssuedAtUtc).ToListAsync(ct));

    [HttpGet("tickets")]
    public async Task<IActionResult> Tickets(CancellationToken ct) => Ok(await _db.SupportTickets.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct));

    [HttpGet("assets")]
    public async Task<IActionResult> Assets(CancellationToken ct) => Ok(await _db.InventoryAssets.AsNoTracking().OrderBy(x => x.AssetTag).ToListAsync(ct));

    [HttpGet("coverage")]
    public async Task<IActionResult> CoverageList(CancellationToken ct) => Ok(await _db.CoverageChecks.AsNoTracking().OrderByDescending(x => x.CheckedAtUtc).ToListAsync(ct));

    [HttpGet("coverage/evaluate")]
    public async Task<IActionResult> EvaluateCoverage([FromQuery] string address, [FromQuery] int requiredMbps = 0, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address)) return BadRequest("Domicilio obligatorio.");
        var check = await _db.CoverageChecks.AsNoTracking().Where(x => x.Address == address).OrderByDescending(x => x.CheckedAtUtc).FirstOrDefaultAsync(ct);
        if (check is null) return Ok(new { Status = CoverageStatus.RequiresFieldValidation, CapacityMbps = (int?)null, Evidence = "No existe chequeo persistido." });
        var capacityOk = !check.CapacityMbps.HasValue || requiredMbps <= check.CapacityMbps.Value;
        var available = check.Status is CoverageStatus.Available or CoverageStatus.ProbablyAvailable;
        return Ok(new { Status = available && capacityOk ? check.Status : CoverageStatus.CapacityLimited, check.CapacityMbps, RequiredMbps = requiredMbps, Evidence = "Último chequeo persistido." });
    }

    [HttpGet("visits")]
    public async Task<IActionResult> Visits(CancellationToken ct) => Ok(await _db.TechnicianVisits.AsNoTracking().OrderBy(x => x.ScheduledAtUtc).ToListAsync(ct));

    [HttpGet("prospects")]
    public async Task<IActionResult> Prospects(CancellationToken ct) => Ok(await _db.Prospects.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct));

    [HttpPost("prospects")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> CreateProspect([FromBody] ProspectRequest request, CancellationToken ct) { var prospect = new Prospect(request.Name, request.Address, request.Phone); _db.Prospects.Add(prospect); await _db.SaveChangesAsync(ct); return Ok(prospect); }

    [HttpPost("prospects/{id:guid}/status")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> ProspectStatus(Guid id, [FromBody] ProspectStatusRequest request, CancellationToken ct) { var prospect = await _db.Prospects.FindAsync(new object[] { id }, ct); if (prospect is null) return NotFound(); prospect.SetStatus(request.Status); await _db.SaveChangesAsync(ct); return Ok(prospect); }

    [HttpGet("billing/{customerId:guid}/promises")]
    public async Task<IActionResult> Promises(Guid customerId, CancellationToken ct) => Ok(await _db.PaymentPromises.AsNoTracking().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.ExpiresAtUtc).ToListAsync(ct));

    [HttpGet("cpe-cases")]
    public async Task<IActionResult> CpeCases(CancellationToken ct) => Ok(await _db.CpeAuthorizationCases.AsNoTracking().OrderByDescending(x => x.DetectedAtUtc).ToListAsync(ct));

    [HttpGet("contracts/{customerId:guid}")]
    public async Task<IActionResult> Contracts(Guid customerId, CancellationToken ct) => Ok(await _db.ServiceContracts.AsNoTracking().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct));

    [HttpPost("contracts")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> CreateContract([FromBody] ContractRequest request, CancellationToken ct) { if (!await _db.CustomerServices.AnyAsync(x => x.Id == request.ServiceId && x.CustomerId == request.CustomerId, ct)) return BadRequest("Servicio inválido."); var contract = new ServiceContract(request.CustomerId, request.ServiceId, request.Terms); _db.ServiceContracts.Add(contract); await _db.SaveChangesAsync(ct); return Ok(contract); }

    [HttpPost("contracts/{id:guid}/accept")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> AcceptContract(Guid id, CancellationToken ct) { var contract = await _db.ServiceContracts.FindAsync(new object[] { id }, ct); if (contract is null) return NotFound(); contract.Accept(User.Identity?.Name ?? "unknown"); await _db.SaveChangesAsync(ct); return Ok(contract); }

    [HttpGet("installations")]
    public async Task<IActionResult> Installations(CancellationToken ct) => Ok(await _db.InstallationOrders.AsNoTracking().OrderByDescending(x => x.ScheduledAtUtc).ToListAsync(ct));

    [HttpGet("support/interactions/{ticketId:guid}")]
    public async Task<IActionResult> Interactions(Guid ticketId, CancellationToken ct) => Ok(await _db.SupportInteractions.AsNoTracking().Where(x => x.TicketId == ticketId).OrderByDescending(x => x.StartedAtUtc).ToListAsync(ct));

    [HttpPost("support/interactions")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> AddInteraction([FromBody] InteractionRequest request, CancellationToken ct) { if (!await _db.SupportTickets.AnyAsync(x => x.Id == request.TicketId, ct)) return BadRequest("Ticket inválido."); var item = new SupportInteraction(request.TicketId, request.Channel, request.Symptoms, request.Diagnosis, request.Actions, User.Identity?.Name ?? "unknown", request.Result, request.DurationMinutes, request.RootIncidentId); _db.SupportInteractions.Add(item); await _db.SaveChangesAsync(ct); return Ok(item); }

    [HttpGet("credits/{customerId:guid}")]
    public async Task<IActionResult> Credits(Guid customerId, CancellationToken ct) => Ok(await _db.ServiceCredits.AsNoTracking().Where(x => x.CustomerId == customerId).OrderByDescending(x => x.FromUtc).ToListAsync(ct));

    [HttpGet("credits/preview/{customerId:guid}/{incidentId:guid}")]
    public async Task<IActionResult> CreditPreview(Guid customerId, Guid incidentId, CancellationToken ct)
    {
        var incident = await _db.Incidents.AsNoTracking().FirstOrDefaultAsync(x => x.Id == incidentId, ct);
        if (incident is null || incident.ResolvedAtUtc is null) return BadRequest("El incidente debe existir y estar resuelto.");
        var service = await _db.CustomerServices.AsNoTracking().Where(x => x.CustomerId == customerId && x.Status != ServiceStatus.Cancelled).OrderByDescending(x => x.ActivatedAtUtc).FirstOrDefaultAsync(ct);
        if (service is null) return NotFound("El cliente no tiene servicio activo.");
        var plan = await _db.ServicePlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == service.PlanId, ct);
        if (plan is null) return NotFound("El plan del servicio no existe.");
        var isAffectedCustomer = await _db.SupportInteractions.AsNoTracking()
            .Join(_db.SupportTickets.AsNoTracking(), interaction => interaction.TicketId, ticket => ticket.Id,
                (interaction, ticket) => new { interaction.RootIncidentId, ticket.CustomerId })
            .AnyAsync(x => x.RootIncidentId == incidentId && x.CustomerId == customerId, ct);
        if (!isAffectedCustomer)
            return BadRequest("No existe evidencia persistida de que el incidente afecte a este cliente.");
        var to = incident.ResolvedAtUtc.Value;
        var minutes = Math.Max(0, (to - incident.CreatedAtUtc).TotalMinutes);
        var amount = ServiceCreditCalculator.Calculate(plan.MonthlyPrice, incident.CreatedAtUtc, to);
        return Ok(new { CustomerId = customerId, IncidentId = incidentId, FromUtc = incident.CreatedAtUtc, ToUtc = to, DowntimeMinutes = (int)Math.Round(minutes), SuggestedAmount = amount, Evidence = "Incidente persistido y resuelto; cálculo proporcional sobre tarifa mensual del plan." });
    }

    [HttpGet("customers/{customerId:guid}/diagnostic")]
    public async Task<IActionResult> Diagnostic(Guid customerId, [FromServices] INetworkDiagnosticService diagnostics, CancellationToken ct) => Ok(await diagnostics.DiagnoseCustomerAsync(customerId, ct));

    [HttpGet("devices/{deviceId:guid}/impact")]
    public async Task<IActionResult> Impact(Guid deviceId, [FromServices] INetworkImpactService impact, CancellationToken ct) => Ok(await impact.PreviewAsync(deviceId, ct));

    [HttpPost("credits")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> CreateCredit([FromBody] CreditRequest request, CancellationToken ct)
    {
        if (!await _db.Customers.AnyAsync(x => x.Id == request.CustomerId, ct))
            return NotFound("El cliente no existe.");

        var incident = await _db.Incidents.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.IncidentId, ct);
        if (incident is null || incident.ResolvedAtUtc is null)
            return BadRequest("El incidente debe existir y estar resuelto.");

        var affectsCustomer = await _db.SupportInteractions.AsNoTracking()
            .Join(_db.SupportTickets.AsNoTracking(), interaction => interaction.TicketId,
                ticket => ticket.Id, (interaction, ticket) => new { interaction.RootIncidentId, ticket.CustomerId })
            .AnyAsync(x => x.RootIncidentId == request.IncidentId && x.CustomerId == request.CustomerId, ct);
        if (!affectsCustomer)
            return BadRequest("No existe evidencia persistida de que el incidente afecte a este cliente.");

        var item = new ServiceCredit(request.CustomerId, request.IncidentId, request.FromUtc,
            request.ToUtc, request.SuggestedAmount, request.Reason);
        _db.ServiceCredits.Add(item);
        await _db.SaveChangesAsync(ct);
        return Ok(item);
    }

    [HttpPost("credits/{id:guid}/status")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> CreditStatus(Guid id, [FromBody] CreditStatusRequest request, CancellationToken ct) { var item = await _db.ServiceCredits.FindAsync(new object[] { id }, ct); if (item is null) return NotFound(); item.SetStatus(request.Status); await _db.SaveChangesAsync(ct); return Ok(item); }

    [HttpPost("installations")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> CreateInstallation([FromBody] InstallationRequest request, CancellationToken ct) { if (!await _db.CustomerServices.AnyAsync(x => x.Id == request.ServiceId, ct)) return BadRequest("Servicio inválido."); var order = new InstallationOrder(request.ServiceId, request.OutsideCity, request.InstallationFee, request.RouterFee); if (request.ScheduledAtUtc.HasValue) order.Schedule(request.ScheduledAtUtc.Value); _db.InstallationOrders.Add(order); await _db.SaveChangesAsync(ct); return Ok(order); }

    [HttpPost("cpe-cases")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> CreateCpeCase([FromBody] CpeCaseRequest request, CancellationToken ct) { var mac = request.MacAddress ?? string.Empty; if (!Regex.IsMatch(mac, "^(?:[0-9A-Fa-f]{2}[:-]){5}[0-9A-Fa-f]{2}$")) return BadRequest("MAC inválida."); if (request.IpAddress is not null && (!IPAddress.TryParse(request.IpAddress, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)) return BadRequest("IP observada inválida."); if (await _db.CpeAuthorizationCases.AnyAsync(x => x.MacAddress == mac.ToUpperInvariant().Replace('-', ':') && x.Status == CpeAuthorizationStatus.Pending, ct)) return Conflict("Ya existe un caso pendiente para esa MAC."); var item = new CpeAuthorizationCase(mac, request.AccessPoint, request.IpAddress, request.Rssi, request.Snr, request.CustomerServiceId); _db.CpeAuthorizationCases.Add(item); await _db.SaveChangesAsync(ct); return Ok(item); }

    [HttpPost("cpe-cases/{id:guid}/decision")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> DecideCpeCase(Guid id, [FromBody] CpeDecisionRequest request, CancellationToken ct) { var item = await _db.CpeAuthorizationCases.FindAsync(new object[] { id }, ct); if (item is null) return NotFound(); if (request.Status == CpeAuthorizationStatus.Authorized && item.CustomerServiceId is null) return BadRequest("Una CPE sólo puede autorizarse vinculada a un servicio."); item.Decide(request.Status, request.Reason, User.Identity?.Name ?? "unknown"); item.SetProvisioning(ProvisioningStatus.Unsupported, request.Status == CpeAuthorizationStatus.Authorized ? "Autorización persistida; provisioning de red no ejecutado." : "Rechazo persistido; enforcement de red no ejecutado."); await _db.SaveChangesAsync(ct); return Ok(item); }

    [HttpPost("billing/{customerId:guid}/promises")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> Promise(Guid customerId, [FromBody] PromiseRequest request, CancellationToken ct) { if (!await _db.Customers.AnyAsync(x => x.Id == customerId, ct)) return NotFound(); var promise = new PaymentPromise(customerId, request.Amount, request.PromisedAtUtc, request.ExpiresAtUtc, User.Identity?.Name ?? "unknown", request.Conditions); _db.PaymentPromises.Add(promise); await _db.SaveChangesAsync(ct); return Ok(promise); }

    [HttpPost("billing/promises/{id:guid}/default")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> DefaultPromise(Guid id, CancellationToken ct) { var promise = await _db.PaymentPromises.FindAsync(new object[] { id }, ct); if (promise is null) return NotFound(); promise.Default(); await _db.SaveChangesAsync(ct); return Ok(promise); }

    [HttpPost("billing/promises/reconcile")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> ReconcilePromises(CancellationToken ct) { var expired = await _db.PaymentPromises.Where(x => x.Status == PromiseStatus.Active && x.ExpiresAtUtc < DateTime.UtcNow).ToListAsync(ct); foreach (var promise in expired) promise.Default(); await _db.SaveChangesAsync(ct); return Ok(new { Defaulted = expired.Count }); }

    [HttpPost("visits/route")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> Route([FromBody] RouteRequest request, CancellationToken ct) => Ok(await _routes.PlanAsync(request.VisitIds, ct));

    [HttpGet("devices/{deviceId:guid}/configuration-revisions")]
    public async Task<IActionResult> ConfigurationRevisions(Guid deviceId, CancellationToken ct) => Ok(await _db.ConfigurationRevisions.AsNoTracking().Where(x => x.DeviceId == deviceId).OrderByDescending(x => x.Revision).ToListAsync(ct));

    [HttpPost("devices/{deviceId:guid}/configuration-revisions")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> AddConfigurationRevision(Guid deviceId, [FromBody] ConfigurationRevisionRequest request, CancellationToken ct)
    { if (!await _db.Devices.AnyAsync(x => x.Id.Value == deviceId, ct)) return NotFound(); var revision = (await _db.ConfigurationRevisions.Where(x => x.DeviceId == deviceId).Select(x => (int?)x.Revision).MaxAsync(ct) ?? 0) + 1; var entity = new ConfigurationRevision(deviceId, revision, request.BeforeHash, request.AfterHash, request.Source, request.Reason, User.Identity?.Name ?? "unknown"); _db.ConfigurationRevisions.Add(entity); await _db.SaveChangesAsync(ct); return Ok(entity); }

    [HttpPost("configuration-revisions/{id:guid}/known-good")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> MarkKnownGood(Guid id, CancellationToken ct) { var revision = await _db.ConfigurationRevisions.FirstOrDefaultAsync(x => x.Id == id, ct); if (revision is null) return NotFound(); revision.MarkKnownGood(); await _db.SaveChangesAsync(ct); return Ok(revision); }

    [HttpPost("customers")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> CreateCustomer([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var code = request.ServiceCode?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Código y nombre son obligatorios.");
        if (await _db.Customers.AnyAsync(x => x.ServiceCode.ToUpper() == code, ct)) return Conflict("ServiceCode ya existe.");
        var customer = new Customer(code, request.Name, request.Phone, request.Email);
        // Persistir el Customer primero y luego el BillingAccount (FK_BillingAccounts_Customers),
        // en una transacción: evita violar la FK al insertar la cuenta antes de que exista la fila cliente.
        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            _db.Customers.Add(customer);
            await _db.SaveChangesAsync(ct);
            _db.BillingAccounts.Add(new BillingAccount(customer.Id));
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return Created($"/api/operations/customers/{customer.Id}", customer);
    }

    [HttpPost("plans")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> CreatePlan([FromBody] CreatePlanRequest request, CancellationToken ct)
    { var plan = new ServicePlan(request.Name, request.MonthlyPrice, request.DownloadMbps, request.UploadMbps); _db.ServicePlans.Add(plan); await _db.SaveChangesAsync(ct); return Ok(plan); }

    [HttpPost("services")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> CreateService([FromBody] CreateServiceRequest request, CancellationToken ct)
    { if (!await _db.Customers.AnyAsync(x => x.Id == request.CustomerId, ct) || !await _db.ServicePlans.AnyAsync(x => x.Id == request.PlanId && x.IsActive, ct)) return BadRequest("Cliente o plan inválido."); if (request.ZoneId is not null && !await _db.NetworkZones.AnyAsync(x => x.Id == request.ZoneId, ct)) return BadRequest("Zona inválida."); var service = new CustomerService(request.CustomerId, request.PlanId, request.Address, request.ZoneId); _db.CustomerServices.Add(service); await _db.SaveChangesAsync(ct); return Ok(service); }

    [HttpPost("services/{id:guid}/activate")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        var service = await _db.CustomerServices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (service is null) return NotFound();
        if (service.Status != ServiceStatus.Active && service.ZoneId is Guid zoneId)
        {
            var zone = await _db.NetworkZones.FirstOrDefaultAsync(x => x.Id == zoneId, ct);
            var plan = await _db.ServicePlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == service.PlanId, ct);
            if (zone is null || plan is null) return BadRequest("Zona o plan inválido.");
            if (zone.Status is ZoneStatus.Retired or ZoneStatus.Saturated)
                return Conflict("La zona no admite nuevas activaciones.");
            zone.Reserve(plan.DownloadMbps);
        }
        if (service.Status != ServiceStatus.Active)
            service.Activate();
        await _db.SaveChangesAsync(ct);
        return Ok(service);
    }
    [HttpPost("services/{id:guid}/suspend")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct) => await ChangeService(id, s => s.Suspend(), ct);
    [HttpPost("services/{id:guid}/reconnect")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> Reconnect(Guid id, CancellationToken ct) => await ChangeService(id, s => s.Reconnect(), ct);

    [HttpPost("services/{id:guid}/change-plan")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> ChangePlan(Guid id, [FromBody] ChangePlanRequest request, CancellationToken ct)
    {
        if (!request.Confirmed) return BadRequest("Se requiere confirmación explícita del impacto.");
        var service = await _db.CustomerServices.FirstOrDefaultAsync(x => x.Id == id, ct);
        var plan = await _db.ServicePlans.FirstOrDefaultAsync(x => x.Id == request.PlanId && x.IsActive, ct);
        if (service is null || plan is null) return BadRequest("Servicio o plan inválido.");
        var previous = await _db.ServicePlans.FindAsync(new object[] { service.PlanId }, ct);
        NetworkZone? zone = null;
        if (service.Status == ServiceStatus.Active && service.ZoneId is Guid zoneId)
        {
            zone = await _db.NetworkZones.FirstOrDefaultAsync(x => x.Id == zoneId, ct);
            if (zone is null) return BadRequest("La zona del servicio no existe.");
            if (zone.Status is ZoneStatus.Retired or ZoneStatus.Saturated)
                return Conflict("La zona no admite cambios de plan.");
            if (previous is not null) zone.ReleaseReservation(previous.DownloadMbps);
            try
            {
                zone.Reserve(plan.DownloadMbps);
            }
            catch (InvalidOperationException)
            {
                if (previous is not null) zone.Reserve(previous.DownloadMbps);
                return Conflict("La zona no tiene capacidad para el nuevo plan.");
            }
        }
        service.ChangePlan(plan.Id);
        await _db.SaveChangesAsync(ct);
        return Ok(new { ServiceId = service.Id, PreviousPlanId = previous?.Id, NewPlanId = plan.Id, MonthlyDifference = plan.MonthlyPrice - (previous?.MonthlyPrice ?? 0), Capacity = new { plan.DownloadMbps, plan.UploadMbps } });
    }

    [HttpPost("services/{id:guid}/cancel")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> CancelService(Guid id, CancellationToken ct)
    {
        var service = await _db.CustomerServices.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (service is null) return NotFound();
        if (service.ZoneId is Guid zoneId && service.Status == ServiceStatus.Active)
        {
            var zone = await _db.NetworkZones.FirstOrDefaultAsync(x => x.Id == zoneId, ct);
            var plan = await _db.ServicePlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == service.PlanId, ct);
            if (zone is null || plan is null) return BadRequest("Zona o plan inválido.");
            zone.ReleaseReservation(plan.DownloadMbps);
        }
        service.Cancel();
        await _db.SaveChangesAsync(ct);
        return Ok(service);
    }

    [HttpPost("assets/{id:guid}/recover")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> RecoverAsset(Guid id, CancellationToken ct) => await ChangeAsset(id, x => x.Recover(), ct);

    [HttpPost("assets/{id:guid}/inspect")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> InspectAsset(Guid id, [FromBody] AssetInspectionRequest request, CancellationToken ct) => await ChangeAsset(id, x => x.MarkInspected(request.Passed), ct);

    [HttpPost("billing/{customerId:guid}/charge")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public Task<IActionResult> Charge(Guid customerId, [FromBody] BillingRequest request, CancellationToken ct) => AddLedger(customerId, LedgerEntryType.Charge, request, ct);
    [HttpPost("billing/{customerId:guid}/payment")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public Task<IActionResult> Payment(Guid customerId, [FromBody] BillingRequest request, CancellationToken ct) => AddLedger(customerId, LedgerEntryType.Payment, request, ct);

    [HttpPost("billing/{customerId:guid}/suspend-if-overdue")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> SuspendIfOverdue(Guid customerId, CancellationToken ct)
    {
        var account = await _db.BillingAccounts.AsNoTracking().FirstOrDefaultAsync(x => x.CustomerId == customerId, ct);
        if (account is null) return NotFound();
        var overdue = await _db.BillingEntries.AsNoTracking()
            .Where(x => x.AccountId == account.Id && x.Type == LedgerEntryType.Charge && x.DueAtUtc != null)
            .AnyAsync(x => x.DueAtUtc!.Value.AddDays(3) < DateTime.UtcNow, ct);
        if (account.Balance <= 0 || !overdue) return Ok(new { Suspended = 0, account.Balance, Reason = "Saldo no vencido o dentro de gracia de 3 días." });
        var services = await _db.CustomerServices.Where(x => x.CustomerId == customerId && x.Status == ServiceStatus.Active).ToListAsync(ct);
        foreach (var service in services) service.Suspend();
        await _db.SaveChangesAsync(ct);
        return Ok(new { Suspended = services.Count, account.Balance, Reason = "Saldo vencido persistido." });
    }

    [HttpPost("billing/{customerId:guid}/monthly-charge")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> MonthlyCharge(Guid customerId, CancellationToken ct)
    {
        var service = await _db.CustomerServices.Where(x => x.CustomerId == customerId && x.Status != ServiceStatus.Cancelled).OrderByDescending(x => x.ActivatedAtUtc).FirstOrDefaultAsync(ct);
        if (service is null) return BadRequest("El cliente no tiene servicio activo.");
        var plan = await _db.ServicePlans.FindAsync(new object[] { service.PlanId }, ct);
        return plan is null ? BadRequest("El plan no existe.") : await AddLedger(customerId, LedgerEntryType.Charge, new BillingRequest(plan.MonthlyPrice, $"Mensualidad {plan.Name}"), ct);
    }

    [HttpGet("services/{id:guid}/prepayment-quote")]
    public async Task<IActionResult> PrepaymentQuote(Guid id, [FromQuery] int months, CancellationToken ct)
    {
        var service = await _db.CustomerServices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (service is null) return NotFound();
        var plan = await _db.ServicePlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == service.PlanId, ct);
        if (plan is null) return BadRequest("El plan no existe.");
        return Ok(new PrepaymentPolicy().Quote(months, plan.MonthlyPrice));
    }

    [HttpPost("services/{id:guid}/prepayment")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> RegisterPrepayment(Guid id, [FromBody] PrepaymentRequest request, CancellationToken ct)
    {
        if (!request.Confirmed) return BadRequest("Se requiere confirmación explícita del prepago.");
        var service = await _db.CustomerServices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        var plan = service is null ? null : await _db.ServicePlans.AsNoTracking().FirstOrDefaultAsync(x => x.Id == service.PlanId, ct);
        if (service is null || plan is null) return NotFound();
        var quote = new PrepaymentPolicy().Quote(request.Months, plan.MonthlyPrice);
        return await AddLedger(service.CustomerId, LedgerEntryType.Payment, new BillingRequest(quote.Total, $"Prepago {quote.Months} meses; bonificación {quote.BonusDays} días"), ct);
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
    [HttpPost("tickets/{id:guid}/status")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> TicketStatus(Guid id, [FromBody] TicketStatusRequest request, CancellationToken ct) { var ticket = await _db.SupportTickets.FindAsync(new object[] { id }, ct); if (ticket is null) return NotFound(); ticket.SetStatus(request.Status); await _db.SaveChangesAsync(ct); return Ok(ticket); }
    [HttpPost("assets")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> Asset([FromBody] AssetRequest request, CancellationToken ct) { if (string.IsNullOrWhiteSpace(request.AssetTag) || string.IsNullOrWhiteSpace(request.Type)) return BadRequest("AssetTag y tipo son obligatorios."); if (await _db.InventoryAssets.AnyAsync(x => x.AssetTag == request.AssetTag || (request.SerialNumber != null && x.SerialNumber == request.SerialNumber) || (request.MacAddress != null && x.MacAddress == request.MacAddress), ct)) return Conflict("AssetTag, serial o MAC ya registrados."); var asset = new InventoryAsset(request.AssetTag, request.Type, request.SerialNumber, request.MacAddress); _db.InventoryAssets.Add(asset); await _db.SaveChangesAsync(ct); return Ok(asset); }
    [HttpPost("assets/{id:guid}/assign")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> AssignAsset(Guid id, [FromBody] AssetAssignmentRequest request, CancellationToken ct)
    {
        var asset = await _db.InventoryAssets.FindAsync(new object[] { id }, ct);
        if (asset is null) return NotFound();
        if (!await _db.CustomerServices.AnyAsync(x => x.Id == request.ServiceId && x.Status != ServiceStatus.Cancelled, ct)) return BadRequest("Servicio inválido o cancelado.");
        if (asset.CustomerServiceId.HasValue && asset.CustomerServiceId != request.ServiceId) return Conflict("El equipo ya está asignado a otro servicio; primero debe recuperarse.");
        asset.Assign(request.ServiceId);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("El equipo fue asignado concurrentemente; primero debe recuperarse.");
        }
        return Ok(asset);
    }
    [HttpPost("coverage")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> Coverage([FromBody] CoverageRequest request, CancellationToken ct) { var check = new CoverageCheck(request.Address, request.Status, request.CapacityMbps); _db.CoverageChecks.Add(check); await _db.SaveChangesAsync(ct); return Ok(check); }

    [HttpGet("zones")]
    public async Task<IActionResult> ListNetworkZones(CancellationToken ct) => Ok(await _db.NetworkZones.AsNoTracking().OrderBy(x => x.Code).ToListAsync(ct));

    [HttpPost("zones")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> CreateNetworkZone([FromBody] ZoneRequest request, CancellationToken ct)
    {
        if (await _db.NetworkZones.AnyAsync(x => x.Code == request.Code.Trim().ToUpper(), ct)) return Conflict("El código de zona ya existe.");
        var zone = new NetworkZone(request.Code, request.Name, request.Geography, request.TotalCapacityMbps);
        _db.NetworkZones.Add(zone); await _db.SaveChangesAsync(ct); return Ok(zone);
    }

    [HttpPost("zones/{id:guid}/status")]
    [Authorize(Roles = "Administrator,NocOperator")]
    public async Task<IActionResult> ChangeNetworkZoneStatus(Guid id, [FromBody] ZoneStatusRequest request, CancellationToken ct)
    {
        var zone = await _db.NetworkZones.FindAsync(new object[] { id }, ct); if (zone is null) return NotFound();
        zone.SetStatus(request.Status); await _db.SaveChangesAsync(ct); return Ok(zone);
    }
    [HttpPost("visits")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> Visit([FromBody] VisitRequest request, CancellationToken ct) { if (!await _db.Customers.AnyAsync(x => x.Id == request.CustomerId, ct)) return BadRequest("Cliente inválido."); var visit = new TechnicianVisit(request.CustomerId, request.ScheduledAtUtc, request.WorkType, request.EstimatedMinutes); _db.TechnicianVisits.Add(visit); await _db.SaveChangesAsync(ct); return Ok(visit); }

    [HttpPost("visits/{id:guid}/complete")]
    [Authorize(Roles = "Administrator,NocOperator,Support")]
    public async Task<IActionResult> CompleteVisit(Guid id, [FromBody] VisitCompletionRequest request, CancellationToken ct) { var visit = await _db.TechnicianVisits.FindAsync(new object[] { id }, ct); if (visit is null) return NotFound(); visit.Complete(request.ActualMinutes, request.TravelMinutes, request.Result); await _db.SaveChangesAsync(ct); return Ok(visit); }

    private async Task<IActionResult> ChangeService(Guid id, Action<CustomerService> change, CancellationToken ct) { var service = await _db.CustomerServices.FirstOrDefaultAsync(x => x.Id == id, ct); if (service is null) return NotFound(); change(service); service.SetProvisioning(ProvisioningStatus.Unsupported, "Business record updated, network provisioning NOT EXECUTED."); await _db.SaveChangesAsync(ct); return Ok(service); }
    private async Task<IActionResult> ChangeAsset(Guid id, Action<InventoryAsset> change, CancellationToken ct) { var asset = await _db.InventoryAssets.FirstOrDefaultAsync(x => x.Id == id, ct); if (asset is null) return NotFound(); change(asset); await _db.SaveChangesAsync(ct); return Ok(asset); }
    private async Task<IActionResult> AddLedger(Guid customerId, LedgerEntryType type, BillingRequest request, CancellationToken ct)
        => await _db.Database.CreateExecutionStrategy().ExecuteAsync(
            () => AddLedgerCore(customerId, type, request, ct));

    private async Task<IActionResult> AddLedgerCore(Guid customerId, LedgerEntryType type, BillingRequest request, CancellationToken ct)
    {
        var account = await _db.BillingAccounts.FirstOrDefaultAsync(x => x.CustomerId == customerId, ct);
        if (account is null || request.Amount <= 0) return BadRequest("Cuenta o monto inválido.");
        var key = request.IdempotencyKey?.Trim();
        await using var tx = await _db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);
        if (!string.IsNullOrWhiteSpace(key))
        {
            var existing = await _db.BillingEntries.AsNoTracking().FirstOrDefaultAsync(
                x => x.AccountId == account.Id && x.Type == type && x.IdempotencyKey == key, ct);
            if (existing is not null)
            {
                await tx.CommitAsync(ct);
                return Ok(new { account.Id, account.Balance, IdempotentReplay = true,
                    Entry = existing, Receipt = (PaymentReceipt?)null });
            }
        }
        account.Apply(type, request.Amount);
        _db.BillingEntries.Add(new BillingEntry(account.Id, type, request.Amount, request.Description, request.DueAtUtc, request.Period, key));
        PaymentReceipt? receipt = null;
        if (type == LedgerEntryType.Payment)
        {
            var remaining = request.Amount;
            foreach (var promise in await _db.PaymentPromises.Where(x => x.CustomerId == customerId && x.Status == PromiseStatus.Active).OrderBy(x => x.ExpiresAtUtc).ToListAsync(ct))
            {
                var allocated = Math.Min(remaining, promise.Remaining);
                if (allocated > 0) { promise.ApplyPayment(allocated); remaining -= allocated; }
                if (remaining <= 0) break;
            }
            receipt = new PaymentReceipt(customerId, account.Id, request.Amount, $"PAY-{Guid.NewGuid():N}");
            _db.PaymentReceipts.Add(receipt);
        }
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Ok(new { account.Id, account.Balance, IdempotentReplay = false, Receipt = receipt });
        }
        catch (DbUpdateException ex) when (!string.IsNullOrWhiteSpace(key) && IsDuplicateKey(ex))
        {
            // Two requests with the same idempotency key can pass the initial
            // read concurrently. The unique index is the final arbiter; after
            // rolling back the losing unit, return the committed entry as a
            // replay instead of leaking a false 405/500 to the caller.
            await tx.RollbackAsync(CancellationToken.None);
            _db.ChangeTracker.Clear();
            var committedAccount = await _db.BillingAccounts.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CustomerId == customerId, ct);
            var committedEntry = committedAccount is null
                ? null
                : await _db.BillingEntries.AsNoTracking().FirstOrDefaultAsync(
                    x => x.AccountId == committedAccount.Id && x.Type == type && x.IdempotencyKey == key, ct);
            if (committedAccount is null || committedEntry is null)
                throw;

            return Ok(new
            {
                committedAccount.Id,
                committedAccount.Balance,
                IdempotentReplay = true,
                Entry = committedEntry,
                Receipt = (PaymentReceipt?)null
            });
        }
    }

    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is MySqlConnector.MySqlException mysql && mysql.Number == 1062)
                return true;

            if (current.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) ||
                current.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

public sealed record CreateCustomerRequest(string ServiceCode, string Name, string? Phone, string? Email);
public sealed record CreatePlanRequest(string Name, decimal MonthlyPrice, int DownloadMbps, int UploadMbps);
public sealed record CreateServiceRequest(Guid CustomerId, Guid PlanId, string Address, Guid? ZoneId = null);
public sealed record BillingRequest(decimal Amount, string Description, DateTime? DueAtUtc = null, string? Period = null, string? IdempotencyKey = null);
public sealed record TicketRequest(Guid CustomerId, string Title, string? Description);
public sealed record AssetRequest(string AssetTag, string Type, string? SerialNumber, string? MacAddress);
public sealed record CoverageRequest(string Address, CoverageStatus Status, int? CapacityMbps);
public sealed record ZoneRequest(string Code, string Name, string? Geography, int TotalCapacityMbps);
public sealed record ZoneStatusRequest(ZoneStatus Status);
public sealed record VisitRequest(Guid CustomerId, DateTime ScheduledAtUtc, string WorkType, int EstimatedMinutes);
public sealed record RouteRequest(IReadOnlyList<Guid> VisitIds);
public sealed record ConfigurationRevisionRequest(string BeforeHash, string AfterHash, string Source, string Reason);
public sealed record ProspectRequest(string Name, string Address, string? Phone);
public sealed record ProspectStatusRequest(ProspectStatus Status);
public sealed record PromiseRequest(decimal Amount, DateTime PromisedAtUtc, DateTime ExpiresAtUtc, string Conditions);
public sealed record CpeCaseRequest(string MacAddress, string? AccessPoint, string? IpAddress, double? Rssi, double? Snr, Guid? CustomerServiceId);
public sealed record CpeDecisionRequest(CpeAuthorizationStatus Status, string Reason);
public sealed record ContractRequest(Guid CustomerId, Guid ServiceId, string Terms);
public sealed record InstallationRequest(Guid ServiceId, bool OutsideCity, decimal InstallationFee, decimal RouterFee, DateTime? ScheduledAtUtc);
public sealed record InteractionRequest(Guid TicketId, SupportInteractionChannel Channel, string Symptoms, string Diagnosis, string Actions, string Result, int DurationMinutes, Guid? RootIncidentId);
public sealed record CreditRequest(Guid CustomerId, Guid? IncidentId, DateTime FromUtc, DateTime ToUtc, decimal SuggestedAmount, string Reason);
public sealed record CreditStatusRequest(ServiceCreditStatus Status);
public sealed record RootIncidentRequest(string Title, string? Description, string? RootCauseDeviceId);
public sealed record IncidentPriorityRequest(IncidentPriority Priority, string Reason);
public sealed record ChangePlanRequest(Guid PlanId, bool Confirmed);
public sealed record AssetInspectionRequest(bool Passed);
public sealed record VisitCompletionRequest(int ActualMinutes, int TravelMinutes, string Result);
public sealed record TicketStatusRequest(TicketStatus Status);
public sealed record PrepaymentRequest(int Months, bool Confirmed);
public sealed record AssetAssignmentRequest(Guid ServiceId);
