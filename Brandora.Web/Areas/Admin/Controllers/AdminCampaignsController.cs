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

    public async Task<IActionResult> Details(int id)
    {
        var campaign = await db.Campaigns
            .Include(c => c.BrandProfile)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign is null) return NotFound();

        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Campaigns";
        ViewData["Title"] = campaign.Title;
        ViewData["Breadcrumb"] = new List<(string, string?)>
        {
            ("Campaigns", "/Admin/AdminCampaigns/Index"),
            (campaign.Title, null)
        };

        var proposals = await db.Proposals
            .Include(p => p.InfluencerProfile)
            .Where(p => p.CampaignId == id)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        var collaborations = await db.Collaborations
            .Include(c => c.InfluencerProfile)
            .Include(c => c.Milestones).ThenInclude(m => m.Payment)
            .Where(c => c.CampaignId == id)
            .ToListAsync();

        var disputes = await db.Disputes
            .Include(d => d.Milestone)
            .Include(d => d.InfluencerProfile)
            .Where(d => d.Collaboration.CampaignId == id)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var milestones = collaborations
            .SelectMany(c => c.Milestones.Select(m => new CampaignMilestoneRow(
                m.Id,
                m.Title,
                m.ContentType,
                c.InfluencerProfile.FullName,
                m.DueDate,
                m.Amount,
                m.Status,
                PaymentStateOf(m),
                m.ProofUrl,
                m.ProofNotes)))
            .OrderBy(m => m.DueDate ?? DateTime.MaxValue)
            .ToList();

        var payments = collaborations.SelectMany(c => c.Payments).ToList();
        var released = payments.Where(p => p.Status == PaymentStatus.Completed).Sum(p => p.Amount);
        var pending = payments.Where(p => p.Status == PaymentStatus.Pending).Sum(p => p.Amount);
        var locked = milestones.Where(m => m.PaymentState == "Locked" || m.PaymentState == "Available").Sum(m => m.Amount);

        var spendPoints = payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .OrderBy(p => p.PaidAt ?? p.CreatedAt)
            .Select(p => (At: p.PaidAt ?? p.CreatedAt, Amount: p.Amount))
            .ToList();

        var activity = new List<CampaignActivityRow> { new(campaign.CreatedAt, "Campaign created", campaign.Status.ToString()) };
        activity.AddRange(proposals.Select(p => new CampaignActivityRow(p.CreatedAt, p.InfluencerProfile.FullName + " applied", "Proposed ৳" + p.ProposedAmount.ToString("N0"))));
        activity.AddRange(proposals.Where(p => p.Status == ProposalStatus.Accepted && p.DecidedAt.HasValue)
            .Select(p => new CampaignActivityRow(p.DecidedAt!.Value, "Proposal accepted", p.InfluencerProfile.FullName)));
        activity.AddRange(collaborations.SelectMany(c => c.Milestones.Select(m => new CampaignActivityRow(m.CreatedAt, "Milestone created", m.Title))));
        activity.AddRange(payments.Where(p => p.Status == PaymentStatus.Completed)
            .Select(p => new CampaignActivityRow(p.PaidAt ?? p.CreatedAt, "Payment released", "৳" + p.Amount.ToString("N0"))));
        activity.AddRange(disputes.Select(d => new CampaignActivityRow(d.CreatedAt, "Dispute opened", d.Reason)));

        var vm = new AdminCampaignDetailsViewModel
        {
            Campaign = campaign,
            Proposals = proposals,
            Milestones = milestones,
            Disputes = disputes,
            AcceptedCount = proposals.Count(p => p.Status == ProposalStatus.Accepted),
            CompletedCollaborations = collaborations.Count(c => c.Status == CollaborationStatus.Completed),
            PaymentsReleased = released,
            PaymentsPending = pending,
            PaymentsLocked = locked,
            SpendPoints = spendPoints.Select(s => new SpendPoint(s.At, s.Amount)).ToList(),
            Activity = activity.OrderByDescending(a => a.At).Take(30).ToList()
        };

        return View(vm);
    }

    // PLACEHOLDER — payment gate. Today a payment is "Available" once the milestone is Approved.
    // When the admin-approval / gateway fields from the payment work are merged, change ONLY
    // this method (e.g. Available = admin approved and no payment yet).
    private static string PaymentStateOf(Milestone m)
    {
        if (m.Payment is { Status: PaymentStatus.Completed }) return "Released";
        if (m.Payment is { Status: PaymentStatus.Pending }) return "Pending";
        return m.Status == MilestoneStatus.Approved ? "Available" : "Locked";
    }
}

public record CampaignMilestoneRow(
    int Id,
    string Title,
    string? ContentType,
    string InfluencerName,
    DateTime? DueDate,
    decimal Amount,
    MilestoneStatus Status,
    string PaymentState,
    string? ProofUrl,
    string? ProofNotes);

public record CampaignActivityRow(DateTime At, string Title, string Detail);

public record SpendPoint(DateTime At, decimal Amount);

public class AdminCampaignDetailsViewModel
{
    public Campaign Campaign { get; set; } = null!;
    public List<Proposal> Proposals { get; set; } = new();
    public List<CampaignMilestoneRow> Milestones { get; set; } = new();
    public List<Dispute> Disputes { get; set; } = new();
    public int AcceptedCount { get; set; }
    public int CompletedCollaborations { get; set; }
    public decimal PaymentsReleased { get; set; }
    public decimal PaymentsPending { get; set; }
    public decimal PaymentsLocked { get; set; }
    public List<SpendPoint> SpendPoints { get; set; } = new();
    public List<CampaignActivityRow> Activity { get; set; } = new();
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
