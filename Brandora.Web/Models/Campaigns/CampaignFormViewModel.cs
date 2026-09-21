using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Brandora.Web.Models.Campaigns;

public class CampaignFormViewModel
{
    public int? Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a primary platform.")]
    public string Platform { get; set; } = string.Empty;

    [Required(ErrorMessage = "Select a campaign niche.")]
    public string Niche { get; set; } = string.Empty;

    [Range(1, 100000000, ErrorMessage = "Enter a budget greater than zero.")]
    public decimal Budget { get; set; }

    [DataType(DataType.Date)]
    public DateTime? StartDate { get; set; }

    [DataType(DataType.Date)]
    public DateTime? Deadline { get; set; }

    [StringLength(2000)]
    public string? ContentGuidelines { get; set; }

    // Banner (cover image) — image only; shown as the campaign's cover in cards and lists.
    public IFormFile? MediaFile { get; set; }

    public bool RemoveMedia { get; set; }

    public string? ExistingMediaUrl { get; set; }
    public string? ExistingMediaType { get; set; }

    // Campaign video / reel — video only; shown on the campaign's details page.
    public IFormFile? VideoFile { get; set; }

    public bool RemoveVideo { get; set; }

    public string? ExistingVideoUrl { get; set; }
}

// The campaign video player (Views/Shared/_CampaignReel.cshtml), shared by the Brand's
// campaign Detail/Preview pages and the creator's campaign Details page.
public record CampaignReelModel(string VideoUrl, string? BannerUrl, string BrandName, bool ForCreator);
