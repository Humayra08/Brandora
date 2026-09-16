using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public class AdminCampaignsController(ApplicationDbContext db) : AdminControllerBase(db)
{
    public async Task<IActionResult> Index(CampaignStatus? status, string? search)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Campaigns";
        ViewData["Title"] = "Campaigns";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Campaigns", null) };
        ViewData["StatusFilter"] = status;
        ViewData["Search"] = search ?? "";

        var query = db.Campaigns
            .Include(c => c.BrandProfile)
            .AsQueryable();

        if (status is not null)
        {
            query = query.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Title.Contains(search) || c.BrandProfile.CompanyName.Contains(search) || (c.Niche != null && c.Niche.Contains(search)));
        }

        var campaigns = await query
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new
            {
                Campaign = c,
                ApplicantCount = c.Proposals.Count(),
                AcceptedInfluencers = c.Proposals.Count(p => p.Status == ProposalStatus.Accepted),
                MilestoneCount = db.Collaborations.Where(col => col.CampaignId == c.Id).SelectMany(col => col.Milestones).Count()
            })
            .ToListAsync();

        var vm = campaigns.Select(x => new AdminCampaignRow(
            x.Campaign.Id,
            x.Campaign.Title,
            x.Campaign.Description,
            x.Campaign.BrandProfile.CompanyName,
            x.Campaign.Niche,
            x.Campaign.Platform,
            x.Campaign.Status,
            x.Campaign.Budget,
            x.Campaign.SpentAmount,
            x.AcceptedInfluencers,
            x.ApplicantCount,
            x.MilestoneCount,
            x.Campaign.Deadline,
            x.Campaign.CreatedAt)).ToList();

        return View(vm);
    }
}

public record AdminCampaignRow(
    int Id,
    string Title,
    string Description,
    string BrandName,
    string? Niche,
    string? Platform,
    CampaignStatus Status,
    decimal Budget,
    decimal SpentAmount,
    int AcceptedInfluencers,
    int ApplicantCount,
    int MilestoneCount,
    DateTime? Deadline,
    DateTime CreatedAt);
