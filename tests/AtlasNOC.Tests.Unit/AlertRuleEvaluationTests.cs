using AtlasNOC.Domain.Entities;
using AtlasNOC.Domain.Enums;
using AtlasNOC.Infrastructure.Services;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public class AlertRuleEvaluationTests
{
    [Fact]
    public void FailFailOk_DoesNotTriggerThreeConsecutiveFaults()
    {
        // Entrada newest-first: el OK es la muestra más reciente.
        Assert.False(AlertEvaluationEngine.HasConsecutiveFaults(
            new[] { 100d, 0d, 0d }, 3, "<", 50));
    }

    [Fact]
    public void FailFailFail_TriggersThreeConsecutiveFaults()
        => Assert.True(AlertEvaluationEngine.HasConsecutiveFaults(
            new[] { 0d, 10d, 20d }, 3, "<", 50));

    [Fact]
    public void InsufficientSamples_DoNotTrigger()
        => Assert.False(AlertEvaluationEngine.HasConsecutiveFaults(
            new[] { 0d, 0d }, 3, "<", 50));

    [Theory]
    [InlineData(">", 11, 10)]
    [InlineData(">=", 10, 10)]
    [InlineData("<", 9, 10)]
    [InlineData("<=", 10, 10)]
    [InlineData("==", 10, 10)]
    public void SupportedOperatorsAreEvaluatedStrictly(string op, double value, double threshold)
        => Assert.True(AlertEvaluationEngine.Compare(value, op, threshold));

    [Fact]
    public void InvalidOperator_IsRejectedInsteadOfFallingBack()
    {
        Assert.Throws<ArgumentException>(() =>
            new AlertRule("CPU", "cpu_usage", "!=", 90, AlertSeverity.High));
        Assert.Throws<ArgumentException>(() => AlertEvaluationEngine.Compare(100, "!=", 90));
    }

    [Fact]
    public void AcknowledgedAlert_CanStillResolve()
    {
        var alert = new Alert(Guid.NewGuid(), "Device", Guid.NewGuid().ToString(),
            "availability", 0, 50, AlertSeverity.High);
        alert.Acknowledge("operator");

        alert.Resolve("system");

        Assert.Equal(AlertState.Resolved, alert.State);
        Assert.Equal("operator", alert.AcknowledgedBy);
        Assert.Equal("system", alert.ResolvedBy);
    }
}
