using Brandora.Web.Models.Domain;
namespace Brandora.Web.Models.Dashboard;

public class InfluencerEarningsViewModel
{
    public string InfluencerName { get; set; } = "";
    public string Tab { get; set; } = "milestones";
    public int? CampaignId { get; set; }
    public string? Search { get; set; }
    public bool AllActivity { get; set; }
    public decimal Received { get; set; }
    public decimal Pending { get; set; }
    public decimal TotalEarnings => Received + Pending;
    public int ReceivedMilestones { get; set; }
    public int PendingMilestones { get; set; }
    public int ActiveCampaigns { get; set; }
    public List<Collaboration> Collaborations { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];
    public List<Campaign> Campaigns { get; set; } = [];
    public List<Notification> Notifications { get; set; } = [];
    public List<EarningsActivity> Activity { get; set; } = [];

    public static string? SafeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(char.IsControl) || value.Contains('\\')) return null;
        if (value.StartsWith('/') && !value.StartsWith("//")) return value;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" ? value : null;
    }

    public static string? ImageUrl(string? value)
    {
        var safe = SafeUrl(value);
        if (safe is null) return null;
        var extension = Path.GetExtension(safe.Split('?', '#')[0]).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".webp" or ".gif" or ".avif" ? safe : null;
    }
}
public record EarningsActivity(string Title, string Description, DateTime Date, string Kind);