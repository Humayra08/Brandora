using System.ComponentModel.DataAnnotations;

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
    public DateTime? Deadline { get; set; }
}
