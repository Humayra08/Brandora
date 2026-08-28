using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Proposals;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class ProposalsController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : BrandControllerBase(userManager, db)
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

        return RedirectToAction("Detail", new { id = proposal.Id });
    }

    public async Task<IActionResult> Index(int? campaignId, ProposalStatus? status)
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var query = db.Proposals.Where(p => p.Campaign.BrandProfileId == brand.Id);

        if (campaignId.HasValue)
        {
            query = query.Where(p => p.CampaignId == campaignId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var proposals = await query
            .Include(p => p.InfluencerProfile)
            .Include(p => p.Campaign)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        Campaign? campaign = null;
        if (campaignId.HasValue)
        {
            campaign = await db.Campaigns.FirstOrDefaultAsync(c => c.Id == campaignId.Value && c.BrandProfileId == brand.Id);
        }

        return View(new ProposalListViewModel { Proposals = proposals, CampaignId = campaignId, Status = status, Campaign = campaign });
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

        return View(proposal);
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
