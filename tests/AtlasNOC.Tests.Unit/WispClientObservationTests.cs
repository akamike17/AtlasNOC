using AtlasNOC.Domain.Entities;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public sealed class WispClientObservationTests
{
    [Fact]
    public void Normalizes_evidence_without_changing_identity_semantics()
    {
        var observed = new WispClientObservation(
            "  client-1 ", " account-1 ", " cpe-1 ", " session-1 ",
            DateTime.UtcNow, " radius ", .75);

        Assert.Equal("client-1", observed.ExternalId);
        Assert.Equal("account-1", observed.AccountReference);
        Assert.Equal("cpe-1", observed.CpeAddress);
        Assert.Equal("session-1", observed.SessionReference);
        Assert.Equal("radius", observed.Source);
        Assert.Equal(.75, observed.Confidence);
    }

    [Theory]
    [InlineData(1.1)]
    [InlineData(-0.1)]
    public void Rejects_confidence_outside_unit_interval(double confidence)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new WispClientObservation(
            "client-1", null, null, null, DateTime.UtcNow, "test", confidence));

    [Fact]
    public void Rejects_missing_identity_or_source()
    {
        Assert.Throws<ArgumentException>(() => new WispClientObservation(
            "", null, null, null, DateTime.UtcNow, "test", .5));
        Assert.Throws<ArgumentException>(() => new WispClientObservation(
            "client-1", null, null, null, DateTime.UtcNow, "", .5));
    }

    [Fact]
    public void Rejects_unbounded_external_values()
    {
        Assert.Throws<ArgumentException>(() => new WispClientObservation(
            new string('x', 201), null, null, null, DateTime.UtcNow, "test", .5));
        Assert.Throws<ArgumentException>(() => new WispClientObservation(
            "client-1", null, null, new string('x', 201), DateTime.UtcNow, "test", .5));
    }
}
