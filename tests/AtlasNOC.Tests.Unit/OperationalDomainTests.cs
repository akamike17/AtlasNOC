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
}
