using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Influencers;

public class CreatorProfileViewModel
{
    public InfluencerProfile Creator { get; set; } = null!;
    public bool IsShortlisted { get; set; }
}
