using Brandora.Web.Data;
using Brandora.Web.Models.Dashboard;
using Brandora.Web.Models.Domain;
using Brandora.Web.Models.Influencers;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Controllers;

public class DashboardController(UserManager<ApplicationUser> userManager, ApplicationDbContext db) : BrandControllerBase(userManager, db)
{
    public async Task<IActionResult> Index()
    {
        var brand = await GetCurrentBrandAsync();
        if (brand is null)
        {
            return RedirectToAction("Index", "Home");
        }

        var userId = userManager.GetUserId(User)!;
        var today = DateTime.UtcNow.Date;

        var campaigns = await db.Campaigns
            .Where(c => c.BrandProfileId == brand.Id)
            .ToListAsync();

        var proposals = await db.Proposals
            .Where(p => p.Campaign.BrandProfileId == brand.Id)
            .Include(p => p.InfluencerProfile)
            .Include(p => p.Campaign)
            .ToListAsync();

        var collaborations = await db.Collaborations
            .Where(c => c.Campaign.BrandProfileId == brand.Id)
            .Include(c => c.InfluencerProfile)
            .Include(c => c.Campaign)
            .Include(c => c.Milestones)
            .ToListAsync();

        var payments = await db.Payments
            .Where(p => p.Collaboration.Campaign.BrandProfileId == brand.Id)
            .Include(p => p.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .ToListAsync();

        var shortlistedIds = (await db.ShortlistEntries
            .Where(s => s.BrandProfileId == brand.Id)
            .Select(s => s.InfluencerProfileId)
            .ToListAsync()).ToHashSet();

        var creatorPreview = await db.InfluencerProfiles
            .Where(i => !shortlistedIds.Contains(i.Id))
            .OrderByDescending(i => i.CreatedAt)
            .Take(4)
            .ToListAsync();

        var recentActivity = await db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(6)
            .ToListAsync();

        // ---- Campaign workspace: recent campaigns with real applicant/collaboration/progress data ----

        var campaignWorkspace = campaigns
            .OrderBy(c => c.Status == CampaignStatus.Completed || c.Status == CampaignStatus.Cancelled ? 1 : 0)
            .ThenByDescending(c => c.CreatedAt)
            .Take(4)
            .Select(c =>
            {
                var campaignCollaborations = collaborations.Where(cl => cl.CampaignId == c.Id).ToList();
                var milestones = campaignCollaborations.SelectMany(cl => cl.Milestones).ToList();

                var progress = milestones.Count > 0
                    ? (int)Math.Round(milestones.Count(m => m.Status == MilestoneStatus.Paid) * 100.0 / milestones.Count)
                    : c.Budget > 0
                        ? (int)Math.Round(Math.Min(100, (double)(c.SpentAmount / c.Budget * 100)))
                        : 0;

                return new CampaignSummaryItem
                {
                    Campaign = c,
                    ApplicantCount = proposals.Count(p => p.CampaignId == c.Id),
                    CollaborationCount = campaignCollaborations.Count,
                    CreatorNames = campaignCollaborations.Select(cl => cl.InfluencerProfile.FullName).Distinct().ToList(),
                    ProgressPercent = progress
                };
            })
            .ToList();

        // ---- What needs your attention ----

        var attentionItems = new List<AttentionItem>();

        foreach (var p in proposals.Where(p => p.Status == ProposalStatus.Pending).OrderBy(p => p.CreatedAt).Take(3))
        {
            attentionItems.Add(new AttentionItem
            {
                Kind = "Proposal",
                Title = $"Confirm {p.InfluencerProfile.FullName}'s response",
                Detail = $"Invited to \"{p.Campaign.Title}\" · awaiting confirmation",
                LinkUrl = $"/Proposals/Detail/{p.Id}",
                ActionLabel = "Review"
            });
        }

        var submittedMilestones = collaborations
            .SelectMany(cl => cl.Milestones.Where(m => m.Status == MilestoneStatus.Submitted).Select(m => (Collab: cl, Milestone: m)))
            .OrderBy(x => x.Milestone.DueDate ?? DateTime.MaxValue)
            .Take(3);

        foreach (var (collab, milestone) in submittedMilestones)
        {
            attentionItems.Add(new AttentionItem
            {
                Kind = "Milestone",
                Title = $"Review proof for \"{milestone.Title}\"",
                Detail = $"Submitted by {collab.InfluencerProfile.FullName} · {collab.Campaign.Title}",
                LinkUrl = $"/Milestones/Detail/{milestone.Id}",
                ActionLabel = "Review Proof"
            });
        }

        foreach (var payment in payments.Where(p => p.Status == PaymentStatus.Pending).OrderBy(p => p.CreatedAt).Take(2))
        {
            attentionItems.Add(new AttentionItem
            {
                Kind = "Payment",
                Title = $"Confirm payment to {payment.Collaboration.InfluencerProfile.FullName}",
                Detail = $"৳{payment.Amount:N0} · transfer initiated",
                LinkUrl = "/Payments",
                ActionLabel = "Confirm"
            });
        }

        // ---- Collaboration activity ----

        var collaborationActivity = collaborations
            .Where(c => c.Status == CollaborationStatus.Active)
            .Select(c =>
            {
                var nextDue = c.Milestones
                    .Where(m => m.Status != MilestoneStatus.Paid && m.DueDate.HasValue)
                    .OrderBy(m => m.DueDate)
                    .Select(m => m.DueDate)
                    .FirstOrDefault();

                return new CollaborationSummaryItem
                {
                    Collaboration = c,
                    TotalMilestones = c.Milestones.Count,
                    PaidMilestones = c.Milestones.Count(m => m.Status == MilestoneStatus.Paid),
                    NextDueDate = nextDue,
                    NeedsAttention = c.Milestones.Any(m => m.Status == MilestoneStatus.Submitted)
                                      || (nextDue.HasValue && nextDue.Value.Date < today)
                };
            })
            .OrderByDescending(c => c.NeedsAttention)
            .ThenBy(c => c.NextDueDate ?? DateTime.MaxValue)
            .Take(4)
            .ToList();

        // ---- Upcoming ----

        var upcoming = new List<UpcomingItem>();

        upcoming.AddRange(campaigns
            .Where(c => c.Deadline.HasValue && c.Deadline.Value.Date >= today
                        && c.Status != CampaignStatus.Completed && c.Status != CampaignStatus.Cancelled)
            .Select(c => new UpcomingItem
            {
                Kind = "Campaign deadline",
                Title = c.Title,
                Date = c.Deadline!.Value,
                LinkUrl = $"/Campaigns/Detail/{c.Id}"
            }));

        upcoming.AddRange(collaborations
            .SelectMany(c => c.Milestones.Where(m => m.DueDate.HasValue && m.DueDate.Value.Date >= today
                                                       && m.Status != MilestoneStatus.Paid)
                .Select(m => new UpcomingItem
                {
                    Kind = "Milestone due",
                    Title = $"{m.Title} · {c.InfluencerProfile.FullName}",
                    Date = m.DueDate!.Value,
                    LinkUrl = $"/Milestones/Detail/{m.Id}"
                })));

        var upcomingItems = upcoming.OrderBy(u => u.Date).Take(5).ToList();

        // ---- Smart Match preview (one relevant campaign, real scoring) ----

        var smartMatchCampaign = campaigns
            .Where(c => (c.Status == CampaignStatus.Published || c.Status == CampaignStatus.Active)
                        && (!string.IsNullOrEmpty(c.Platform) || !string.IsNullOrEmpty(c.Niche)))
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefault();

        var smartMatchPreview = new List<SmartMatchResult>();
        if (smartMatchCampaign is not null)
        {
            var allCreators = await db.InfluencerProfiles.ToListAsync();
            smartMatchPreview = allCreators
                .Select(creator =>
                {
                    var (score, reasons) = SmartMatchScorer.ComputeMatch(creator, smartMatchCampaign);
                    return new SmartMatchResult
                    {
                        Creator = creator,
                        Score = score,
                        Reasons = reasons,
                        IsShortlisted = shortlistedIds.Contains(creator.Id)
                    };
                })
                .Where(r => r.Score > 0)
                .OrderByDescending(r => r.Score)
                .Take(3)
                .ToList();
        }

        // ---- Spend by month (last 6 months, from completed payments) ----

        var completedPayments = payments.Where(p => p.Status == PaymentStatus.Completed && p.PaidAt.HasValue).ToList();
        var months = Enumerable.Range(0, 6)
            .Select(i => DateTime.UtcNow.AddMonths(-i))
            .Reverse()
            .Select(d => new { d.Year, d.Month, Label = d.ToString("MMM") })
            .ToList();

        var spendByMonth = months.Select(m => new MonthlySpendPoint
        {
            Month = m.Label,
            Amount = completedPayments
                .Where(p => p.PaidAt!.Value.Year == m.Year && p.PaidAt.Value.Month == m.Month)
                .Sum(p => p.Amount)
        }).ToList();

        var vm = new DashboardViewModel
        {
            CompanyName = brand.CompanyName,
            DraftCount = campaigns.Count(c => c.Status == CampaignStatus.Draft),
            PublishedCount = campaigns.Count(c => c.Status == CampaignStatus.Published),
            ActiveCount = campaigns.Count(c => c.Status == CampaignStatus.Active),
            CompletedCount = campaigns.Count(c => c.Status == CampaignStatus.Completed),
            TotalBudget = campaigns.Sum(c => c.Budget),
            TotalSpend = campaigns.Sum(c => c.SpentAmount),
            PendingProposalCount = proposals.Count(p => p.Status == ProposalStatus.Pending),
            ShortlistedCount = shortlistedIds.Count,
            ActiveCollaborationCount = collaborations.Count(c => c.Status == CollaborationStatus.Active),
            PendingPaymentsAmount = payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount),
            CompletedPaymentsAmount = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount),
            RecentCampaigns = campaigns.OrderByDescending(c => c.CreatedAt).Take(5).ToList(),
            CampaignWorkspace = campaignWorkspace,
            AttentionItems = attentionItems,
            CreatorPreview = creatorPreview,
            ShortlistedIds = shortlistedIds,
            SmartMatchCampaign = smartMatchCampaign,
            SmartMatchPreview = smartMatchPreview,
            CollaborationActivity = collaborationActivity,
            UpcomingItems = upcomingItems,
            RecentActivity = recentActivity,
            SpendByMonth = spendByMonth
        };

        return View(vm);
    }
}
