namespace Brandora.Web.Models.Domain;

/// <summary>
/// One "Report an Issue" submission from the public Contact page. Visitors need
/// no account to submit, so the row stands alone — only what the sender typed,
/// plus the time it arrived and the status admins work it through.
/// </summary>
public class ContactSubmission
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string IssueType { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public ContactSubmissionStatus Status { get; set; } = ContactSubmissionStatus.New;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
