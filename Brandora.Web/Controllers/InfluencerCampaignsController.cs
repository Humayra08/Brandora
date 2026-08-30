using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class InfluencerCampaignsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : InfluencerControllerBase(userManager, db)
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
            .Where(c => c.Status == CampaignStatus.Published || c.Status == CampaignStatus.Active);

        var myProposals = await db.Proposals
            .Where(p => p.InfluencerProfileId == influencer.Id)
            .ToListAsync();

        var myProposalByCampaign = myProposals
            .GroupBy(p => p.CampaignId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(p => p.CreatedAt).First().Status);

        var myCollaborations = await db.Collaborations
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
            query = query.Where(c => c.Title.Contains(search) || c.Description.Contains(search));
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
            MyProposalStatus = myProposalByCampaign.TryGetValue(c.Id, out var proposalStatus) ? proposalStatus : null,
            IsCollaborating = myActiveCollabCampaignIds.Contains(c.Id),
            IsCollabCompleted = myCompletedCollabCampaignIds.Contains(c.Id)
        }).ToList();

        var selectedTab = string.IsNullOrWhiteSpace(tab) ? "all" : tab;

        var tabFiltered = selectedTab switch
        {
            "applied" => allRows.Where(r => r.MyProposalStatus.HasValue).ToList(),
            "inreview" => allRows.Where(r => r.MyProposalStatus == ProposalStatus.Pending).ToList(),
            "ongoing" => allRows.Where(r => r.IsCollaborating).ToList(),
            "completed" => allRows.Where(r => r.IsCollabCompleted).ToList(),
            _ => allRows
        };

        var totalFiltered = tabFiltered.Count;
        var pageSize = 6;
        var pageNumber = Math.Max(1, page);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(int campaignId, decimal proposedAmount, string deliverables, string? message)
    {
        var influencer = await GetCurrentInfluencerAsync();
        if (influencer is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId);
        if (campaign is null)
        {
            return NotFound();
        }

        var alreadyApplied = await db.Proposals.AnyAsync(p => p.CampaignId == campaignId && p.InfluencerProfileId == influencer.Id);
        if (!alreadyApplied && !string.IsNullOrWhiteSpace(deliverables))
        {
            db.Proposals.Add(new Proposal
            {
                CampaignId = campaignId,
                InfluencerProfileId = influencer.Id,
                InitiatedBy = ProposalInitiator.Influencer,
                ProposedAmount = proposedAmount > 0 ? proposedAmount : campaign.Budget,
                Deliverables = deliverables,
                Message = message,
                Status = ProposalStatus.Pending
            });

            await db.SaveChangesAsync();
        }

        return RedirectToAction("Index");
    }
}
