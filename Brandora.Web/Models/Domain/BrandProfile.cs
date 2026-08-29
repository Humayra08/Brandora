namespace Brandora.Web.Models.Domain;

public class BrandProfile
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string CompanyName { get; set; } = string.Empty;
    public string ContactFullName { get; set; } = string.Empty;
    public string? WebsiteUrl { get; set; }
    public string Industry { get; set; } = string.Empty;
    public string MonthlyBudget { get; set; } = string.Empty;
    public string? ProfilePictureUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Campaign> Campaigns { get; set; } = new List<Campaign>();
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    public ICollection<ShortlistEntry> ShortlistEntries { get; set; } = new List<ShortlistEntry>();
}
