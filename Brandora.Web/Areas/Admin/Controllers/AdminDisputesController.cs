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
    private static PayoutMethodKind MethodKind(string? method) => method switch
    {
        "bKash" => PayoutMethodKind.Bkash,
        "Nagad" => PayoutMethodKind.Nagad,
        _ => PayoutMethodKind.BankAccount
    };

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

    public async Task<IActionResult> Index()
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Disputes";
        ViewData["Title"] = "Dispute Resolution";
        ViewData["HasCustomHero"] = true;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dispute Resolution", null) };

        var disputes = await db.Disputes
            .Include(d => d.BrandProfile)
            .Include(d => d.InfluencerProfile)
            .Include(d => d.Collaboration).ThenInclude(c => c.Campaign)
            .Include(d => d.Milestone)
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();

        var rows = disputes.Select(d => new DisputeRow(
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
            d.RaisedBy.ToString(),
            d.CreatedAt,
            StatusLabel(d.Status))).ToList();

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = monthStart.AddMonths(-1);
        var trendTotal = Trend(
            disputes.Count(d => d.CreatedAt >= monthStart),
            disputes.Count(d => d.CreatedAt >= lastMonthStart && d.CreatedAt < monthStart));
        var trendResolved = Trend(
            disputes.Count(d => d.ResolvedAt >= monthStart),
            disputes.Count(d => d.ResolvedAt >= lastMonthStart && d.ResolvedAt < monthStart));

        var ledger = await PlatformWalletLedger.LoadAsync(db);

        return View(new DisputeIndexViewModel
        {
            Rows = rows,
            Total = rows.Count,
            OpenCount = rows.Count(r => r.Status == "Open"),
            UnderReviewCount = rows.Count(r => r.Status == "Under Review"),
            ResolvedCount = rows.Count(r => r.Status == "Resolved"),
            AmountInDispute = rows.Where(r => r.Status != "Resolved").Sum(r => r.Amount),
            TotalTrend = trendTotal,
            ResolvedTrend = trendResolved,
            Campaigns = rows.Select(r => r.CampaignTitle).Distinct().OrderBy(t => t).ToList(),
            CompensationThisMonth = ledger.CompensationThisMonth
        });
    }

    // ------------------------------------------------------------- details

    public async Task<IActionResult> Details(int id)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Disputes";
        ViewData["HasCustomHero"] = true;

        var snap = await PlatformWalletData.LoadAsync(db);
        var walletBalance = PlatformWalletData.Balance(snap);

        var d = await db.Disputes
            .Include(x => x.BrandProfile).ThenInclude(b => b.User)
            .Include(x => x.InfluencerProfile).ThenInclude(i => i.User)
            .Include(x => x.Collaboration).ThenInclude(c => c.Campaign)
            .Include(x => x.Milestone)
            .Include(x => x.Evidence)
            .Include(x => x.Notes)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (d is null) return NotFound();
        var vm = await MapReal(d);

        vm.WalletBalance = walletBalance;
        vm.AdminName = AdminName;

        ViewData["Title"] = "Dispute " + vm.Code;
        ViewData["Breadcrumb"] = new List<(string, string?)>
        {
            ("Dispute Resolution", "/Admin/AdminDisputes/Index"),
            (vm.Code, null)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(int id, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return RedirectToAction(nameof(Details), new { id });
        }

        var exists = await db.Disputes.AnyAsync(x => x.Id == id);
        if (!exists) return NotFound();

        db.DisputeNotes.Add(new DisputeNote { DisputeId = id, AdminName = AdminName, Body = body.Trim() });
        await db.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id });
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
        var raisedByBrand = d.RaisedBy == ProposalInitiator.Brand;

        // Real steps, in the order they actually happened — not a fixed 3-step guess.
        var timeline = new List<DisputeTimelineStep>
        {
            new($"Dispute raised by {(raisedByBrand ? "Brand" : "Influencer")}", d.CreatedAt, "done")
        };
        var otherStatementAt = raisedByBrand ? d.InfluencerStatementAt : d.BrandStatementAt;
        var otherLabel = raisedByBrand ? "Influencer" : "Brand";
        if (otherStatementAt is not null)
            timeline.Add(new($"{otherLabel}'s statement submitted", otherStatementAt, "done"));
        else if (!resolved)
            timeline.Add(new($"Waiting for {otherLabel}'s statement", null, "pending"));
        timeline.Add(new("Under review", null, d.Status == DisputeStatus.Open ? "pending" : "done"));
        timeline.Add(resolved
            ? new DisputeTimelineStep("Resolved", d.ResolvedAt, "done")
            : new DisputeTimelineStep("Waiting for resolution", null, "current"));

        var evidence = d.Evidence.OrderBy(e => e.UploadedAt).ToList();
        var notes = d.Notes.OrderBy(n => n.CreatedAt)
            .Select(n => new NoteVm(n.AdminName, n.CreatedAt, n.Body))
            .ToList();

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
            RaisedBy = d.RaisedBy.ToString(),
            Reason = d.Reason,

            BrandStatement = d.BrandStatement,
            BrandStatementAt = d.BrandStatementAt,
            InfluencerStatement = d.InfluencerStatement,
            InfluencerStatementAt = d.InfluencerStatementAt,
            BrandEvidence = evidence.Where(e => e.UploadedBy == ProposalInitiator.Brand)
                .Select(e => new EvidenceVm(e.FileName, e.FileUrl, e.FileSizeBytes)).ToList(),
            InfluencerEvidence = evidence.Where(e => e.UploadedBy == ProposalInitiator.Influencer)
                .Select(e => new EvidenceVm(e.FileName, e.FileUrl, e.FileSizeBytes)).ToList(),
            Notes = notes,

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
            ResolutionNote = d.ResolutionNotes,
            ResolvedAt = d.ResolvedAt,
            HasSchemaExtras = true
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

    // Only two real outcomes exist: Compensation (paid from the platform's own earned
    // Admin Wallet balance) and No Action. "Release Payment"/"Refund Brand" are gone — only
    // the brand can start a real gateway payment, and neither bKash nor Nagad has a refund
    // endpoint, so nothing here can genuinely move money the way those implied. Compensation
    // is the one thing that's actually real: to an influencer it's instant (their own in-app
    // wallet, via WalletTransaction(Adjustment)); to a brand it's the same manual-send-then-log
    // pattern as an admin cash-out, since a brand has no in-app wallet to credit.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(
        int id, string? outcome, string? notes,
        string? compensationTo, decimal? compensationAmount,
        string? compensationMethod, string? compensationAccount, string? compensationReference)
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

        var allowed = new[] { "Compensation", "NoAction" };
        if (outcome is null || !allowed.Contains(outcome)) return Fail("Choose how to resolve this dispute.");

        string summary;

        if (outcome == "Compensation")
        {
            if (compensationTo is not ("Brand" or "Influencer")) return Fail("Choose who receives the compensation.");
            if (compensationAmount is not (> 0)) return Fail("Enter a compensation amount greater than ৳0.");

            var toInfluencer = compensationTo == "Influencer";
            var tx = new PlatformWalletTransaction
            {
                Type = PlatformWalletTransactionType.Compensation,
                Amount = compensationAmount.Value,
                DisputeId = d.Id,
                Note = note,
                ProcessedByAdmin = AdminName
            };

            if (toInfluencer)
            {
                // Real, instant, internal: credited straight to the influencer's own wallet —
                // no external send needed, so no method/account/reference is required here.
                tx.RecipientInfluencerProfileId = d.InfluencerProfileId;
                tx.Method = PayoutMethodKind.Bkash;
                tx.AccountDetail = "Credited to influencer's in-app wallet";
                tx.GatewayReference = "Internal wallet credit — no external transfer";

                db.WalletTransactions.Add(new WalletTransaction
                {
                    InfluencerProfileId = d.InfluencerProfileId,
                    Type = WalletTransactionType.Adjustment,
                    Amount = compensationAmount.Value,
                    Description = $"Dispute compensation: {note}"
                });
            }
            else
            {
                // A brand has no in-app wallet — this is real money leaving the platform's
                // own merchant account, so it needs the same manual-send-then-log details as
                // an admin cash-out: the admin sends it themselves and logs the real reference.
                var acct = (compensationAccount ?? "").Trim();
                var reff = (compensationReference ?? "").Trim();
                if (compensationMethod is not ("bKash" or "Nagad" or "Bank Transfer")) return Fail("Choose how you sent the compensation.");
                if (acct.Length == 0) return Fail("Enter the brand's account you sent the compensation to.");
                if (reff.Length == 0) return Fail("Enter the real transaction reference from your bKash/Nagad app.");

                tx.RecipientBrandProfileId = d.BrandProfileId;
                tx.Method = MethodKind(compensationMethod);
                tx.AccountDetail = acct;
                tx.GatewayReference = reff;
            }

            db.PlatformWalletTransactions.Add(tx);

            summary = toInfluencer
                ? $"Compensation of ৳{compensationAmount:N0} credited to the influencer's wallet"
                : $"Compensation of ৳{compensationAmount:N0} sent to the brand (ref: {tx.GatewayReference})";
        }
        else
        {
            summary = "No action taken";
        }

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
    public List<DisputeRow> Rows { get; set; } = new();
    public int Total { get; set; }
    public int OpenCount { get; set; }
    public int UnderReviewCount { get; set; }
    public int ResolvedCount { get; set; }
    public decimal AmountInDispute { get; set; }
    public decimal? TotalTrend { get; set; }
    public decimal? ResolvedTrend { get; set; }
    public List<string> Campaigns { get; set; } = new();
    public decimal CompensationThisMonth { get; set; }
}

public record DisputeMessageRow(string Party, string Name, string Body, DateTime At, string? MediaUrl);
public record DisputeTimelineStep(string Title, DateTime? At, string State);
public record EvidenceVm(string FileName, string Url, long SizeBytes);
public record NoteVm(string Admin, DateTime At, string Body);
public record SamplePaymentVm(string Status, string Tone, string Method, string Reference);

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

    public string? ResolutionNote { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public decimal WalletBalance { get; set; }
    public string AdminName { get; set; } = "";
}
