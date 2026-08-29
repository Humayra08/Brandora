using Brandora.Web.Data;
using Brandora.Web.Models.Campaigns;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class CampaignsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, MediaUploadService mediaUploads) : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(string? search, CampaignStatus? status, string? platform, string? sort)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var baseQuery = db.Campaigns.Where(c => c.BrandProfileId == brand.Id);

        var summary = await baseQuery
            .GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var query = baseQuery.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Title.Contains(search));
        }

        if (status.HasValue)
        {
            query = query.Where(c => c.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            query = query.Where(c => c.Platform == platform);
        }

        query = sort switch
        {
            "budget" => query.OrderByDescending(c => c.Budget),
            "oldest" => query.OrderBy(c => c.CreatedAt),
            _ => query.OrderByDescending(c => c.CreatedAt)
        };

        var campaigns = await query.ToListAsync();
        var campaignIds = campaigns.Select(c => c.Id).ToList();

        var applicantCounts = await db.Proposals
            .Where(p => campaignIds.Contains(p.CampaignId))
            .GroupBy(p => p.CampaignId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var vm = new CampaignListViewModel
        {
            Campaigns = campaigns,
            ApplicantCounts = applicantCounts,
            Search = search,
            Status = status,
            Platform = platform,
            Sort = sort,
            DraftCount = summary.FirstOrDefault(s => s.Status == CampaignStatus.Draft)?.Count ?? 0,
            PublishedCount = summary.FirstOrDefault(s => s.Status == CampaignStatus.Published)?.Count ?? 0,
            ActiveCount = summary.FirstOrDefault(s => s.Status == CampaignStatus.Active)?.Count ?? 0,
            CompletedCount = summary.FirstOrDefault(s => s.Status == CampaignStatus.Completed)?.Count ?? 0,
            TotalCount = summary.Sum(s => s.Count)
        };

        return View(vm);
    }

    public async Task<IActionResult> Create()
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new CampaignFormViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Create(CampaignFormViewModel model)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string? mediaUrl = null;
        string? mediaType = null;

        if (model.MediaFile is { Length: > 0 })
        {
            var (url, type, error) = await mediaUploads.SaveMediaAsync(model.MediaFile, "campaigns");
            if (error is not null)
            {
                ModelState.AddModelError(string.Empty, error);
                return View(model);
            }

            mediaUrl = url;
            mediaType = type;
        }

        var campaign = new Campaign
        {
            BrandProfileId = brand.Id,
            Title = model.Title,
            Description = model.Description,
            Platform = model.Platform,
            Niche = model.Niche,
            Budget = model.Budget,
            Deadline = model.Deadline,
            Status = CampaignStatus.Draft,
            MediaUrl = mediaUrl,
            MediaType = mediaType
        };

        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        return RedirectToAction("Preview", new { id = campaign.Id });
    }

    public async Task<IActionResult> Edit(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.BrandProfileId == brand.Id);
        if (campaign is null)
        {
            return NotFound();
        }

        return View("Create", new CampaignFormViewModel
        {
            Id = campaign.Id,
            Title = campaign.Title,
            Description = campaign.Description,
            Platform = campaign.Platform ?? string.Empty,
            Niche = campaign.Niche ?? string.Empty,
            Budget = campaign.Budget,
            Deadline = campaign.Deadline,
            ExistingMediaUrl = campaign.MediaUrl,
            ExistingMediaType = campaign.MediaType
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(100_000_000)]
    public async Task<IActionResult> Edit(int id, CampaignFormViewModel model)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.BrandProfileId == brand.Id);
        if (campaign is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.Id = id;
            model.ExistingMediaUrl = campaign.MediaUrl;
            model.ExistingMediaType = campaign.MediaType;
            return View("Create", model);
        }

        campaign.Title = model.Title;
        campaign.Description = model.Description;
        campaign.Platform = model.Platform;
        campaign.Niche = model.Niche;
        campaign.Budget = model.Budget;
        campaign.Deadline = model.Deadline;

        if (model.RemoveMedia && campaign.MediaUrl is not null)
        {
            mediaUploads.DeleteMedia(campaign.MediaUrl);
            campaign.MediaUrl = null;
            campaign.MediaType = null;
        }

        if (model.MediaFile is { Length: > 0 })
        {
            var (url, type, error) = await mediaUploads.SaveMediaAsync(model.MediaFile, "campaigns");
            if (error is not null)
            {
                ModelState.AddModelError(string.Empty, error);
                model.ExistingMediaUrl = campaign.MediaUrl;
                model.ExistingMediaType = campaign.MediaType;
                return View("Create", model);
            }

            mediaUploads.DeleteMedia(campaign.MediaUrl);
            campaign.MediaUrl = url;
            campaign.MediaType = type;
        }

        await db.SaveChangesAsync();

        return RedirectToAction(campaign.Status == CampaignStatus.Draft ? "Preview" : "Detail", new { id = campaign.Id });
    }

    public async Task<IActionResult> Preview(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.BrandProfileId == brand.Id);
        if (campaign is null)
        {
            return NotFound();
        }

        if (campaign.Status != CampaignStatus.Draft)
        {
            return RedirectToAction("Detail", new { id });
        }

        return View(campaign);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.BrandProfileId == brand.Id);
        if (campaign is null)
        {
            return NotFound();
        }

        campaign.Status = CampaignStatus.Published;
        await db.SaveChangesAsync();

        return RedirectToAction("Detail", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, CampaignStatus status)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.BrandProfileId == brand.Id);
        if (campaign is null)
        {
            return NotFound();
        }

        campaign.Status = status;
        await db.SaveChangesAsync();

        return RedirectToAction("Detail", new { id });
    }

    public async Task<IActionResult> Detail(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == id && c.BrandProfileId == brand.Id);
        if (campaign is null)
        {
            return NotFound();
        }

        var applicantCount = await db.Proposals.CountAsync(p => p.CampaignId == id);
        var collaborationCount = await db.Collaborations.CountAsync(c => c.CampaignId == id);
        var conversationCount = await db.Conversations.CountAsync(c => c.CampaignId == id);

        return View(new CampaignDetailViewModel
        {
            Campaign = campaign,
            ApplicantCount = applicantCount,
            CollaborationCount = collaborationCount,
            ConversationCount = conversationCount
        });
    }
}
