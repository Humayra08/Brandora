using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public class AdminPaymentsController(ApplicationDbContext db) : AdminControllerBase(db)
{
    public async Task<IActionResult> Index(PaymentStatus? status, DateTime? from, DateTime? to)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Payments";
        ViewData["Title"] = "Payment Oversight";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Payment Oversight", null) };
        ViewData["StatusFilter"] = status;
        ViewData["FromFilter"] = from;
        ViewData["ToFilter"] = to;

        var query = db.Payments
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(p => p.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Include(p => p.Milestone)
            .AsQueryable();

        if (status is not null)
        {
            query = query.Where(p => p.Status == status);
        }

        if (from is not null)
        {
            query = query.Where(p => p.CreatedAt >= from);
        }

        if (to is not null)
        {
            query = query.Where(p => p.CreatedAt <= to);
        }

        var payments = await query.OrderByDescending(p => p.CreatedAt).ToListAsync();
        return View(payments);
    }

    public async Task<IActionResult> Details(int id)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Payments";
        ViewData["Title"] = "Transaction Detail";
        ViewData["Breadcrumb"] = new List<(string, string?)>
        {
            ("Payment Oversight", "/Admin/AdminPayments/Index"),
            ("Detail", null)
        };

        var payment = await db.Payments
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .Include(p => p.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Include(p => p.Milestone)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (payment is null) return NotFound();

        var dispute = payment.MilestoneId is not null
            ? await db.Disputes.FirstOrDefaultAsync(d => d.MilestoneId == payment.MilestoneId)
            : null;

        ViewData["LinkedDispute"] = dispute;

        return View(payment);
    }
}
