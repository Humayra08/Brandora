using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services.Email;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public class AdminContactSubmissionsController(ApplicationDbContext db, IEmailSender email) : AdminControllerBase(db)
{
    /// <summary>
    /// The acknowledgement wording is fixed for every submission, so it is built
    /// here rather than accepted from the browser. Only the name varies.
    /// </summary>
    public const string AcknowledgementSubject = "Thank you for contacting Brandora";

    public static string AcknowledgementBody(string fullName) =>
        $"""
        Dear {fullName},

        Thank you for contacting us and sharing your issue with Brandora.

        We have received your request and our team is currently working on it. We appreciate your patience and will get back to you once the issue has been resolved.

        Best regards,
        Brandora Support Team
        """;

    public async Task<IActionResult> Index(ContactSubmissionStatus? status)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "ContactSubmissions";
        ViewData["Title"] = "Contact Submissions";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Contact Submissions", null) };
        ViewData["StatusFilter"] = status;

        var query = db.ContactSubmissions.AsQueryable();

        if (status is not null)
        {
            query = query.Where(c => c.Status == status);
        }

        var submissions = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();
        return View(submissions);
    }

    public async Task<IActionResult> Details(int id)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "ContactSubmissions";
        ViewData["Title"] = "Contact Submission";
        ViewData["Breadcrumb"] = new List<(string, string?)>
        {
            ("Contact Submissions", "/Admin/AdminContactSubmissions/Index"),
            ("Details", null)
        };

        var submission = await db.ContactSubmissions.FirstOrDefaultAsync(c => c.Id == id);

        if (submission is null)
        {
            return NotFound();
        }

        ViewData["EmailConfigured"] = email.IsConfigured;
        return View(submission);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendAcknowledgement(int id)
    {
        var submission = await db.ContactSubmissions.FirstOrDefaultAsync(c => c.Id == id);

        if (submission is null)
        {
            return NotFound();
        }

        if (submission.Status != ContactSubmissionStatus.New)
        {
            TempData["ContactError"] = "This submission has already been acknowledged.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Recipient comes from the stored record, never from the request.
        var sent = await email.SendAsync(submission.Email, submission.FullName, AcknowledgementSubject,
            EmailTemplates.Message(AcknowledgementBody(submission.FullName)));
        if (!sent)
        {
            // Status is left untouched so the admin can retry.
            TempData["ContactError"] = "The email could not be sent, so the status is unchanged. Check the SMTP settings and try again.";
            return RedirectToAction(nameof(Details), new { id });
        }

        submission.Status = ContactSubmissionStatus.InProgress;
        await db.SaveChangesAsync();

        TempData["ContactMessage"] = $"Acknowledgement sent to {submission.Email}. Status is now In Progress.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendResolution(int id, string subject, string body)
    {
        var submission = await db.ContactSubmissions.FirstOrDefaultAsync(c => c.Id == id);

        if (submission is null)
        {
            return NotFound();
        }

        if (submission.Status != ContactSubmissionStatus.InProgress)
        {
            TempData["ContactError"] = "Send the acknowledgement first — only in-progress submissions can be resolved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
        {
            TempData["ContactError"] = "Enter both a subject and a message before sending.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var sent = await email.SendAsync(submission.Email, submission.FullName, subject.Trim(),
            EmailTemplates.Message(body.Trim()));
        if (!sent)
        {
            TempData["ContactError"] = "The email could not be sent, so the status stays In Progress. Check the SMTP settings and try again.";
            return RedirectToAction(nameof(Details), new { id });
        }

        submission.Status = ContactSubmissionStatus.Resolved;
        await db.SaveChangesAsync();

        TempData["ContactMessage"] = $"Resolution sent to {submission.Email}. Status is now Resolved.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
