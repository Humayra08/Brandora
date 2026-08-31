using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Dashboard;

public class ConnectedPlatformRow
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string IconGradient { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public bool Verified { get; set; }
    public int Followers { get; set; }
    public string FollowerLabel { get; set; } = "Followers";
    public bool Connected { get; set; }
}

public class ReachPoint
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
}

public class SocialAccountsViewModel
{
    public InfluencerProfile Profile { get; set; } = null!;

    public List<ConnectedPlatformRow> Platforms { get; set; } = new();

    public int TotalFollowers { get; set; }
    public int TotalFollowersGrowthPercent { get; set; }
    public int TotalReach { get; set; }
    public int TotalReachGrowthPercent { get; set; }
    public decimal EngagementRate { get; set; }
    public decimal EngagementRateGrowthPercent { get; set; }
    public int AvgViews { get; set; }
    public int AvgViewsGrowthPercent { get; set; }

    public List<ReachPoint> ReachSeries { get; set; } = new();
}
