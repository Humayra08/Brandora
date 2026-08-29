using Brandora.Web.Models.Domain;

namespace Brandora.Web.Services;

public static class SmartMatchScorer
{
    public static (int Score, List<string> Reasons) ComputeMatch(InfluencerProfile creator, Campaign campaign)
    {
        var score = 0;
        var reasons = new List<string>();

        if (!string.IsNullOrEmpty(campaign.Platform) &&
            (string.Equals(creator.PrimaryPlatform, campaign.Platform, StringComparison.OrdinalIgnoreCase)
             || campaign.Platform == "Multi-Platform"))
        {
            score += 50;
            reasons.Add($"Active on {creator.PrimaryPlatform}, your campaign's target platform");
        }

        if (!string.IsNullOrEmpty(campaign.Niche) &&
            string.Equals(creator.ContentNiche, campaign.Niche, StringComparison.OrdinalIgnoreCase))
        {
            score += 35;
            reasons.Add($"Creates {creator.ContentNiche} content, matching your campaign niche");
        }

        var engagementPoints = (int)Math.Min(15, Math.Round(creator.EngagementRate * 3, MidpointRounding.AwayFromZero));
        if (engagementPoints > 0)
        {
            score += engagementPoints;
            reasons.Add($"{creator.EngagementRate:0.0}% engagement rate");
        }

        return (Math.Min(100, score), reasons);
    }
}
