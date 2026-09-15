using AtlasNOC.Domain.Entities;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public sealed class OperationalDomainTests
{
    [Fact]
    public void Billing_account_tracks_charges_payments_and_credits()
    {
        var account = new BillingAccount(Guid.NewGuid());
        account.Apply(LedgerEntryType.Charge, 500);
        account.Apply(LedgerEntryType.Payment, 200);
        account.Apply(LedgerEntryType.Credit, 50);
        Assert.Equal(250, account.Balance);
    }

    [Fact]
    public void Billing_entry_preserves_idempotency_key_for_retries()
    {
        var key = "monthly:customer-1:2026-09";
        var entry = new BillingEntry(Guid.NewGuid(), LedgerEntryType.Charge, 250, "Mensualidad", period: "2026-09", idempotencyKey: key);
        Assert.Equal(key, entry.IdempotencyKey);
    }

    [Fact]
    public void Service_lifecycle_is_explicit_and_reconnectable()
    {
        var service = new CustomerService(Guid.NewGuid(), Guid.NewGuid(), "Calle 1");
        service.Activate();
        service.Suspend();
        service.Reconnect();
        Assert.Equal(ServiceStatus.Active, service.Status);
        Assert.NotNull(service.ActivatedAtUtc);
    }

    [Fact]
    public void Asset_assignment_changes_ownership_state()
    {
        var asset = new InventoryAsset("AT-1", "CPE");
        asset.Assign(Guid.NewGuid());
        Assert.Equal(AssetStatus.Assigned, asset.Status);
        Assert.NotNull(asset.CustomerServiceId);
    }

    [Fact]
    public void Prospect_is_not_a_customer_until_explicitly_converted()
    {
        var prospect = new Prospect("Ana", "Calle 2");
        prospect.SetStatus(ProspectStatus.CoverageChecked);
        Assert.Equal(ProspectStatus.CoverageChecked, prospect.Status);
    }

    [Fact]
    public void Payment_promise_tracks_remaining_and_fulfillment()
    {
        var promise = new PaymentPromise(Guid.NewGuid(), 300, DateTime.UtcNow, DateTime.UtcNow.AddDays(7), "op", "una cuota");
        promise.ApplyPayment(100);
        Assert.Equal(200, promise.Remaining);
        promise.ApplyPayment(200);
        Assert.Equal(PromiseStatus.Fulfilled, promise.Status);
    }

    [Fact]
    public void Cpe_case_requires_explicit_decision()
    {
        var cpe = new CpeAuthorizationCase("aa:bb:cc:dd:ee:ff", "AP-1", "192.0.2.10", -55, 28);
        Assert.Equal(CpeAuthorizationStatus.Pending, cpe.Status);
        cpe.Decide(CpeAuthorizationStatus.Authorized, "reemplazo validado", "admin");
        Assert.Equal(CpeAuthorizationStatus.Authorized, cpe.Status);
    }

    [Fact]
    public void Contract_and_installation_follow_operational_lifecycle()
    {
        var customer = Guid.NewGuid();
        var service = Guid.NewGuid();
        var contract = new ServiceContract(customer, service, "mensualidad y devolución de equipo");
        contract.Accept("admin");
        var installation = new InstallationOrder(service, true, 350, 350);
        installation.Schedule(DateTime.UtcNow.AddDays(1));
        installation.Complete();
        Assert.Equal(ContractStatus.Accepted, contract.Status);
        Assert.Equal(InstallationStatus.Completed, installation.Status);
    }

    [Fact]
    public void Credit_requires_real_interval_and_can_be_approved()
    {
        var credit = new ServiceCredit(Guid.NewGuid(), Guid.NewGuid(), DateTime.UtcNow.AddHours(-2), DateTime.UtcNow, 25, "incidente raíz confirmado");
        credit.SetStatus(ServiceCreditStatus.Approved);
        Assert.Equal(ServiceCreditStatus.Approved, credit.Status);
    }

    [Fact]
    public void Prepayment_policy_is_configurable_and_quotes_bonus()
    {
        var policy = new PrepaymentPolicy(new Dictionary<int, int> { [3] = 7 });
        var quote = policy.Quote(3, 250);
        Assert.Equal(7, quote.BonusDays);
        Assert.Equal(750, quote.Total);
    }

    [Fact]
    public void Sla_policy_returns_internal_targets_by_priority()
    {
        var target = new SlaPolicy().For(IncidentPriority.P1Critical);
        Assert.Equal(15, target.ResponseMinutes);
        Assert.Equal(4, target.ResolutionTargetHours);
    }

    [Fact]
    public void Cancelled_service_cannot_be_reconnected()
    {
        var service = new CustomerService(Guid.NewGuid(), Guid.NewGuid(), "Calle 3");
        service.Cancel();
        Assert.Throws<InvalidOperationException>(() => service.Reconnect());
    }
}
