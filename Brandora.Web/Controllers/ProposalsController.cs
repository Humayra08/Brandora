using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Proposals;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class ProposalsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db, NotificationService notifications) : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Invite(int influencerId, int? campaignId)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var creator = await db.InfluencerProfiles.FirstOrDefaultAsync(i => i.Id == influencerId);
        if (creator is null)
        {
            return NotFound();
        }

        var campaigns = await InvitableCampaignsAsync(brand.Id);

        if (campaigns.Count == 0)
        {
            TempData["InviteError"] = "Create and publish a campaign before inviting creators.";
            return RedirectToAction("Profile", "Influencers", new { id = influencerId });
        }

        var vm = new InviteFormViewModel
        {
            InfluencerProfileId = influencerId,
            Creator = creator,
            AvailableCampaigns = campaigns,
            CampaignId = campaignId is not null && campaigns.Any(c => c.Id == campaignId) ? campaignId.Value : campaigns[0].Id
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(InviteFormViewModel model)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var creator = await db.InfluencerProfiles.FirstOrDefaultAsync(i => i.Id == model.InfluencerProfileId);
        var campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == model.CampaignId && c.BrandProfileId == brand.Id);

        if (creator is null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid || campaign is null)
        {
            if (campaign is null)
            {
                ModelState.AddModelError(string.Empty, "Select a valid campaign for this invite.");
            }

            model.Creator = creator;
            model.AvailableCampaigns = await InvitableCampaignsAsync(brand.Id);
            return View(model);
        }

        var proposal = new Proposal
        {
            CampaignId = campaign.Id,
            InfluencerProfileId = creator.Id,
            InitiatedBy = ProposalInitiator.Brand,
            ProposedAmount = model.ProposedAmount,
            Deliverables = model.Deliverables,
            Timeline = model.Timeline,
            Message = model.Message,
            Status = ProposalStatus.Pending
        };

        db.Proposals.Add(proposal);
        await db.SaveChangesAsync();

        await notifications.NotifyAsync(
            creator.UserId,
            "Proposal",
            "New campaign invite",
            $"{brand.CompanyName} invited you to \"{campaign.Title}\" for ৳{model.ProposedAmount:N0}.",
            $"/InfluencerProposals/Detail/{proposal.Id}");
        await db.SaveChangesAsync();

        return RedirectToAction("Detail", new { id = proposal.Id });
    }

    public async Task<IActionResult> Index(int? campaignId, ProposalStatus? status, string? search, DateTime? from, DateTime? to, int page = 1, int pageSize = 6)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var baseQuery = db.Proposals.Where(p => p.Campaign.BrandProfileId == brand.Id);

        var counts = await baseQuery
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var query = baseQuery;

        if (campaignId.HasValue)
        {
            query = query.Where(p => p.CampaignId == campaignId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.InfluencerProfile.FullName.Contains(search));
        }

        if (from.HasValue)
        {
            var fromUtc = DateTime.SpecifyKind(from.Value.Date, DateTimeKind.Utc);
            query = query.Where(p => p.CreatedAt >= fromUtc);
        }

        if (to.HasValue)
        {
            var toUtc = DateTime.SpecifyKind(to.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(p => p.CreatedAt < toUtc);
        }

        var totalCount = await query.CountAsync();

        page = Math.Max(1, page);
        pageSize = pageSize is 6 or 10 or 25 or 50 ? pageSize : 6;

        var proposals = await query
            .Include(p => p.InfluencerProfile)
            .Include(p => p.Campaign)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Campaign? campaign = null;
        if (campaignId.HasValue)
        {
            campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId.Value && c.BrandProfileId == brand.Id);
        }

        var availableCampaigns = await db.Campaigns
            .Where(c => c.BrandProfileId == brand.Id)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return View(new ProposalListViewModel
        {
            Proposals = proposals,
            CampaignId = campaignId,
            Status = status,
            Campaign = campaign,
            Search = search,
            From = from,
            To = to,
            AvailableCampaigns = availableCampaigns,
            AllCount = counts.Sum(c => c.Count),
            PendingCount = counts.FirstOrDefault(c => c.Status == ProposalStatus.Pending)?.Count ?? 0,
            AcceptedCount = counts.FirstOrDefault(c => c.Status == ProposalStatus.Accepted)?.Count ?? 0,
            RejectedCount = counts.FirstOrDefault(c => c.Status == ProposalStatus.Rejected)?.Count ?? 0,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        });
    }

    public async Task<IActionResult> Detail(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var proposal = await db.Proposals
            .Include(p => p.InfluencerProfile)
            .Include(p => p.Campaign)
            .Include(p => p.Collaboration)
            .FirstOrDefaultAsync(p => p.Id == id && p.Campaign.BrandProfileId == brand.Id);

        if (proposal is null)
        {
            return NotFound();
        }

        var milestonePlanCount = await db.CampaignMilestonePlans.CountAsync(p => p.CampaignId == proposal.CampaignId);

        var pastCollaborations = await db.Collaborations
            .Include(c => c.Campaign)
            .Where(c => c.InfluencerProfileId == proposal.InfluencerProfileId
                        && c.Campaign.BrandProfileId == brand.Id
                        && c.ProposalId != proposal.Id)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

        return View(new ProposalDetailViewModel
        {
            Proposal = proposal,
            CampaignMilestonePlanCount = milestonePlanCount,
            PastCollaborations = pastCollaborations
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmAccept(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var proposal = await db.Proposals
            .Include(p => p.Campaign)
            .Include(p => p.InfluencerProfile)
            .FirstOrDefaultAsync(p => p.Id == id && p.Campaign.BrandProfileId == brand.Id);

        if (proposal is null)
        {
            return NotFound();
        }

        if (proposal.Status != ProposalStatus.Pending)
        {
            return RedirectToAction("Detail", new { id });
        }

        proposal.Status = ProposalStatus.Accepted;
        proposal.DecidedAt = DateTime.UtcNow;

        var collaboration = new Collaboration
        {
            ProposalId = proposal.Id,
            CampaignId = proposal.CampaignId,
            InfluencerProfileId = proposal.InfluencerProfileId,
            Status = CollaborationStatus.Active
        };
        db.Collaborations.Add(collaboration);

        var existingConversation = await db.Conversations.FirstOrDefaultAsync(c =>
            c.BrandProfileId == brand.Id && c.InfluencerProfileId == proposal.InfluencerProfileId && c.CampaignId == proposal.CampaignId);

        if (existingConversation is null)
        {
            db.Conversations.Add(new Conversation
            {
                BrandProfileId = brand.Id,
                InfluencerProfileId = proposal.InfluencerProfileId,
                CampaignId = proposal.CampaignId
            });
        }

        // Turn the campaign's milestone plan (drafted in the campaign wizard,
        // Step 2) into this creator's real, trackable milestones. The plan
        // itself stays untouched so it can be reused if other creators join
        // the same campaign.
        var milestonePlans = await db.CampaignMilestonePlans
            .Where(p => p.CampaignId == proposal.CampaignId)
            .OrderBy(p => p.SortOrder).ThenBy(p => p.Id)
            .ToListAsync();

        foreach (var plan in milestonePlans)
        {
            db.Milestones.Add(new Milestone
            {
                Collaboration = collaboration,
                Title = plan.Title,
                ContentType = plan.ContentType,
                Description = plan.Description,
                Amount = plan.Amount,
                DueDate = plan.DueDate,
                Status = MilestoneStatus.Pending
            });
        }

        await db.SaveChangesAsync();

        await notifications.NotifyAsync(
            proposal.InfluencerProfile.UserId,
            "Collaboration",
            "Collaboration started",
            $"Your proposal for \"{proposal.Campaign.Title}\" was accepted — the collaboration is now active.",
            $"/InfluencerCampaigns/Details/{proposal.CampaignId}");
        await db.SaveChangesAsync();

        return RedirectToAction("Detail", "Collaborations", new { id = collaboration.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var proposal = await db.Proposals
            .Include(p => p.Campaign)
            .FirstOrDefaultAsync(p => p.Id == id && p.Campaign.BrandProfileId == brand.Id);

        if (proposal is null)
        {
            return NotFound();
        }

        if (proposal.Status == ProposalStatus.Pending)
        {
            proposal.Status = ProposalStatus.Rejected;
            proposal.DecidedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }

        return RedirectToAction("Detail", new { id });
    }

    private async Task<List<Campaign>> InvitableCampaignsAsync(int brandId)
    {
        return await db.Campaigns
            .Where(c => c.BrandProfileId == brandId
                        && c.Status != CampaignStatus.Cancelled
                        && c.Status != CampaignStatus.Completed)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }
}
