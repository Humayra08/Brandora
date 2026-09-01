using Brandora.Web.Models.Domain;

namespace Brandora.Web.Models.Dashboard;

public class InfluencerProfileFormModel
{
    public string FullName { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Location { get; set; }
    public string? ContentNiche { get; set; }
    public string? Bio { get; set; }
    public string? WebsiteUrl { get; set; }
}

public class InfluencerSettingsViewModel
{
    public InfluencerProfile Profile { get; set; } = null!;
    public InfluencerProfileFormModel Form { get; set; } = new();
}
