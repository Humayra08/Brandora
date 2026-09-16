using System.ComponentModel.DataAnnotations;

namespace Brandora.Web.Models.Campaigns;

public class CampaignTargetingFormViewModel
{
    public int CampaignId { get; set; }

    [StringLength(150)]
    public string? TargetLocation { get; set; }

    [Range(1, 500000000, ErrorMessage = "Enter a positive follower count.")]
    public int? TargetFollowersMin { get; set; }

    [Range(1, 500000000, ErrorMessage = "Enter a positive follower count.")]
    public int? TargetFollowersMax { get; set; }

    [Range(0.1, 100, ErrorMessage = "Enter an engagement rate between 0.1 and 100.")]
    public decimal? TargetEngagementRateMin { get; set; }

    public bool TargetVerifiedOnly { get; set; }
}
