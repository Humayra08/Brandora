namespace Brandora.Web.Models.Domain;

public class ShortlistEntry
{
    public int Id { get; set; }

    public int BrandProfileId { get; set; }
    public BrandProfile BrandProfile { get; set; } = null!;

    public int InfluencerProfileId { get; set; }
    public InfluencerProfile InfluencerProfile { get; set; } = null!;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
