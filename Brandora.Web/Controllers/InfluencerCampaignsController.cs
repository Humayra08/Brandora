using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class InfluencerCampaignsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, NotificationService notifications) : InfluencerControllerBase(userManager, db)
{
    public async Task<IActionResult> Index(string? search, string? category, string? platform, string? budget, string? sort, string? tab, int page = 1)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var browsable = db.Campaigns
            .Include(c => c.BrandProfile)
            .Where(c => c.Status == CampaignStatus.Published || c.Status == CampaignStatus.Active || ((c.Status == CampaignStatus.Completed || c.Status == CampaignStatus.Cancelled) && db.Collaborations.Any(x => x.CampaignId == c.Id && x.InfluencerProfileId == influencer.Id)));

        var myProposals = await db.Proposals
            .Where(p => p.InfluencerProfileId == influencer.Id)
            .ToListAsync();

        var myProposalByCampaign = myProposals
            .GroupBy(p => p.CampaignId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedAt).First().Status);

        var myCollaborations = await db.Collaborations.Include(c => c.Milestones)
            .Where(c => c.InfluencerProfileId == influencer.Id)
            .ToListAsync();

        var myActiveCollabCampaignIds = myCollaborations
            .Where(c => c.Status == CollaborationStatus.Active)
            .Select(c => c.CampaignId)
            .ToHashSet();

        var myCompletedCollabCampaignIds = myCollaborations
            .Where(c => c.Status == CollaborationStatus.Completed)
            .Select(c => c.CampaignId)
            .ToHashSet();

        var query = browsable.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => c.Title.Contains(search) || c.Description.Contains(search) || c.BrandProfile.CompanyName.Contains(search) || (c.Niche != null && c.Niche.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(c => c.Niche == category);
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            query = query.Where(c => c.Platform == platform);
        }

        query = budget switch
        {
            "under10k" => query.Where(c => c.Budget < 10000),
            "10k-25k" => query.Where(c => c.Budget >= 10000 && c.Budget <= 25000),
            "25k-50k" => query.Where(c => c.Budget > 25000 && c.Budget <= 50000),
            "over50k" => query.Where(c => c.Budget > 50000),
            _ => query
        };

        query = sort switch
        {
            "budget" => query.OrderByDescending(c => c.Budget),
            "deadline" => query.OrderBy(c => c.Deadline ?? DateTime.MaxValue),
            _ => query.OrderByDescending(c => c.CreatedAt)
        };

        var campaigns = await query.ToListAsync();
        var campaignIds = campaigns.Select(c => c.Id).ToList();

        var applicantCounts = await db.Proposals
            .Where(p => campaignIds.Contains(p.CampaignId))
            .GroupBy(p => p.CampaignId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var allRows = campaigns.Select(c => new BrowseCampaignRow
        {
            CampaignId = c.Id,
            Title = c.Title,
            Description = c.Description,
            BrandName = c.BrandProfile.CompanyName,
            Platform = c.Platform,
            Niche = c.Niche,
            Budget = c.Budget,
            Deadline = c.Deadline,
            CreatedAt = c.CreatedAt,
            MediaUrl = c.MediaUrl,
            Status = c.Status,
            ApplicantCount = applicantCounts.GetValueOrDefault(c.Id),
            MilestoneCount = myCollaborations.Where(x => x.CampaignId == c.Id).Select(x => (int?)x.Milestones.Count).FirstOrDefault(),
            MyProposalStatus = myProposalByCampaign.TryGetValue(c.Id, out var proposalStatus) ? proposalStatus : null,
            IsCollaborating = myActiveCollabCampaignIds.Contains(c.Id),
            IsCollabCompleted = myCompletedCollabCampaignIds.Contains(c.Id)
        }).ToList();

        var selectedTab = tab is "open" or "closed" or "applied" or "inreview" or "ongoing" or "completed" ? tab : "all";

        var tabFiltered = selectedTab switch
        {
            "open" => allRows.Where(r => (r.Status == CampaignStatus.Published || r.Status == CampaignStatus.Active) && (!r.Deadline.HasValue || r.Deadline >= DateTime.UtcNow)).ToList(),
            "closed" => allRows.Where(r => r.Status == CampaignStatus.Cancelled || r.Status == CampaignStatus.Completed || r.Deadline < DateTime.UtcNow).ToList(),
            "applied" => allRows.Where(r => r.MyProposalStatus.HasValue).ToList(),
            "inreview" => allRows.Where(r => r.MyProposalStatus == ProposalStatus.Pending).ToList(),
            "ongoing" => allRows.Where(r => r.IsCollaborating).ToList(),
            "completed" => allRows.Where(r => r.IsCollabCompleted).ToList(),
            _ => allRows
        };

        var totalFiltered = tabFiltered.Count;
        var pageSize = 4;
        var pageNumber = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(totalFiltered / (double)pageSize)));
        var pagedRows = tabFiltered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        var platformOptions = await browsable
            .Where(c => c.Platform != null)
            .Select(c => c.Platform!)
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync();

        var categoryOptions = await browsable
            .Where(c => c.Niche != null)
            .Select(c => c.Niche!)
            .Distinct()
            .OrderBy(n => n)
            .ToListAsync();

        var vm = new InfluencerCampaignsViewModel
        {
            Profile = influencer,
            Notifications = await db.Notifications.AsNoTracking().Where(n => n.UserId == influencer.UserId).OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync(),
            Campaigns = pagedRows,
            PlatformOptions = platformOptions,
            CategoryOptions = categoryOptions,
            Search = search,
            Category = category,
            Platform = platform,
            Budget = budget,
            Sort = sort,
            Tab = selectedTab,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalFilteredCount = totalFiltered,
            AllCount = allRows.Count,
            AppliedCount = allRows.Count(r => r.MyProposalStatus.HasValue),
            InReviewCount = allRows.Count(r => r.MyProposalStatus == ProposalStatus.Pending),
            OngoingCount = allRows.Count(r => r.IsCollaborating),
            CompletedCount = allRows.Count(r => r.IsCollabCompleted)
        };

        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaign = await db.Campaigns.Include(c => c.BrandProfile).FirstOrDefaultAsync(c => c.Id == id);
        if (campaign is null)
        {
            return NotFound();
        }

        var isBrowsable = campaign.Status is CampaignStatus.Published or CampaignStatus.Active
            || await db.Collaborations.AnyAsync(x => x.CampaignId == campaign.Id && x.InfluencerProfileId == influencer.Id);
        if (!isBrowsable)
        {
            return NotFound();
        }

        var myProposal = await db.Proposals.Where(p => p.CampaignId == campaign.Id && p.InfluencerProfileId == influencer.Id)
            .OrderByDescending(p => p.CreatedAt).Select(p => new { p.Id, p.Status }).FirstOrDefaultAsync();

        var myProposalStatus = myProposal is null ? (ProposalStatus?)null : myProposal.Status;

        var collaboration = await db.Collaborations.AsNoTracking().Include(c => c.Milestones).FirstOrDefaultAsync(c => c.CampaignId == campaign.Id && c.InfluencerProfileId == influencer.Id);

        var closed = campaign.Status is CampaignStatus.Completed or CampaignStatus.Cancelled || campaign.Deadline < DateTime.UtcNow;

        List<CampaignDetailsMilestone> milestones;
        if (collaboration is not null)
        {
            milestones = collaboration.Milestones.OrderBy(m => m.DueDate ?? DateTime.MaxValue).ThenBy(m => m.Id)
                .Select(m => new CampaignDetailsMilestone { Title = m.Title, Description = m.Description,
                    Amount = m.Amount, DueDate = m.DueDate, Status = m.Status }).ToList();
        }
        else
        {
            milestones = await db.CampaignMilestonePlans.AsNoTracking().Where(p => p.CampaignId == campaign.Id)
                .OrderBy(p => p.SortOrder).ThenBy(p => p.DueDate ?? DateTime.MaxValue).ThenBy(p => p.Id)
                .Select(p => new CampaignDetailsMilestone { Title = p.Title, Description = p.Description,
                    Amount = p.Amount, DueDate = p.DueDate, Status = MilestoneStatus.Pending }).ToListAsync();
        }

        var vm = new InfluencerCampaignDetailsViewModel
        {
            Profile = influencer,
            Notifications = await db.Notifications.AsNoTracking().Where(n => n.UserId == influencer.UserId).OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync(),
            CampaignId = campaign.Id,
            Title = campaign.Title,
            Description = campaign.Description,
            BrandName = campaign.BrandProfile.CompanyName,
            BrandLogoUrl = campaign.BrandProfile.ProfilePictureUrl,
            BrandVerified = campaign.BrandProfile.VerificationStatus == VerificationStatus.Verified,
            MyProposalId = myProposal?.Id,
            Milestones = milestones,
            BrandIndustry = campaign.BrandProfile.Industry,
            BrandWebsiteUrl = campaign.BrandProfile.WebsiteUrl,
            Platform = campaign.Platform,
            Niche = campaign.Niche,
            Budget = campaign.Budget,
            Deadline = campaign.Deadline,
            CreatedAt = campaign.CreatedAt,
            Status = campaign.Status,
            ApplicantCount = await db.Proposals.CountAsync(p => p.CampaignId == campaign.Id),
            MyProposalStatus = myProposalStatus,
            IsCollaborating = collaboration?.Status == CollaborationStatus.Active,
            IsCollabCompleted = collaboration?.Status == CollaborationStatus.Completed,
            CanApply = !closed && !myProposalStatus.HasValue && collaboration is null
        };

        return View(vm);
    }

    private async Task<CampaignApplyViewModel?> BuildApplyViewModelAsync(int id, InfluencerProfile influencer)
    {
        var campaign = await db.Campaigns.Include(c => c.BrandProfile).FirstOrDefaultAsync(c => c.Id == id);
        if (campaign is null)
        {
            return null;
        }

        return new CampaignApplyViewModel
        {
            Profile = influencer,
            Notifications = await db.Notifications.AsNoTracking().Where(n => n.UserId == influencer.UserId).OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync(),
            CampaignId = campaign.Id,
            Title = campaign.Title,
            Description = campaign.Description,
            BrandName = campaign.BrandProfile.CompanyName,
            BrandLogoUrl = campaign.BrandProfile.ProfilePictureUrl,
            BrandVerified = campaign.BrandProfile.VerificationStatus == VerificationStatus.Verified,
            Platform = campaign.Platform,
            Niche = campaign.Niche,
            Budget = campaign.Budget,
            Deadline = campaign.Deadline,
            ApplicantCount = await db.Proposals.CountAsync(p => p.CampaignId == campaign.Id),
            MilestoneCount = await db.CampaignMilestonePlans.CountAsync(p => p.CampaignId == campaign.Id)
        };
    }


    public async Task<IActionResult> Apply(int id, string? concept, string? whyGoodFit, string? instagramLink, string? tikTokLink, string? youTubeLink, string? instagramProfileUrl, string? facebookProfileUrl, string? tikTokProfileUrl)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var vm = await BuildApplyViewModelAsync(id, influencer);
        if (vm is null)
        {
            return NotFound();
        }

        vm.Step = 2;
        vm.Concept = concept ?? "";
        vm.WhyGoodFit = whyGoodFit ?? "";
        vm.InstagramLink = instagramLink;
        vm.TikTokLink = tikTokLink;
        vm.YouTubeLink = youTubeLink;
        vm.InstagramProfileUrl = instagramProfileUrl;
        vm.FacebookProfileUrl = facebookProfileUrl;
        vm.TikTokProfileUrl = tikTokProfileUrl;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(CampaignApplyInputModel form)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var vm = await BuildApplyViewModelAsync(form.CampaignId, influencer);
        if (vm is null)
        {
            return NotFound();
        }

        vm.Step = 2;
        // Model binding converts a posted empty string to null (ConvertEmptyStringToNull),
        // so these must be normalized before the view touches Concept.Length etc.
        vm.Concept = form.Concept ?? "";
        vm.WhyGoodFit = form.WhyGoodFit ?? "";
        vm.InstagramLink = form.InstagramLink;
        vm.TikTokLink = form.TikTokLink;
        vm.YouTubeLink = form.YouTubeLink;
        vm.InstagramProfileUrl = form.InstagramProfileUrl;
        vm.FacebookProfileUrl = form.FacebookProfileUrl;
        vm.TikTokProfileUrl = form.TikTokProfileUrl;

        if (string.IsNullOrWhiteSpace(vm.Concept))
        {
            ModelState.AddModelError(nameof(vm.Concept), "Share your creative idea for this campaign.");
        }
        if (string.IsNullOrWhiteSpace(vm.WhyGoodFit))
        {
            ModelState.AddModelError(nameof(vm.WhyGoodFit), "Tell the brand why you're a good fit.");
        }
        if (!ModelState.IsValid)
        {
            return View(vm);
        }

        vm.Step = 3;
        return View("ApplySocial", vm);
    }

    public async Task<IActionResult> ApplySocial(int id, string? concept, string? whyGoodFit, string? instagramLink, string? tikTokLink, string? youTubeLink, string? instagramProfileUrl, string? facebookProfileUrl, string? tikTokProfileUrl)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var vm = await BuildApplyViewModelAsync(id, influencer);
        if (vm is null)
        {
            return NotFound();
        }

        vm.Step = 3;
        vm.Concept = concept ?? "";
        vm.WhyGoodFit = whyGoodFit ?? "";
        vm.InstagramLink = instagramLink;
        vm.TikTokLink = tikTokLink;
        vm.YouTubeLink = youTubeLink;
        vm.InstagramProfileUrl = instagramProfileUrl;
        vm.FacebookProfileUrl = facebookProfileUrl;
        vm.TikTokProfileUrl = tikTokProfileUrl;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplySocial(CampaignApplyInputModel form)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var vm = await BuildApplyViewModelAsync(form.CampaignId, influencer);
        if (vm is null)
        {
            return NotFound();
        }

        vm.Step = 4;
        vm.Concept = form.Concept ?? "";
        vm.WhyGoodFit = form.WhyGoodFit ?? "";
        vm.InstagramLink = form.InstagramLink;
        vm.TikTokLink = form.TikTokLink;
        vm.YouTubeLink = form.YouTubeLink;
        vm.InstagramProfileUrl = form.InstagramProfileUrl;
        vm.FacebookProfileUrl = form.FacebookProfileUrl;
        vm.TikTokProfileUrl = form.TikTokProfileUrl;
        return View("ApplyReview", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplySubmit(CampaignApplyInputModel form)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaign = await db.Campaigns.Include(c => c.BrandProfile).FirstOrDefaultAsync(c => c.Id == form.CampaignId);
        if (campaign is null)
        {
            return NotFound();
        }

        if (campaign.Status is not (CampaignStatus.Published or CampaignStatus.Active) || campaign.Deadline < DateTime.UtcNow)
        {
            return BadRequest("This campaign is no longer accepting proposals.");
        }

        var alreadyApplied = await db.Proposals.AnyAsync(p => p.CampaignId == form.CampaignId && p.InfluencerProfileId == influencer.Id);
        if (alreadyApplied)
        {
            return RedirectToAction(nameof(ApplicationSubmitted), new { id = form.CampaignId });
        }
        if (!form.Confirmed)
        {
            ModelState.AddModelError(nameof(form.Confirmed), "Please confirm that your application is accurate and complete.");
        }
        if (!ModelState.IsValid)
        {
            var vm = await BuildApplyViewModelAsync(form.CampaignId, influencer);
            vm!.Step = 4;
            vm.Concept = form.Concept ?? "";
            vm.WhyGoodFit = form.WhyGoodFit ?? "";
            vm.InstagramLink = form.InstagramLink;
            vm.TikTokLink = form.TikTokLink;
            vm.YouTubeLink = form.YouTubeLink;
            vm.InstagramProfileUrl = form.InstagramProfileUrl;
            vm.FacebookProfileUrl = form.FacebookProfileUrl;
            vm.TikTokProfileUrl = form.TikTokProfileUrl;
            return View("ApplyReview", vm);
        }
        if (!alreadyApplied)
        {
            var sampleLinks = new[] { form.InstagramLink, form.TikTokLink, form.YouTubeLink }
                .Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            var message = $"Why I'm a good fit: {form.WhyGoodFit}"
                + (sampleLinks.Count > 0 ? "\n\nSample work:\n" + string.Join("\n", sampleLinks) : "");

            var proposal = new Proposal
            {
                CampaignId = form.CampaignId,
                InfluencerProfileId = influencer.Id,
                InitiatedBy = ProposalInitiator.Influencer,
                ProposedAmount = campaign.Budget,
                Deliverables = form.Concept,
                Message = message,
                Status = ProposalStatus.Pending
            };
            db.Proposals.Add(proposal);
            await db.SaveChangesAsync();

            await notifications.NotifyAsync(
                campaign.BrandProfile.UserId,
                "Proposal",
                "New proposal received",
                $"{influencer.FullName} applied to \"{campaign.Title}\".",
                $"/Proposals/Detail/{proposal.Id}");
            await db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(ApplicationSubmitted), new { id = form.CampaignId });
    }

    [HttpGet]
    public async Task<IActionResult> ApplicationSubmitted(int id)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null) return RedirectToAction("Index", "Home");
        var proposal = await db.Proposals.AsNoTracking()
            .Include(p => p.Campaign).ThenInclude(c => c.BrandProfile)
            .Where(p => p.CampaignId == id && p.InfluencerProfileId == influencer.Id)
            .OrderByDescending(p => p.CreatedAt).FirstOrDefaultAsync();
        if (proposal is null) return NotFound();
        var campaign = proposal.Campaign;
        return View(new CampaignApplicationSubmittedViewModel
        {
            Profile = influencer,
            Notifications = await db.Notifications.AsNoTracking().Where(n => n.UserId == influencer.UserId)
                .OrderByDescending(n => n.CreatedAt).Take(5).ToListAsync(),
            CampaignId = campaign.Id, Title = campaign.Title, Description = campaign.Description,
            BrandName = campaign.BrandProfile.CompanyName, BrandLogoUrl = campaign.BrandProfile.ProfilePictureUrl,
            BrandVerified = campaign.BrandProfile.VerificationStatus == VerificationStatus.Verified,
            Niche = campaign.Niche, Platform = campaign.Platform, Budget = campaign.Budget, Deadline = campaign.Deadline,
            ApplicantCount = await db.Proposals.CountAsync(p => p.CampaignId == campaign.Id),
            MilestoneCount = await db.CampaignMilestonePlans.CountAsync(p => p.CampaignId == campaign.Id),
            ProposalId = proposal.Id, SubmittedAt = proposal.CreatedAt, Status = proposal.Status
        });
    }
}
