namespace Brandora.Web.Areas.Admin.Services;

public record PayoutResult(bool Success, string? Reference, string? Error);

// PLACEHOLDER — the admin cash-out is sent through the payment gateway that is being built
// separately. Until it is merged, no money can move, so this always reports "not connected"
// (the page shows that as a failed cash-out with a clear message). When the gateway exists,
// replace the body of SendAsync with the real call and return Success with its reference; the
// cash-out page, the confirmation states and the admin email already react to that result.
//
// For testing the success flow and the email in Development only, set PAYOUT_GATEWAY=simulate
// in .env. That returns a fake reference and moves no money.
public static class PlatformPayoutGateway
{
    public const decimal GatewayCharge = 0m;

    public static Task<PayoutResult> SendAsync(decimal amount, string method, string account, bool simulate)
    {
        if (simulate)
        {
            var reference = "SIM" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
            return Task.FromResult(new PayoutResult(true, reference, null));
        }

        return Task.FromResult(new PayoutResult(
            false,
            null,
            "The payment gateway is not connected yet, so no money was moved."));
    }
}
