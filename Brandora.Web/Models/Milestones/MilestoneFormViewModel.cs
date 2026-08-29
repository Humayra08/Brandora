using System.ComponentModel.DataAnnotations;

namespace Brandora.Web.Models.Milestones;

public class MilestoneFormViewModel
{
    public int CollaborationId { get; set; }

    [Required]
    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [Required]
    [Range(1, 100000000, ErrorMessage = "Enter an amount greater than zero.")]
    public decimal Amount { get; set; }

    [DataType(DataType.Date)]
    public DateTime? DueDate { get; set; }
}
