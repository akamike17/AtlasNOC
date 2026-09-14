using AtlasNOC.Application.Devices;
using AtlasNOC.Application.Services;
using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Devices;
using Xunit;

namespace AtlasNOC.Tests.Unit;

/// <summary>Smoke independiente de los 30 checkpoints del cierre post-auditoría.</summary>
public sealed class ClosureSmokeTests
{
    [Fact(DisplayName = "Smoke 30/30: ciclo operativo simulado completo")]
    public async Task Smoke_30_of_30_simulated_checkpoints_pass()
    {
        var customer = new Customer("CLI-001", "Cliente Smoke"); Check(1, customer.Status == CustomerStatus.Active);
        var plan = new ServicePlan("Plan Smoke", 500, 100, 20); Check(2, plan.MonthlyPrice == 500);
        var service = new CustomerService(customer.Id, plan.Id, "Calle 1"); Check(3, service.Status == ServiceStatus.Pending);
        service.Activate(); Check(4, service.Status == ServiceStatus.Active);
        var account = new BillingAccount(customer.Id); account.Apply(LedgerEntryType.Charge, 500); Check(5, account.Balance == 500);
        var charge = new BillingEntry(account.Id, LedgerEntryType.Charge, 500, "Mensualidad", DateTime.UtcNow.AddDays(-4), "2026-09"); Check(6, charge.IsOverdue(DateTime.UtcNow, 3));
        account.Apply(LedgerEntryType.Payment, 250); Check(7, account.Balance == 250);
        var promise = new PaymentPromise(customer.Id, 250, DateTime.UtcNow, DateTime.UtcNow.AddDays(7), "operator", "Pago parcial"); promise.ApplyPayment(250); Check(8, promise.Status == PromiseStatus.Fulfilled);
        service.Suspend(); Check(9, service.Status == ServiceStatus.Suspended);
        service.SetProvisioning(ProvisioningStatus.Unsupported, "No se ejecuta hardware"); Check(10, service.Provisioning == ProvisioningStatus.Unsupported);
        service.Reconnect(); Check(11, service.Status == ServiceStatus.Active);
        var asset = new InventoryAsset("AST-001", "CPE", macAddress: "AA:BB:CC:DD:EE:FF"); asset.Assign(service.Id); Check(12, asset.Status == AssetStatus.Assigned);
        asset.Recover(); asset.MarkInspected(true); Check(13, asset.Status == AssetStatus.Tested);
        var cpe = new CpeAuthorizationCase("AA:BB:CC:DD:EE:11", "AP-1", "10.0.0.10", -55, 35, service.Id); Check(14, cpe.Status == CpeAuthorizationStatus.Pending);
        cpe.Decide(CpeAuthorizationStatus.Authorized, "Validada", "operator"); cpe.SetProvisioning(ProvisioningStatus.Unsupported, "SIMULATED"); Check(15, cpe.Provisioning == ProvisioningStatus.Unsupported);
        var contract = new ServiceContract(customer.Id, service.Id, "Términos"); contract.Accept("operator"); Check(16, contract.Status == ContractStatus.Accepted);
        var install = new InstallationOrder(service.Id, false, 0, 0); install.Schedule(DateTime.UtcNow.AddHours(1)); Check(17, install.Status == InstallationStatus.Scheduled);
        var ticket = new SupportTicket(customer.Id, "Sin servicio"); ticket.SetStatus(TicketStatus.InProgress); Check(18, ticket.Status == TicketStatus.InProgress);
        var interaction = new SupportInteraction(ticket.Id, SupportInteractionChannel.Phone, "síntoma", "diagnóstico", "acción", "operator", "resultado", 10); Check(19, interaction.DurationMinutes == 10);
        var visit = new TechnicianVisit(customer.Id, DateTime.UtcNow.AddHours(2), "Revisión", 60); visit.Complete(45, 20, "Resuelto"); Check(20, visit.ActualMinutes == 45);
        var incident = new Incident("AP caído", "operator", "evidencia"); incident.MarkRootCauseCandidate(); incident.Reclassify(IncidentPriority.P2High, "Impacto"); Check(21, incident.Priority == IncidentPriority.P2High);
        var credit = new ServiceCredit(customer.Id, incident.Id, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow, 50, "Duración respaldada"); credit.SetStatus(ServiceCreditStatus.Approved); Check(22, credit.Status == ServiceCreditStatus.Approved);
        var coverage = new CoverageCheck("Calle 1", CoverageStatus.ProbablyAvailable, 100); Check(23, coverage.Status == CoverageStatus.ProbablyAvailable);
        var simulated = new SimulatedDeviceControlDriver(); var credential = new ResolvedDeviceCredential(Guid.NewGuid(), "sim", SnmpVersion.V3, null, "u", null, "p", null, null);
        var disabled = await simulated.ExecuteAsync(new DeviceActionRequest("10.0.0.1", DeviceAction.DisableInterface, "ether1"), credential); Check(24, disabled.Succeeded);
        var description = await simulated.ExecuteAsync(new DeviceActionRequest("10.0.0.1", DeviceAction.SetInterfaceDescription, "ether1", "uplink"), credential); Check(25, description.Succeeded);
        Check(26, simulated.GetInterfaceState("10.0.0.1", "ether1")?.Enabled == false);
        Check(27, simulated.GetInterfaceState("10.0.0.1", "ether1")?.Description == "uplink");
        simulated.FailOnce(); Check(28, !(await simulated.ExecuteAsync(new DeviceActionRequest("10.0.0.1", DeviceAction.Reboot), credential)).Succeeded);
        simulated.SetUnsupported(DeviceAction.Reboot); Check(29, !(await simulated.ExecuteAsync(new DeviceActionRequest("10.0.0.1", DeviceAction.Reboot), credential)).Succeeded);
        service.SetProvisioning(ProvisioningStatus.Applied, "SIMULATED post-verification"); Check(30, service.Provisioning == ProvisioningStatus.Applied);
    }

    private static void Check(int checkpoint, bool passed) => Assert.True(passed, $"Smoke checkpoint {checkpoint}/30 falló.");
}
