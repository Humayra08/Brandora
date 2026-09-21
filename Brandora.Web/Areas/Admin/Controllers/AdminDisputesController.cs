using Brandora.Web.Areas.Admin.Services;
using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Brandora.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

// The Dispute Resolution pages use ONLY the dispute fields that already exist (reason, status,
// resolution note, dates, parties, milestone, payment, conversation). Things the database does not
// store yet — who raised it, each side's statement and evidence, internal notes, assignment — are
// shown as clear placeholders on real disputes, and as full sample data in the design preview:
//   /Admin/AdminDisputes/Index?preview=true
//   /Admin/AdminDisputes/Details?preview=true
// When the Brand/Influencer "report a dispute" pages are built, those fields can be added and
// filled in from the mapping in MapReal below.
public class AdminDisputesController(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    NotificationService notifications) : AdminControllerBase(db)
{
    private static string Code(Dispute d) => $"DS-{d.CreatedAt.Year}-{d.Id:D3}";

    private static string StatusLabel(DisputeStatus s) => s switch
    {
        DisputeStatus.UnderReview => "Under Review",
        DisputeStatus.Resolved => "Resolved",
        _ => "Open"
    };

    private static decimal? Trend(int now, int before) =>
        before > 0 ? Math.Round((decimal)(now - before) / before * 100, 0) : null;

    // ---------------------------------------------------------------- list

    public async Task<IActionResult> Index(bool preview = false)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Disputes";
        ViewData["Title"] = "Dispute Resolution";
        ViewData["HasCustomHero"] = true;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dispute Resolution", null) };

        List<DisputeRow> rows;
        var trendTotal = (decimal?)null;
        var trendResolved = (decimal?)null;

        if (preview)
        {
            rows = SampleRows();
        }
        else
        {
            var disputes = await db.Disputes
                .Include(d => d.BrandProfile)
                .Include(d => d.InfluencerProfile)
                .Include(d => d.Collaboration).ThenInclude(c => c.Campaign)
                .Include(d => d.Milestone)
                .OrderByDescending(d => d.CreatedAt)
                .ToListAsync();

            rows = disputes.Select(d => new DisputeRow(
                d.Id,
                Code(d),
                d.Collaboration.CampaignId,
                d.Collaboration.Campaign.Title,
                d.Milestone?.Title ?? "—",
                d.Milestone?.ContentType,
                d.BrandProfile.CompanyName,
                d.BrandProfile.ProfilePictureUrl,
                d.InfluencerProfile.FullName,
                d.InfluencerProfile.PlatformUsername,
                d.Reason,
                d.Milestone?.Amount ?? 0m,
                "—",
                d.CreatedAt,
                StatusLabel(d.Status))).ToList();

            var now = DateTime.UtcNow;
            var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var lastMonthStart = monthStart.AddMonths(-1);
            trendTotal = Trend(
                disputes.Count(d => d.CreatedAt >= monthStart),
                disputes.Count(d => d.CreatedAt >= lastMonthStart && d.CreatedAt < monthStart));
            trendResolved = Trend(
                disputes.Count(d => d.ResolvedAt >= monthStart),
                disputes.Count(d => d.ResolvedAt >= lastMonthStart && d.ResolvedAt < monthStart));
        }

        return View(new DisputeIndexViewModel
        {
            IsPreview = preview,
            Rows = rows,
            Total = rows.Count,
            OpenCount = rows.Count(r => r.Status == "Open"),
            UnderReviewCount = rows.Count(r => r.Status == "Under Review"),
            ResolvedCount = rows.Count(r => r.Status == "Resolved"),
            AmountInDispute = rows.Where(r => r.Status != "Resolved").Sum(r => r.Amount),
            TotalTrend = trendTotal,
            ResolvedTrend = trendResolved,
            Campaigns = rows.Select(r => r.CampaignTitle).Distinct().OrderBy(t => t).ToList()
        });
    }

    // ------------------------------------------------------------- details

    // preview=true&sides=both|brand|influencer shows the design with sample data. "brand" means only
    // the brand has given a statement (the influencer box says "not given yet"), and vice versa.
    public async Task<IActionResult> Details(int id, bool preview = false, string? sides = null)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Disputes";
        ViewData["HasCustomHero"] = true;

        var snap = await PlatformWalletData.LoadAsync(db);
        var walletBalance = PlatformWalletData.Balance(snap);

        DisputeDetailsVm vm;
        if (preview)
        {
            vm = SampleDetails(sides);
        }
        else
        {
            var d = await db.Disputes
                .Include(x => x.BrandProfile).ThenInclude(b => b.User)
                .Include(x => x.InfluencerProfile).ThenInclude(i => i.User)
                .Include(x => x.Collaboration).ThenInclude(c => c.Campaign)
                .Include(x => x.Milestone)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (d is null) return NotFound();
            vm = await MapReal(d);
        }

        vm.WalletBalance = walletBalance;
        vm.AdminName = AdminName;

        ViewData["Title"] = "Dispute " + vm.Code;
        ViewData["Breadcrumb"] = new List<(string, string?)>
        {
            ("Dispute Resolution", "/Admin/AdminDisputes/Index" + (preview ? "?preview=true" : "")),
            (vm.Code, null)
        };

        return View(vm);
    }

    private async Task<DisputeDetailsVm> MapReal(Dispute d)
    {
        var camp = d.Collaboration.Campaign;
        var ms = d.Milestone;

        var payment = d.MilestoneId is not null
            ? await db.Payments.FirstOrDefaultAsync(p => p.MilestoneId == d.MilestoneId)
            : null;

        var conversations = await db.Conversations
            .Include(c => c.Messages)
            .Where(c => c.BrandProfileId == d.BrandProfileId && c.InfluencerProfileId == d.InfluencerProfileId)
            .ToListAsync();

        var messages = conversations
            .SelectMany(c => c.Messages)
            .OrderBy(m => m.SentAt)
            .Select(m =>
            {
                var fromBrand = m.SenderUserId == d.BrandProfile.UserId;
                return new DisputeMessageRow(
                    fromBrand ? "Brand" : "Influencer",
                    fromBrand ? d.BrandProfile.CompanyName : d.InfluencerProfile.FullName,
                    m.Body, m.SentAt, m.MediaUrl);
            })
            .ToList();

        var resolved = d.Status == DisputeStatus.Resolved;
        var timeline = new List<DisputeTimelineStep>
        {
            new("Dispute raised", d.CreatedAt, "done"),
            new("Under review", null, d.Status == DisputeStatus.Open ? "pending" : "done"),
            resolved
                ? new DisputeTimelineStep("Resolved", d.ResolvedAt, "done")
                : new DisputeTimelineStep("Waiting for resolution", null, "current")
        };

        return new DisputeDetailsVm
        {
            Id = d.Id,
            Code = Code(d),
            StatusLabel = StatusLabel(d.Status),
            StatusTone = resolved ? "green" : d.Status == DisputeStatus.UnderReview ? "amber" : "red",
            IsResolved = resolved,
            IsOpen = d.Status == DisputeStatus.Open,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.ResolvedAt ?? d.CreatedAt,
            RaisedBy = "",
            Reason = d.Reason,

            BrandName = d.BrandProfile.CompanyName,
            BrandPicture = d.BrandProfile.ProfilePictureUrl,
            BrandEmail = d.BrandProfile.User.Email ?? "",
            BrandVerified = d.BrandProfile.VerificationStatus == VerificationStatus.Verified,
            BrandProfileId = d.BrandProfileId,
            BrandCampaigns = await db.Campaigns.CountAsync(c => c.BrandProfileId == d.BrandProfileId),
            BrandPastDisputes = await db.Disputes.CountAsync(x => x.BrandProfileId == d.BrandProfileId && x.Id != d.Id),

            InfluencerName = d.InfluencerProfile.FullName,
            InfluencerHandle = d.InfluencerProfile.PlatformUsername,
            InfluencerEmail = d.InfluencerProfile.User.Email ?? "",
            InfluencerVerified = d.InfluencerProfile.VerificationStatus == VerificationStatus.Verified,
            InfluencerProfileId = d.InfluencerProfileId,
            InfluencerCollaborations = await db.Collaborations.CountAsync(c => c.InfluencerProfileId == d.InfluencerProfileId),
            InfluencerPastDisputes = await db.Disputes.CountAsync(x => x.InfluencerProfileId == d.InfluencerProfileId && x.Id != d.Id),

            CampaignId = camp.Id,
            CampaignTitle = camp.Title,
            CampaignCode = $"CAM-{camp.CreatedAt.Year}-{camp.Id:D3}",
            CampaignMedia = camp.MediaUrl,
            MilestoneId = ms?.Id,
            MilestoneTitle = ms?.Title,
            MilestoneType = ms?.ContentType,
            Amount = ms?.Amount ?? payment?.Amount ?? 0m,

            ProofUrl = ms?.ProofUrl,
            ProofNotes = ms?.ProofNotes,
            ProofStatus = ms is null ? null
                : ms.Status is MilestoneStatus.Approved or MilestoneStatus.Paid ? "Approved by Admin"
                : ms.Status == MilestoneStatus.Submitted ? "Waiting for review"
                : ms.Status.ToString(),
            ProofTone = ms is not null && ms.Status is MilestoneStatus.Approved or MilestoneStatus.Paid ? "green"
                : ms?.Status == MilestoneStatus.RevisionRequested ? "red" : "amber",

            Messages = messages,
            Timeline = timeline,
            Payment = payment,
            CanRelease = payment is not null && payment.Status != PaymentStatus.Failed,
            CanRefund = payment is not null && payment.Status != PaymentStatus.Completed,
            ResolutionNote = d.ResolutionNotes,
            ResolvedAt = d.ResolvedAt,
            HasSchemaExtras = false
        };
    }

    // --------------------------------------------------------- real actions

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartReview(int id)
    {
        var d = await db.Disputes.FirstOrDefaultAsync(x => x.Id == id);
        if (d is null) return NotFound();

        if (d.Status == DisputeStatus.Resolved)
        {
            TempData["DisputeError"] = "This dispute is already resolved.";
            return RedirectToAction(nameof(Details), new { id });
        }

        d.Status = DisputeStatus.UnderReview;
        await db.SaveChangesAsync();

        TempData["DisputeMessage"] = "The dispute is now under review.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, string? outcome, string? notes, string? compensationTo, decimal? compensationAmount)
    {
        var d = await db.Disputes
            .Include(x => x.InfluencerProfile)
            .Include(x => x.BrandProfile)
            .Include(x => x.Milestone)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (d is null) return NotFound();

        IActionResult Fail(string message)
        {
            TempData["DisputeError"] = message;
            return RedirectToAction(nameof(Details), new { id });
        }

        if (d.Status == DisputeStatus.Resolved) return Fail("This dispute is already resolved.");

        var note = (notes ?? "").Trim();
        if (note.Length == 0) return Fail("A resolution note is required.");

        var allowed = new[] { "ReleasePayment", "RefundBrand", "Compensation", "NoAction" };
        if (outcome is null || !allowed.Contains(outcome)) return Fail("Choose how to resolve this dispute.");

        var recipient = string.Equals(compensationTo, "Influencer", StringComparison.OrdinalIgnoreCase) ? "influencer" : "brand";
        if (outcome == "Compensation")
        {
            if (compensationTo is not ("Brand" or "Influencer")) return Fail("Choose who receives the compensation.");
            if (compensationAmount is null or <= 0) return Fail("Enter a compensation amount greater than ৳0.");
        }

        var payment = d.MilestoneId is not null
            ? await db.Payments
                .Include(p => p.Collaboration).ThenInclude(c => c.Campaign)
                .FirstOrDefaultAsync(p => p.MilestoneId == d.MilestoneId)
            : null;

        // PLACEHOLDER — payment movement. Uses the payment fields that exist today. When the payment
        // gateway / wallet work is merged, replace the two blocks below with the real release /
        // refund calls; the rest of this action does not need to change.
        if (outcome == "ReleasePayment")
        {
            if (payment is null) return Fail("No payment exists for this milestone yet, so there is nothing to release.");
            if (payment.Status == PaymentStatus.Failed) return Fail("This payment already failed or was refunded and cannot be released.");

            if (payment.Status == PaymentStatus.Pending)
            {
                payment.Status = PaymentStatus.Completed;
                payment.PaidAt = DateTime.UtcNow;
                payment.EscrowStatus = EscrowStatus.Released;
                payment.Collaboration.Campaign.SpentAmount += payment.Amount;
                if (d.Milestone is not null) d.Milestone.Status = MilestoneStatus.Paid;
            }
        }
        else if (outcome == "RefundBrand")
        {
            if (payment is null) return Fail("No payment exists for this milestone, so there is nothing to refund.");
            if (payment.Status == PaymentStatus.Completed) return Fail("This payment was already completed. Use compensation instead of a refund.");

            payment.Status = PaymentStatus.Failed;
            payment.EscrowStatus = EscrowStatus.Refunded;
        }

        // The decision has no column of its own yet, so it is written at the start of the note.
        var summary = outcome switch
        {
            "ReleasePayment" => "Payment released to the influencer",
            "RefundBrand" => "Payment refunded to the brand",
            "Compensation" => $"Compensation of ৳{compensationAmount:N0} awarded to the {recipient} (credited to their wallet once wallet payouts go live)",
            _ => "No action taken"
        };

        d.Status = DisputeStatus.Resolved;
        d.ResolutionNotes = $"{summary}. {note}";
        d.ResolvedAt = DateTime.UtcNow;

        var text = summary.ToLowerInvariant();
        await notifications.NotifyAsync(d.InfluencerProfile.UserId, "Dispute", "Dispute resolved", $"Your dispute was resolved: {text}.", "/Notifications");
        await notifications.NotifyAsync(d.BrandProfile.UserId, "Dispute", "Dispute resolved", $"The dispute was resolved: {text}.", "/Notifications");

        await db.SaveChangesAsync();

        TempData["DisputeMessage"] = $"Dispute resolved: {text}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int id, string party)
    {
        var d = await db.Disputes
            .Include(x => x.BrandProfile)
            .Include(x => x.InfluencerProfile)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (d is null) return NotFound();

        var isBrand = string.Equals(party, "Brand", StringComparison.OrdinalIgnoreCase);
        var userId = isBrand ? d.BrandProfile.UserId : d.InfluencerProfile.UserId;

        var user = await userManager.FindByIdAsync(userId);
        if (user is not null)
        {
            await userManager.SetLockoutEnabledAsync(user, true);
            await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            await userManager.UpdateSecurityStampAsync(user);
        }

        TempData["DisputeMessage"] = (isBrand ? "The brand" : "The influencer") + " account was suspended and signed out.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // --------------------------------------------------------- sample data

    private static List<DisputeRow> SampleRows()
    {
        var d = new DateTime(2026, 9, 18, 11, 20, 0, DateTimeKind.Utc);
        DisputeRow R(int n, string camp, string ms, string brand, string who, string handle, string reason, decimal amt, string by, int daysAgo, string status) =>
            new(n, $"DS-2026-{n:D3}", n, camp, ms, null, brand, null, who, handle, reason, amt, by, d.AddDays(-daysAgo), status);

        return new List<DisputeRow>
        {
            R(1, "Glow Naturally", "Product Mention", "PureGlow", "Sadia Islam", "sadia_says", "Low engagement than promised", 50000, "Brand", 0, "Open"),
            R(2, "Tech for All", "Reel Video", "TechMart BD", "Ahnaf Codes", "ahnaf.codes", "Content not as agreed", 35000, "Influencer", 1, "Open"),
            R(3, "Summer Collection", "Instagram Post", "StyleHub", "Mim Chowdhury", "mim.chowdhury", "Delayed payment", 25000, "Influencer", 2, "Under Review"),
            R(4, "Budget Laptop", "YouTube Review", "GadgetPro", "Tanvir Tech", "tanvir.tech", "Misleading claims", 60000, "Brand", 3, "Open"),
            R(5, "Festive Deals", "Story + Post", "ShopEase", "Rida Rahman", "rida_rahman", "Incomplete content", 40000, "Brand", 4, "Under Review"),
            R(6, "Fitness Challenge", "Reel Series", "FitLife BD", "Sami Fit", "sami.fit", "Payment not received", 30000, "Influencer", 5, "Open"),
            R(7, "New Year Campaign", "3 Posts", "TravelMate", "Nusrat Travels", "nusrat.travels", "Brand requested refund", 80000, "Brand", 6, "Resolved"),
            R(8, "Skincare Routine", "Reel Video", "GlowCare", "Farhan Skincare", "farhan.skincare", "Used unapproved product", 20000, "Brand", 8, "Open"),
            R(9, "Back to School", "Instagram Post", "LearnNest", "Tahia Content", "tahia.content", "Late submission", 15000, "Influencer", 9, "Resolved"),
            R(10, "Food Carnival", "Reel + Story", "TastyBite", "Arif Eats", "arif.eats", "Wrong information in content", 28000, "Brand", 10, "Under Review")
        };
    }

    private static DisputeDetailsVm SampleDetails(string? sides)
    {
        var raised = new DateTime(2026, 9, 18, 11, 20, 0, DateTimeKind.Utc);

        var vm = new DisputeDetailsVm
        {
            IsPreview = true,
            Id = 0,
            Code = "DS-2026-001",
            StatusLabel = "Open", StatusTone = "red", IsOpen = true,
            CreatedAt = raised, UpdatedAt = raised.AddDays(2).AddHours(5),
            RaisedBy = "Brand",
            Reason = "Low engagement than promised",

            BrandName = "PureGlow BD", BrandEmail = "contact@pureglowbd.com", BrandVerified = true,
            BrandCampaigns = 15, BrandPastDisputes = 2,
            InfluencerName = "Sadia Islam", InfluencerHandle = "sadia_says", InfluencerEmail = "sadia.islam@gmail.com", InfluencerVerified = true,
            InfluencerCollaborations = 28, InfluencerPastDisputes = 0,

            CampaignTitle = "Glow Naturally – Product Mention", CampaignCode = "CAM-2026-015",
            MilestoneTitle = "Instagram Post – Product Showcase", MilestoneType = "Image Post", Amount = 50000,

            ProofNotes = "Glowing skin starts with the right care ✨ Loving this new serum from @pureglowbd! #PureGlow #Skincare #BangladeshiBeauty",
            ProofUrl = "https://www.instagram.com/p/Cx123Abc/",
            ProofStatus = "Approved by Admin", ProofTone = "green",

            BrandStatement = "The engagement is significantly lower than the agreed minimum (50K impressions). We believe the content did not meet the campaign requirements. Please review and take necessary action.",
            BrandStatementAt = raised,
            InfluencerStatement = "I delivered the content as agreed and it was posted on time. Engagement depends on audience behaviour and is not guaranteed. Please consider releasing the payment.",
            InfluencerStatementAt = raised.AddMinutes(30),
            BrandEvidence = new List<EvidenceVm> { new("Campaign_Brief.pdf", "#", 327680), new("Low_Engagement_Screenshot.png", "#", 220160) },
            InfluencerEvidence = new List<EvidenceVm> { new("Analytics_Screenshot.png", "#", 286720) },

            Messages = new List<DisputeMessageRow>
            {
                new("Brand", "PureGlow BD", "We were expecting at least 50K impressions as mentioned in the brief, but the post only received 12K.", raised.AddMinutes(2), null),
                new("Influencer", "Sadia Islam", "I understand your concern, but engagement can vary. I delivered the content as agreed and it was posted on time.", raised.AddMinutes(15), null),
                new("Brand", "PureGlow BD", "We believe the content did not meet the campaign requirements. Kindly look into this.", raised.AddMinutes(22), null),
                new("Influencer", "Sadia Islam", "Please consider releasing the payment. The content follows the brief and is still live on my profile.", raised.AddMinutes(28), null)
            },
            Notes = new List<NoteVm> { new("Admin (You)", raised.AddDays(2), "Checked the analytics screenshot. Reach looks normal for the account size; leaning towards releasing the payment.") },

            Timeline = new List<DisputeTimelineStep>
            {
                new("Dispute raised by Brand", raised, "done"),
                new("Influencer statement submitted", raised.AddMinutes(30), "done"),
                new("Admin assigned", raised.AddMinutes(25), "done"),
                new("Under review", raised.AddMinutes(30), "done"),
                new("Waiting for resolution", null, "current")
            },

            SamplePayment = new SamplePaymentVm("Pending", "amber", "Held", "bKash", "—"),
            CanRelease = true, CanRefund = true,
            HasSchemaExtras = true
        };

        if (string.Equals(sides, "brand", StringComparison.OrdinalIgnoreCase))
        {
            // Only the brand has reported: the influencer has not responded yet.
            vm.InfluencerStatement = null;
            vm.InfluencerStatementAt = null;
            vm.InfluencerEvidence = new List<EvidenceVm>();
            vm.Timeline = new List<DisputeTimelineStep>
            {
                new("Dispute raised by Brand", raised, "done"),
                new("Waiting for Influencer's statement", null, "pending"),
                new("Admin assigned", raised.AddMinutes(25), "done"),
                new("Under review", raised.AddMinutes(30), "done"),
                new("Waiting for resolution", null, "current")
            };
        }
        else if (string.Equals(sides, "influencer", StringComparison.OrdinalIgnoreCase))
        {
            // Only the influencer has reported: the brand has not responded yet.
            vm.RaisedBy = "Influencer";
            vm.BrandStatement = null;
            vm.BrandStatementAt = null;
            vm.BrandEvidence = new List<EvidenceVm>();
            vm.InfluencerStatementAt = raised;
            vm.Timeline = new List<DisputeTimelineStep>
            {
                new("Dispute raised by Influencer", raised, "done"),
                new("Waiting for Brand's statement", null, "pending"),
                new("Admin assigned", raised.AddMinutes(25), "done"),
                new("Under review", raised.AddMinutes(30), "done"),
                new("Waiting for resolution", null, "current")
            };
        }

        return vm;
    }
}

public record DisputeRow(
    int Id,
    string Code,
    int CampaignId,
    string CampaignTitle,
    string MilestoneTitle,
    string? MilestoneType,
    string BrandName,
    string? BrandPicture,
    string InfluencerName,
    string Handle,
    string Reason,
    decimal Amount,
    string RaisedBy,
    DateTime CreatedAt,
    string Status);

public class DisputeIndexViewModel
{
    public bool IsPreview { get; set; }
    public List<DisputeRow> Rows { get; set; } = new();
    public int Total { get; set; }
    public int OpenCount { get; set; }
    public int UnderReviewCount { get; set; }
    public int ResolvedCount { get; set; }
    public decimal AmountInDispute { get; set; }
    public decimal? TotalTrend { get; set; }
    public decimal? ResolvedTrend { get; set; }
    public List<string> Campaigns { get; set; } = new();
}

public record DisputeMessageRow(string Party, string Name, string Body, DateTime At, string? MediaUrl);
public record DisputeTimelineStep(string Title, DateTime? At, string State);
public record EvidenceVm(string FileName, string Url, long SizeBytes);
public record NoteVm(string Admin, DateTime At, string Body);
public record SamplePaymentVm(string Status, string Tone, string Escrow, string Method, string Reference);

public class DisputeDetailsVm
{
    public bool IsPreview { get; set; }
    public bool HasSchemaExtras { get; set; }

    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string StatusLabel { get; set; } = "";
    public string StatusTone { get; set; } = "";
    public bool IsResolved { get; set; }
    public bool IsOpen { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string RaisedBy { get; set; } = "";
    public string Reason { get; set; } = "";

    public string BrandName { get; set; } = "";
    public string? BrandPicture { get; set; }
    public string BrandEmail { get; set; } = "";
    public bool BrandVerified { get; set; }
    public int BrandProfileId { get; set; }
    public int BrandCampaigns { get; set; }
    public int BrandPastDisputes { get; set; }

    public string InfluencerName { get; set; } = "";
    public string InfluencerHandle { get; set; } = "";
    public string InfluencerEmail { get; set; } = "";
    public bool InfluencerVerified { get; set; }
    public int InfluencerProfileId { get; set; }
    public int InfluencerCollaborations { get; set; }
    public int InfluencerPastDisputes { get; set; }

    public int CampaignId { get; set; }
    public string CampaignTitle { get; set; } = "";
    public string CampaignCode { get; set; } = "";
    public string? CampaignMedia { get; set; }
    public int? MilestoneId { get; set; }
    public string? MilestoneTitle { get; set; }
    public string? MilestoneType { get; set; }
    public decimal Amount { get; set; }

    public string? ProofUrl { get; set; }
    public string? ProofNotes { get; set; }
    public string? ProofStatus { get; set; }
    public string ProofTone { get; set; } = "amber";

    public string? BrandStatement { get; set; }
    public DateTime? BrandStatementAt { get; set; }
    public string? InfluencerStatement { get; set; }
    public DateTime? InfluencerStatementAt { get; set; }
    public List<EvidenceVm> BrandEvidence { get; set; } = new();
    public List<EvidenceVm> InfluencerEvidence { get; set; } = new();

    public List<DisputeMessageRow> Messages { get; set; } = new();
    public List<NoteVm> Notes { get; set; } = new();
    public List<DisputeTimelineStep> Timeline { get; set; } = new();

    public Payment? Payment { get; set; }
    public SamplePaymentVm? SamplePayment { get; set; }
    public bool CanRelease { get; set; }
    public bool CanRefund { get; set; }

    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public decimal WalletBalance { get; set; }
    public string AdminName { get; set; } = "";
}
