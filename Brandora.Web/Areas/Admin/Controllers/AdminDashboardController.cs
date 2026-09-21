using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public record RecentActivityItem(string Title, string Category, string Url, DateTime CreatedAt, string StatusLabel, string StatusColor);
public record MonthPoint(string Month, decimal Revenue, int PaymentCount, decimal ChangePct);
public record PulsePoint(string Month, int NewUsers, int NewCampaigns, int CompletedCollaborations);
public record FunnelStage(string Label, int Value);
public record PaymentStatusBucket(string Label, decimal Amount, int Count);
public record CategoryShare(string Label, int Count, decimal Pct);
public record StreamEvent(double HourAgo, string Type, string Label, string Url);
public record CampaignPerformanceRow(string Name, string Brand, string Url, int? CompletionPct);
public record CampaignSummaryRow(string Title, string Brand, int InfluencerCount, DateTime? Deadline, string StatusLabel, string StatusColor, string Url);
public record HeatmapRow(string Label, int[] Values);

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int ActiveCampaigns { get; set; }
    public int PendingVerifications { get; set; }
    public int PendingProofReviews { get; set; }
    public int OpenDisputes { get; set; }
    public decimal TotalTransactionVolume { get; set; }
    public List<RecentActivityItem> RecentActivity { get; set; } = new();

    // Platform wallet — the commission collected from both sides.
    public decimal CommissionPercent { get; set; }
    public decimal BrandFeesCollected { get; set; }          // added on top at Brand checkout
    public int BrandFeePaymentCount { get; set; }
    public decimal WithdrawalFeesCollected { get; set; }     // creator share, withdrawal approved/paid
    public decimal WithdrawalFeesPending { get; set; }       // creator share, withdrawal awaiting review
    public decimal LegacySettlementFees { get; set; }        // deducted from creators before the two-sided model
    public decimal PlatformWalletBalance => BrandFeesCollected + WithdrawalFeesCollected + LegacySettlementFees;

    // KPI trend deltas (% change vs previous period), null when there's no prior-period data to compare against
    public decimal? TotalUsersTrendPct { get; set; }
    public decimal? ActiveCampaignsTrendPct { get; set; }
    public decimal? PendingVerificationsTrendPct { get; set; }
    public decimal? OpenDisputesTrendPct { get; set; }
    public decimal? TotalPaymentsTrendPct { get; set; }

    public List<MonthPoint> PaymentFlow { get; set; } = new();
    public List<PulsePoint> BrandoraPulse { get; set; } = new();
    public List<FunnelStage> Funnel { get; set; } = new();
    public List<PaymentStatusBucket> PaymentStatusFlow { get; set; } = new();

    public int InfluencerCount { get; set; }
    public int BrandCount { get; set; }
    public int OtherUserCount { get; set; }
    public int NewCreatorsThisMonth { get; set; }
    public int NewBrandsThisMonth { get; set; }

    public string[] HeatmapDays { get; set; } = Array.Empty<string>();
    public List<HeatmapRow> Heatmap { get; set; } = new();

    public List<CategoryShare> CategoryOrbit { get; set; } = new();
    public int CategoryOrbitTotal { get; set; }
    public List<StreamEvent> ActivityStream { get; set; } = new();
    public List<CampaignPerformanceRow> TopCampaigns { get; set; } = new();
    public List<CampaignSummaryRow> OngoingCampaigns { get; set; } = new();

    public int[] SparkUsers { get; set; } = Array.Empty<int>();
    public int[] SparkCampaigns { get; set; } = Array.Empty<int>();
    public int[] SparkVerifications { get; set; } = Array.Empty<int>();
    public int[] SparkDisputes { get; set; } = Array.Empty<int>();
    public int[] SparkPayments { get; set; } = Array.Empty<int>();
}

public class AdminDashboardController(ApplicationDbContext db, IConfiguration config) : AdminControllerBase(db)
{
    private static decimal? PctChange(decimal previous, decimal current)
    {
        if (previous == 0) return current == 0 ? 0 : null; // no baseline to compare against
        return Math.Round((current - previous) / previous * 100, 1);
    }

    public async Task<IActionResult> Index()
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Dashboard";
        ViewData["Title"] = "Admin Dashboard";
        // This page has its own hero card with the greeting, so the shared topbar's plain
        // heading/subheading text is suppressed (see _AdminLayout). The breadcrumb renders
        // INSIDE the hero card itself (via _AdminBreadcrumb), not in the topbar.
        ViewData["HasCustomHero"] = true;
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Dashboard", null) };

        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var prevMonthStart = monthStart.AddMonths(-1);

        var vm = new AdminDashboardViewModel
        {
            TotalUsers = await db.Users.CountAsync(),
            ActiveCampaigns = await db.Campaigns.CountAsync(c => c.Status == CampaignStatus.Active),
            PendingVerifications = await db.InfluencerProfiles.CountAsync(i => !i.Verified),
            PendingProofReviews = await db.Milestones.CountAsync(m => m.Status == MilestoneStatus.Submitted),
            OpenDisputes = await db.Disputes.CountAsync(d => d.Status == DisputeStatus.Open),
            TotalTransactionVolume = await db.Payments.Where(p => p.Status == PaymentStatus.Completed).SumAsync(p => (decimal?)p.Amount) ?? 0m
        };

        // ---- Platform wallet (commission from both sides) ----
        vm.CommissionPercent = decimal.TryParse(config["PLATFORM_COMMISSION_PERCENT"], out var commissionPct) ? commissionPct : 10m;
        vm.BrandFeesCollected = await db.Payments.Where(p => p.Status == PaymentStatus.Completed).SumAsync(p => (decimal?)p.BrandFeeAmount) ?? 0m;
        vm.BrandFeePaymentCount = await db.Payments.CountAsync(p => p.Status == PaymentStatus.Completed && p.BrandFeeAmount > 0);
        vm.LegacySettlementFees = await db.Payments.Where(p => p.Status == PaymentStatus.Completed).SumAsync(p => (decimal?)p.PlatformFeeAmount) ?? 0m;
        vm.WithdrawalFeesCollected = await db.WithdrawalRequests
            .Where(w => w.Status == WithdrawalStatus.Approved || w.Status == WithdrawalStatus.Paid)
            .SumAsync(w => (decimal?)w.FeeAmount) ?? 0m;
        vm.WithdrawalFeesPending = await db.WithdrawalRequests
            .Where(w => w.Status == WithdrawalStatus.Pending)
            .SumAsync(w => (decimal?)w.FeeAmount) ?? 0m;

        // ---- KPI trend deltas (this month vs last month, by records created) ----
        var usersThisMonth = await db.Users.CountAsync(u => u.CreatedAt >= monthStart);
        var usersLastMonth = await db.Users.CountAsync(u => u.CreatedAt >= prevMonthStart && u.CreatedAt < monthStart);
        vm.TotalUsersTrendPct = PctChange(usersLastMonth, usersThisMonth);

        var campaignsThisMonth = await db.Campaigns.CountAsync(c => c.CreatedAt >= monthStart);
        var campaignsLastMonth = await db.Campaigns.CountAsync(c => c.CreatedAt >= prevMonthStart && c.CreatedAt < monthStart);
        vm.ActiveCampaignsTrendPct = PctChange(campaignsLastMonth, campaignsThisMonth);

        var verificationsThisMonth = await db.InfluencerProfiles.CountAsync(i => !i.Verified && i.CreatedAt >= monthStart);
        var verificationsLastMonth = await db.InfluencerProfiles.CountAsync(i => i.CreatedAt >= prevMonthStart && i.CreatedAt < monthStart);
        vm.PendingVerificationsTrendPct = PctChange(verificationsLastMonth, verificationsThisMonth);

        var disputesThisMonth = await db.Disputes.CountAsync(d => d.CreatedAt >= monthStart);
        var disputesLastMonth = await db.Disputes.CountAsync(d => d.CreatedAt >= prevMonthStart && d.CreatedAt < monthStart);
        vm.OpenDisputesTrendPct = PctChange(disputesLastMonth, disputesThisMonth);

        var paymentsThisMonth = await db.Payments.Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= monthStart).SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var paymentsLastMonth = await db.Payments.Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= prevMonthStart && p.CreatedAt < monthStart).SumAsync(p => (decimal?)p.Amount) ?? 0m;
        vm.TotalPaymentsTrendPct = PctChange(paymentsLastMonth, paymentsThisMonth);

        // ---- Payment Flow: last 7 months revenue + payment count ----
        var monthBuckets = Enumerable.Range(0, 7)
            .Select(i => new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-6 + i))
            .ToList();

        var allCompletedPayments = await db.Payments
            .Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= monthBuckets[0])
            .Select(p => new { p.Amount, p.CreatedAt })
            .ToListAsync();

        decimal prevRevenue = 0;
        for (int i = 0; i < monthBuckets.Count; i++)
        {
            var bucketStart = monthBuckets[i];
            var bucketEnd = bucketStart.AddMonths(1);
            var inBucket = allCompletedPayments.Where(p => p.CreatedAt >= bucketStart && p.CreatedAt < bucketEnd).ToList();
            var revenue = inBucket.Sum(p => p.Amount);
            var change = i == 0 ? 0 : PctChange(prevRevenue, revenue) ?? 0;
            vm.PaymentFlow.Add(new MonthPoint(bucketStart.ToString("MMM"), revenue, inBucket.Count, change));
            prevRevenue = revenue;
        }

        // ---- Brandora Pulse: last 7 months — new users, campaigns created, collaborations completed ----
        var allUsersCreated = await db.Users.Where(u => u.CreatedAt >= monthBuckets[0]).Select(u => u.CreatedAt).ToListAsync();
        var allCampaignsCreated = await db.Campaigns.Where(c => c.CreatedAt >= monthBuckets[0]).Select(c => c.CreatedAt).ToListAsync();
        var allCollabsCompleted = await db.Collaborations.Where(c => c.Status == CollaborationStatus.Completed && c.CreatedAt >= monthBuckets[0]).Select(c => c.CreatedAt).ToListAsync();

        foreach (var bucketStart in monthBuckets)
        {
            var bucketEnd = bucketStart.AddMonths(1);
            vm.BrandoraPulse.Add(new PulsePoint(
                bucketStart.ToString("MMM"),
                allUsersCreated.Count(d => d >= bucketStart && d < bucketEnd),
                allCampaignsCreated.Count(d => d >= bucketStart && d < bucketEnd),
                allCollabsCompleted.Count(d => d >= bucketStart && d < bucketEnd)));
        }

        // ---- Campaign Lifecycle Funnel ----
        vm.Funnel.Add(new FunnelStage("Created", await db.Campaigns.CountAsync()));
        vm.Funnel.Add(new FunnelStage("Applications Received", await db.Proposals.CountAsync()));
        vm.Funnel.Add(new FunnelStage("Collaborations Started", await db.Collaborations.CountAsync()));
        vm.Funnel.Add(new FunnelStage("Proofs Submitted", await db.Milestones.CountAsync(m => m.ProofUrl != null)));
        vm.Funnel.Add(new FunnelStage("Completed", await db.Collaborations.CountAsync(c => c.Status == CollaborationStatus.Completed)));

        // ---- Payment Status Flow ----
        var paymentsByStatus = await db.Payments
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Amount = g.Sum(p => p.Amount), Count = g.Count() })
            .ToListAsync();
        foreach (var status in new[] { PaymentStatus.Completed, PaymentStatus.Pending, PaymentStatus.Failed })
        {
            var bucket = paymentsByStatus.FirstOrDefault(b => b.Status == status);
            vm.PaymentStatusFlow.Add(new PaymentStatusBucket(status.ToString(), bucket?.Amount ?? 0m, bucket?.Count ?? 0));
        }

        // ---- Creator x Brand Marketplace Balance ----
        vm.InfluencerCount = await db.InfluencerProfiles.CountAsync();
        vm.BrandCount = await db.BrandProfiles.CountAsync();
        vm.OtherUserCount = Math.Max(0, vm.TotalUsers - vm.InfluencerCount - vm.BrandCount);
        vm.NewCreatorsThisMonth = await db.InfluencerProfiles.CountAsync(i => i.CreatedAt >= monthStart);
        vm.NewBrandsThisMonth = await db.BrandProfiles.CountAsync(b => b.CreatedAt >= monthStart);

        // ---- Admin Attention Heatmap: rolling last 7 days, by day-of-week the item was created ----
        var weekStart = now.Date.AddDays(-6);
        vm.HeatmapDays = Enumerable.Range(0, 7).Select(i => weekStart.AddDays(i).ToString("ddd")).ToArray();

        var pendingVerificationDates = await db.InfluencerProfiles.Where(i => !i.Verified && i.CreatedAt >= weekStart).Select(i => i.CreatedAt).ToListAsync();
        var submittedProofDates = await db.Milestones.Where(m => m.Status == MilestoneStatus.Submitted && m.CreatedAt >= weekStart).Select(m => m.CreatedAt).ToListAsync();
        var openDisputeDates = await db.Disputes.Where(d => d.Status == DisputeStatus.Open && d.CreatedAt >= weekStart).Select(d => d.CreatedAt).ToListAsync();
        var pendingPaymentDates = await db.Payments.Where(p => p.Status == PaymentStatus.Pending && p.CreatedAt >= weekStart).Select(p => p.CreatedAt).ToListAsync();

        int[] DayCounts(List<DateTime> dates) => Enumerable.Range(0, 7)
            .Select(i => dates.Count(d => d.Date == weekStart.AddDays(i)))
            .ToArray();

        vm.Heatmap.Add(new HeatmapRow("Verification", DayCounts(pendingVerificationDates)));
        vm.Heatmap.Add(new HeatmapRow("Proof Review", DayCounts(submittedProofDates)));
        vm.Heatmap.Add(new HeatmapRow("Disputes", DayCounts(openDisputeDates)));
        vm.Heatmap.Add(new HeatmapRow("Payments", DayCounts(pendingPaymentDates)));

        // ---- Campaign Category Orbit (by Campaign.Niche) ----
        var campaignsByNiche = await db.Campaigns
            .GroupBy(c => c.Niche == null || c.Niche == "" ? "Other" : c.Niche)
            .Select(g => new { Niche = g.Key, Count = g.Count() })
            .ToListAsync();
        var totalCampaigns = campaignsByNiche.Sum(g => g.Count);
        vm.CategoryOrbitTotal = totalCampaigns;
        if (totalCampaigns > 0)
        {
            vm.CategoryOrbit = campaignsByNiche
                .OrderByDescending(g => g.Count)
                .Take(6)
                .Select(g => new CategoryShare(g.Niche, g.Count, Math.Round(g.Count / (decimal)totalCampaigns * 100, 1)))
                .ToList();
        }

        // ---- Platform Activity Stream (last 24h) ----
        var since24h = now.AddHours(-24);
        var streamItems = new List<StreamEvent>();

        streamItems.AddRange((await db.Users.Where(u => u.CreatedAt >= since24h).ToListAsync())
            .Select(u => new StreamEvent((now - u.CreatedAt).TotalHours, "signup",
                (u.Role == UserRole.Brand ? "New brand registered: " : "New influencer signed up: ") + u.DisplayName,
                "/Admin/AdminUserVerification/Index")));

        streamItems.AddRange((await db.Campaigns.Where(c => c.CreatedAt >= since24h).Include(c => c.BrandProfile).ToListAsync())
            .Select(c => new StreamEvent((now - c.CreatedAt).TotalHours, "campaign", $"\"{c.Title}\" campaign published", "/Admin/AdminCampaigns/Index")));

        streamItems.AddRange((await db.Milestones.Where(m => m.ProofUrl != null && m.CreatedAt >= since24h).ToListAsync())
            .Select(m => new StreamEvent((now - m.CreatedAt).TotalHours, "proof", "Proof submitted: " + m.Title, "/Admin/AdminProofReview/Details/" + m.Id)));

        streamItems.AddRange((await db.Payments.Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= since24h).ToListAsync())
            .Select(p => new StreamEvent((now - p.CreatedAt).TotalHours, "payment", $"Payment of ৳{p.Amount:N0} released", "/Admin/AdminPayments/Index")));

        streamItems.AddRange((await db.Disputes.Where(d => d.CreatedAt >= since24h).ToListAsync())
            .Select(d => new StreamEvent((now - d.CreatedAt).TotalHours, "dispute", "Dispute raised: " + d.Reason, "/Admin/AdminDisputes/Details/" + d.Id)));

        vm.ActivityStream = streamItems.OrderBy(e => e.HourAgo).Take(24).ToList();

        // ---- Top Campaign Performance (by milestone completion rate); Reach/Engagement not tracked in schema yet ----
        var campaignsWithMilestones = await db.Collaborations
            .GroupBy(c => new { c.CampaignId, c.Campaign.Title, BrandName = c.Campaign.BrandProfile.CompanyName })
            .Select(g => new
            {
                g.Key.CampaignId,
                g.Key.Title,
                g.Key.BrandName,
                TotalMilestones = g.SelectMany(c => c.Milestones).Count(),
                DoneMilestones = g.SelectMany(c => c.Milestones).Count(m => m.Status == MilestoneStatus.Approved || m.Status == MilestoneStatus.Paid)
            })
            .Where(g => g.TotalMilestones > 0)
            .ToListAsync();

        vm.TopCampaigns = campaignsWithMilestones
            .Select(c => new CampaignPerformanceRow(c.Title, c.BrandName, "/Admin/AdminProofReview/Index", (int)Math.Round(c.DoneMilestones / (double)c.TotalMilestones * 100)))
            .OrderByDescending(c => c.CompletionPct)
            .Take(5)
            .ToList();

        // ---- Ongoing Campaigns (most recently created, real data) ----
        var recentCampaigns = await db.Campaigns
            .Include(c => c.BrandProfile)
            .OrderByDescending(c => c.CreatedAt)
            .Take(4)
            .Select(c => new
            {
                c.Id,
                c.Title,
                BrandName = c.BrandProfile.CompanyName,
                c.Status,
                c.Deadline,
                InfluencerCount = c.Proposals.Count(p => p.Status == ProposalStatus.Accepted)
            })
            .ToListAsync();

        vm.OngoingCampaigns = recentCampaigns.Select(c => new CampaignSummaryRow(
            c.Title,
            c.BrandName,
            c.InfluencerCount,
            c.Deadline,
            c.Status.ToString(),
            c.Status switch
            {
                CampaignStatus.Active => "green",
                CampaignStatus.Completed => "gray",
                CampaignStatus.Published => "blue",
                CampaignStatus.Cancelled => "red",
                _ => "gray"
            },
            $"/Admin/AdminCampaigns/Index?status={c.Status}")).ToList();

        // ---- Mini sparklines: last 7 days daily counts ----
        var sparkWeekStart = now.Date.AddDays(-6);
        var spanUsers = await db.Users.Where(u => u.CreatedAt >= sparkWeekStart).Select(u => u.CreatedAt).ToListAsync();
        var spanCampaigns = await db.Campaigns.Where(c => c.CreatedAt >= sparkWeekStart).Select(c => c.CreatedAt).ToListAsync();
        var spanVerifications = await db.InfluencerProfiles.Where(i => i.CreatedAt >= sparkWeekStart).Select(i => i.CreatedAt).ToListAsync();
        var spanDisputes = await db.Disputes.Where(d => d.CreatedAt >= sparkWeekStart).Select(d => d.CreatedAt).ToListAsync();
        var spanPayments = await db.Payments.Where(p => p.Status == PaymentStatus.Completed && p.CreatedAt >= sparkWeekStart).Select(p => p.CreatedAt).ToListAsync();

        int[] DailyCounts(List<DateTime> dates) => Enumerable.Range(0, 7).Select(i => dates.Count(d => d.Date == sparkWeekStart.AddDays(i))).ToArray();
        vm.SparkUsers = DailyCounts(spanUsers);
        vm.SparkCampaigns = DailyCounts(spanCampaigns);
        vm.SparkVerifications = DailyCounts(spanVerifications);
        vm.SparkDisputes = DailyCounts(spanDisputes);
        vm.SparkPayments = DailyCounts(spanPayments);

        // ---- Recent Activity feed ----
        var recentVerifications = await db.InfluencerProfiles
            .OrderByDescending(i => i.CreatedAt)
            .Take(5)
            .Select(i => new RecentActivityItem(
                i.FullName + " registered",
                "Verification",
                "/Admin/AdminUserVerification/Details?type=influencer&id=" + i.Id,
                i.CreatedAt,
                i.Verified ? "Verified" : "Pending",
                i.Verified ? "green" : "gray"))
            .ToListAsync();

        var recentProofs = await db.Milestones
            .Where(m => m.ProofUrl != null)
            .OrderByDescending(m => m.CreatedAt)
            .Take(5)
            .Select(m => new RecentActivityItem(
                "Proof submitted: " + m.Title,
                "Proof Review",
                "/Admin/AdminProofReview/Details/" + m.Id,
                m.CreatedAt,
                m.Status.ToString(),
                m.Status == MilestoneStatus.Approved || m.Status == MilestoneStatus.Paid ? "green" :
                    m.Status == MilestoneStatus.Submitted ? "blue" :
                    m.Status == MilestoneStatus.RevisionRequested ? "red" : "gray"))
            .ToListAsync();

        var recentDisputes = await db.Disputes
            .OrderByDescending(d => d.CreatedAt)
            .Take(5)
            .Select(d => new RecentActivityItem(
                "Dispute: " + d.Reason,
                "Dispute",
                "/Admin/AdminDisputes/Details/" + d.Id,
                d.CreatedAt,
                d.Status.ToString(),
                d.Status == DisputeStatus.Resolved ? "green" : d.Status == DisputeStatus.UnderReview ? "blue" : "gray"))
            .ToListAsync();

        var recentPayments = await db.Payments
            .Where(p => p.Status == PaymentStatus.Completed)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .Select(p => new RecentActivityItem(
                $"Payment of ৳{p.Amount:N0} completed",
                "Payment",
                "/Admin/AdminPayments/Index",
                p.CreatedAt,
                "Completed",
                "green"))
            .ToListAsync();

        vm.RecentActivity = recentVerifications
            .Concat(recentProofs)
            .Concat(recentDisputes)
            .Concat(recentPayments)
            .OrderByDescending(a => a.CreatedAt)
            .Take(10)
            .ToList();

        return View(vm);
    }
}
