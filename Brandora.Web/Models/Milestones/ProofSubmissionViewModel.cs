using System.ComponentModel.DataAnnotations;

namespace Brandora.Web.Models.Milestones;

public class ProofSubmissionViewModel
{
    public int MilestoneId { get; set; }

    [Required(ErrorMessage = "Add a link to the delivered post or content.")]
    [Url(ErrorMessage = "Enter a valid URL.")]
    public string ProofUrl { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? ProofNotes { get; set; }
}
