using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Dashboard;

public class WithdrawalViewModel
{
    public InfluencerEarningsViewModel Header { get; set; } = new();
    public decimal AvailableBalance { get; set; }
    public decimal MinimumWithdrawal { get; set; } = 500m;
    public decimal FeePercent { get; set; } = 0m;
    public List<PayoutMethod> PayoutMethods { get; set; } = [];
    public List<WithdrawalRequest> History { get; set; } = [];
    public string? Message { get; set; }
    public string? Error { get; set; }
}
