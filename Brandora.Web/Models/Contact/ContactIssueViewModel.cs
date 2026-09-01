using System.ComponentModel.DataAnnotations;

namespace Brandora.Web.Models.Contact;

/// <summary>
/// Backs the "Report an Issue" form on the public Contact page. The report is
/// validated and acknowledged in-page only — nothing here is persisted, so the
/// page needs no schema of its own.
/// </summary>
public class ContactIssueViewModel
{
    [Required(ErrorMessage = "Please enter your full name.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select an account type.")]
    [Display(Name = "Account Type")]
    public string AccountType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select an issue type.")]
    [Display(Name = "Issue Type")]
    public string IssueType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a subject.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please describe the issue.")]
    public string Description { get; set; } = string.Empty;

    public static readonly string[] AccountTypes =
    [
        "Brand",
        "Influencer",
        "Not registered yet"
    ];

    public static readonly string[] IssueTypes =
    [
        "Account & Login",
        "Payments & Billing",
        "Campaigns & Collaborations",
        "Report a User",
        "Technical Problem",
        "Other"
    ];
}
