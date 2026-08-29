using System.ComponentModel.DataAnnotations;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace Brandora.Web.Models.Proposals;

public class InviteFormViewModel
{
    public int InfluencerProfileId { get; set; }

    [ValidateNever]
    public InfluencerProfile Creator { get; set; } = null!;

    [ValidateNever]
    public List<Campaign> AvailableCampaigns { get; set; } = new();

    [Required(ErrorMessage = "Select a campaign for this invite.")]
    public int CampaignId { get; set; }

    [Required]
    [Range(1, 100000000, ErrorMessage = "Enter an amount greater than zero.")]
    public decimal ProposedAmount { get; set; }

    [Required]
    [StringLength(1000)]
    public string Deliverables { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Timeline { get; set; }

    [StringLength(1000)]
    public string? Message { get; set; }
}
