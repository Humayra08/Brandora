using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public class AdminContactSubmissionsController(ApplicationDbContext db) : AdminControllerBase(db)
{
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
}
