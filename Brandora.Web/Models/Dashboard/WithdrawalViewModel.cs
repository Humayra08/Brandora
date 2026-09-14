namespace Brandora.Web.Models.Dashboard;

public class WithdrawalViewModel
{
    public InfluencerEarningsViewModel Header { get; set; } = new();
    public bool IsPreview { get; set; }
    // Design-example values, never used as an account balance or payout policy.
    public decimal ExampleBalance => 20000m;
    public decimal ExampleMinimum => 1000m;
    public decimal ExampleFeePercent => 5m;
}