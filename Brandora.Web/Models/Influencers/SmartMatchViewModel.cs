using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Influencers;

public class SmartMatchResult
{
    public InfluencerProfile Creator { get; set; } = null!;
    public int Score { get; set; }
    public List<string> Reasons { get; set; } = new();
    public bool IsShortlisted { get; set; }
}

public class SmartMatchViewModel
{
    public Campaign Campaign { get; set; } = null!;
    public List<SmartMatchResult> Results { get; set; } = new();
}
