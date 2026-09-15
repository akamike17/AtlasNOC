using AtlasNOC.Domain.Entities;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public sealed class ServiceCreditCalculatorTests
{
    [Fact]
    public void Calculates_proportional_credit_from_persisted_outage_period()
    {
        var from = DateTime.UtcNow.AddHours(-12);
        var amount = ServiceCreditCalculator.Calculate(300m, from, from.AddHours(12));
        Assert.Equal(5m, amount);
    }

    [Fact]
    public void Credit_is_capped_at_monthly_price()
    {
        var from = DateTime.UtcNow.AddDays(-60);
        Assert.Equal(300m, ServiceCreditCalculator.Calculate(300m, from, DateTime.UtcNow));
    }
}
