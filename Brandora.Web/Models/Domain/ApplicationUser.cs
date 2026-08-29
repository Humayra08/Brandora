using Microsoft.AspNetCore.Identity;

namespace Brandora.Web.Models.Domain;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;

    public UserRole Role { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public BrandProfile? BrandProfile { get; set; }

    public InfluencerProfile? InfluencerProfile { get; set; }
}
