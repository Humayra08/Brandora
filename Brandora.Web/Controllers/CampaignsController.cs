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
    public async Task<IActionResult> Index(string? search, CampaignStatus? status, string? platform, string? category, string? sort)
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
            // Platform can hold several comma-separated values (multi-platform
            // campaigns), so match on substring rather than exact equality.
            query = query.Where(c => c.Platform != null && c.Platform.Contains(platform));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(c => c.Niche == category);
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

        var milestoneCounts = await db.CampaignMilestonePlans
            .Where(p => campaignIds.Contains(p.CampaignId))
            .GroupBy(p => p.CampaignId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var vm = new CampaignListViewModel
        {
            Campaigns = campaigns,
            ApplicantCounts = applicantCounts,
            MilestoneCounts = milestoneCounts,
            Search = search,
            Status = status,
            Platform = platform,
            Category = category,
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
    public async Task<IActionResult> Create(CampaignFormViewModel model, bool returnToList = false)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        if (model.StartDate.HasValue && model.Deadline.HasValue && model.StartDate.Value > model.Deadline.Value)
        {
            ModelState.AddModelError(nameof(model.Deadline), "End date must be on or after the start date.");
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
            StartDate = model.StartDate.HasValue ? DateTime.SpecifyKind(model.StartDate.Value, DateTimeKind.Utc) : null,
            Deadline = model.Deadline.HasValue ? DateTime.SpecifyKind(model.Deadline.Value, DateTimeKind.Utc) : null,
            ContentGuidelines = model.ContentGuidelines,
            Status = CampaignStatus.Draft,
            MediaUrl = mediaUrl,
            MediaType = mediaType
        };

        db.Campaigns.Add(campaign);
        await db.SaveChangesAsync();

        return returnToList
            ? RedirectToAction("Index")
            : RedirectToAction("Milestones", new { id = campaign.Id });
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
            StartDate = campaign.StartDate,
            Deadline = campaign.Deadline,
            ContentGuidelines = campaign.ContentGuidelines,
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

        if (model.StartDate.HasValue && model.Deadline.HasValue && model.StartDate.Value > model.Deadline.Value)
        {
            ModelState.AddModelError(nameof(model.Deadline), "End date must be on or after the start date.");
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
        campaign.StartDate = model.StartDate.HasValue ? DateTime.SpecifyKind(model.StartDate.Value, DateTimeKind.Utc) : null;
        campaign.Deadline = model.Deadline.HasValue ? DateTime.SpecifyKind(model.Deadline.Value, DateTimeKind.Utc) : null;
        campaign.ContentGuidelines = model.ContentGuidelines;

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
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

        // Collaborations (and disputes raised on them) are protected against
        // accidental cascade deletes elsewhere in the app, so deleting a
        // campaign that has real collaborations must remove them explicitly
        // and in dependency order — children before the campaign itself.
        var collaborationIds = await db.Collaborations
            .Where(c => c.CampaignId == id)
            .Select(c => c.Id)
            .ToListAsync();

        if (collaborationIds.Count > 0)
        {
            var disputes = await db.Disputes.Where(d => collaborationIds.Contains(d.CollaborationId)).ToListAsync();
            db.Disputes.RemoveRange(disputes);

            var payments = await db.Payments.Where(p => collaborationIds.Contains(p.CollaborationId)).ToListAsync();
            db.Payments.RemoveRange(payments);

            var milestones = await db.Milestones.Where(m => collaborationIds.Contains(m.CollaborationId)).ToListAsync();
            db.Milestones.RemoveRange(milestones);

            var collaborations = await db.Collaborations.Where(c => collaborationIds.Contains(c.Id)).ToListAsync();
            db.Collaborations.RemoveRange(collaborations);
        }

        if (!string.IsNullOrEmpty(campaign.MediaUrl))
        {
            mediaUploads.DeleteMedia(campaign.MediaUrl);
        }

        db.Campaigns.Remove(campaign);
        await db.SaveChangesAsync();

        TempData["CampaignSuccess"] = $"\"{campaign.Title}\" was deleted.";
        return RedirectToAction("Index");
    }

    // ---- Wizard Step 2: Milestones ----

    public async Task<IActionResult> Milestones(int id)
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

        var plans = await db.CampaignMilestonePlans
            .Where(p => p.CampaignId == id)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync();

        return View(new CampaignMilestonesViewModel { Campaign = campaign, Plans = plans });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMilestonePlan(MilestonePlanFormViewModel model)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == model.CampaignId && c.BrandProfileId == brand.Id);
        if (campaign is null)
        {
            return NotFound();
        }

        var plans = await db.CampaignMilestonePlans.Where(p => p.CampaignId == campaign.Id).ToListAsync();

        if (!ModelState.IsValid)
        {
            TempData["MilestoneError"] = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
            return RedirectToAction("Milestones", new { id = campaign.Id });
        }

        if (plans.Sum(p => p.Amount) + model.Amount > campaign.Budget)
        {
            TempData["MilestoneError"] = "This milestone would push your planned total over the campaign budget.";
            return RedirectToAction("Milestones", new { id = campaign.Id });
        }

        db.CampaignMilestonePlans.Add(new CampaignMilestonePlan
        {
            CampaignId = campaign.Id,
            Title = model.Title,
            ContentType = model.ContentType,
            Description = model.Description,
            Amount = model.Amount,
            DueDate = model.DueDate.HasValue ? DateTime.SpecifyKind(model.DueDate.Value, DateTimeKind.Utc) : null,
            SortOrder = plans.Count
        });
        await db.SaveChangesAsync();

        return RedirectToAction("Milestones", new { id = campaign.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMilestonePlan(int id, int campaignId)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var plan = await db.CampaignMilestonePlans
            .FirstOrDefaultAsync(p => p.Id == id && p.CampaignId == campaignId && p.Campaign.BrandProfileId == brand.Id);

        if (plan is not null)
        {
            db.CampaignMilestonePlans.Remove(plan);
            await db.SaveChangesAsync();
        }

        return RedirectToAction("Milestones", new { id = campaignId });
    }

    // ---- Wizard Step 3: Targeting ----

    public async Task<IActionResult> Targeting(int id)
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

        ViewData["Platform"] = campaign.Platform;
        ViewData["MatchingCreatorCount"] = await CountMatchingCreatorsAsync(campaign);
        ViewData["TotalCreatorCount"] = await db.InfluencerProfiles.CountAsync();

        return View(new CampaignTargetingFormViewModel
        {
            CampaignId = campaign.Id,
            TargetLocation = campaign.TargetLocation,
            TargetFollowersMin = campaign.TargetFollowersMin,
            TargetFollowersMax = campaign.TargetFollowersMax,
            TargetEngagementRateMin = campaign.TargetEngagementRateMin,
            TargetVerifiedOnly = campaign.TargetVerifiedOnly
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Targeting(CampaignTargetingFormViewModel model)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == model.CampaignId && c.BrandProfileId == brand.Id);
        if (campaign is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (model.TargetFollowersMin.HasValue && model.TargetFollowersMax.HasValue
            && model.TargetFollowersMin > model.TargetFollowersMax)
        {
            ModelState.AddModelError(nameof(model.TargetFollowersMax), "Maximum followers must be greater than the minimum.");
            return View(model);
        }

        campaign.TargetLocation = string.IsNullOrWhiteSpace(model.TargetLocation) ? null : model.TargetLocation.Trim();
        campaign.TargetFollowersMin = model.TargetFollowersMin;
        campaign.TargetFollowersMax = model.TargetFollowersMax;
        campaign.TargetEngagementRateMin = model.TargetEngagementRateMin;
        campaign.TargetVerifiedOnly = model.TargetVerifiedOnly;
        campaign.TargetingConfigured = true;

        await db.SaveChangesAsync();

        return RedirectToAction("Preview", new { id = campaign.Id });
    }

    // ---- Wizard Step 4: Review & Publish ----

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

        var plans = await db.CampaignMilestonePlans
            .Where(p => p.CampaignId == id)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync();

        var vm = new CampaignReviewViewModel { Campaign = campaign, MilestonePlans = plans };

        if (vm.HasTargeting)
        {
            vm.MatchingCreatorCount = await CountMatchingCreatorsAsync(campaign);
        }

        return View(vm);
    }

    // Real, database-derived count of creators currently matching a
    // campaign's targeting rules — never a fabricated "estimated reach".
    // Shared by the Targeting step (so the brand sees a live number while
    // configuring it) and Review & Publish (the final summary).
    private async Task<int> CountMatchingCreatorsAsync(Campaign campaign)
    {
        var matchQuery = db.InfluencerProfiles.AsQueryable();

        if (!string.IsNullOrWhiteSpace(campaign.TargetLocation))
        {
            matchQuery = matchQuery.Where(i => i.Location != null && i.Location.Contains(campaign.TargetLocation));
        }

        if (campaign.TargetFollowersMin.HasValue)
        {
            matchQuery = matchQuery.Where(i => i.Followers >= campaign.TargetFollowersMin.Value);
        }

        if (campaign.TargetFollowersMax.HasValue)
        {
            matchQuery = matchQuery.Where(i => i.Followers <= campaign.TargetFollowersMax.Value);
        }

        if (campaign.TargetEngagementRateMin.HasValue)
        {
            matchQuery = matchQuery.Where(i => i.EngagementRate >= campaign.TargetEngagementRateMin.Value);
        }

        if (campaign.TargetVerifiedOnly)
        {
            matchQuery = matchQuery.Where(i => i.Verified);
        }

        return await matchQuery.CountAsync();
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

    // Only the exact transitions the UI ever offers are accepted server-side — never an
    // arbitrary CampaignStatus value straight from the request (e.g. jumping Draft
    // straight to Completed, or reverting Completed back to Active).
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

        var allowed =
            (campaign.Status == CampaignStatus.Published && status == CampaignStatus.Active) ||
            (campaign.Status == CampaignStatus.Active && status == CampaignStatus.Completed) ||
            (status == CampaignStatus.Cancelled && campaign.Status is not (CampaignStatus.Completed or CampaignStatus.Cancelled));

        if (!allowed)
        {
            return RedirectToAction("Detail", new { id });
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

        var milestonePlans = await db.CampaignMilestonePlans
            .Where(p => p.CampaignId == id)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync();

        var campaignMilestones = await db.Milestones
            .Where(m => m.Collaboration.CampaignId == id)
            .ToListAsync();

        var pendingPayments = await db.Payments
            .Where(p => p.Collaboration.CampaignId == id && p.Status == PaymentStatus.Pending)
            .SumAsync(p => p.Amount);

        return View(new CampaignDetailViewModel
        {
            Campaign = campaign,
            ApplicantCount = applicantCount,
            CollaborationCount = collaborationCount,
            ConversationCount = conversationCount,
            MilestonePlans = milestonePlans,
            TotalMilestoneCount = campaignMilestones.Count,
            PaidMilestoneCount = campaignMilestones.Count(m => m.Status == MilestoneStatus.Paid),
            PendingPaymentsAmount = pendingPayments
        });
    }
}
