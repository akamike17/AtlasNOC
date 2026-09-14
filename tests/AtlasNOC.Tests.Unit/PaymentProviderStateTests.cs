using AtlasNOC.Application.Services;
using AtlasNOC.Infrastructure.Services;
using Xunit;

namespace AtlasNOC.Tests.Unit;

public sealed class PaymentProviderStateTests
{
    [Fact]
    public async Task Manual_payment_is_pending_validation_not_banked_confirmation()
    {
        var result = await new ManualPaymentProvider().CaptureAsync(new PaymentRequest(Guid.NewGuid(), null, 100m, "MANUAL-1"));
        Assert.False(result.Accepted);
        Assert.Equal(PaymentCaptureState.PendingValidation, result.State);
    }

    [Fact]
    public async Task Manual_invalid_amount_is_rejected()
    {
        var result = await new ManualPaymentProvider().CaptureAsync(new PaymentRequest(Guid.NewGuid(), null, 0m, "MANUAL-2"));
        Assert.Equal(PaymentCaptureState.Rejected, result.State);
    }
}
