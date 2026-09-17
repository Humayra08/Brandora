using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Brandora.Web.Data;
using Brandora.Web.Models;
using Brandora.Web.Models.Discovery;
using Brandora.Web.Models.Contact;
using Brandora.Web.Models.Domain;

namespace Brandora.Web.Controllers;

public class HomeController(ApplicationDbContext db) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet("platform")]
    public IActionResult Platform()
    {
        return View();
    }

    [HttpGet("for-brands")]
    public async Task<IActionResult> ForBrands()
    {
        // Public directory: only profiles an admin has approved via User Verification.
        // Projected to card fields only, so no contact, budget or admin data leaves the query.
        var brands = await db.BrandProfiles
            .Where(b => b.VerificationStatus == VerificationStatus.Verified)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                b.Id,
                b.CompanyName,
                b.Industry,
                b.ProfilePictureUrl,
                ActiveCampaigns = b.Campaigns.Count(c => c.Status == CampaignStatus.Published || c.Status == CampaignStatus.Active)
            })
            .ToListAsync();

        var vm = DirectoryData.BuildBrandDirectory();
        vm.TotalBrandsDisplay = brands.Count.ToString("N0");
        vm.Brands = brands.Select(b => new BrandCardViewModel
        {
            CompanyName = b.CompanyName,
            Industry = b.Industry,
            ActiveCampaigns = b.ActiveCampaigns,
            LogoText = b.CompanyName,
            LogoImageUrl = b.ProfilePictureUrl,
            LogoBackground = DirectoryData.BrandLogoBackgrounds[b.Id % DirectoryData.BrandLogoBackgrounds.Length]
        }).ToList();

        return View(vm);
    }

    [HttpGet("for-influencers")]
    public async Task<IActionResult> ForInfluencers()
    {
        // Public directory: only profiles an admin has approved via User Verification.
        var influencers = await db.InfluencerProfiles
            .Where(i => i.VerificationStatus == VerificationStatus.Verified)
            .OrderByDescending(i => i.Followers)
            .Select(i => new
            {
                i.Id,
                i.FullName,
                i.ContentNiche,
                i.Location,
                i.Followers,
                i.EngagementRate,
                i.PrimaryPlatform
            })
            .ToListAsync();

        var vm = DirectoryData.BuildInfluencerDirectory();
        vm.TotalInfluencersDisplay = influencers.Count.ToString("N0");
        vm.Influencers = influencers.Select(i => new InfluencerCardViewModel
        {
            FullName = i.FullName,
            Niche = i.ContentNiche,
            Location = i.Location ?? string.Empty,
            Followers = i.Followers,
            EngagementRate = i.EngagementRate,
            Verified = true,
            Platform = i.PrimaryPlatform,
            CoverBackground = DirectoryData.InfluencerCoverBackgrounds[i.Id % DirectoryData.InfluencerCoverBackgrounds.Length]
        }).ToList();

        return View(vm);
    }

    [HttpGet("contact")]
    public IActionResult Contact()
    {
        return View(new ContactIssueViewModel());
    }

    [HttpPost("contact")]
    [ValidateAntiForgeryToken]
    public IActionResult Contact(ContactIssueViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Reports are acknowledged in-page; support picks them up over the
        // channels listed alongside the form.
        TempData["ContactSubmitted"] = true;

        return RedirectToAction(nameof(Contact));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
