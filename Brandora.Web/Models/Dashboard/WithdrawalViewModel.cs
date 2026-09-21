using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Dashboard;

public class WithdrawalViewModel
{
    public InfluencerEarningsViewModel Header { get; set; } = new();
    public decimal AvailableBalance { get; set; }
    public decimal MinimumWithdrawal { get; set; } = 500m;
    public decimal FeePercent { get; set; } = 0m;

    // Earnings/withdrawal totals the fee preview needs to mirror the server's cumulative
    // calculation (WalletService.QuoteWithdrawal).
    public Brandora.Web.Services.Payments.WithdrawalLedger Ledger { get; set; } = Brandora.Web.Services.Payments.WithdrawalLedger.Empty;
    public List<PayoutMethod> PayoutMethods { get; set; } = [];
    public List<WithdrawalRequest> History { get; set; } = [];
    public string? Message { get; set; }
    public string? Error { get; set; }
}
