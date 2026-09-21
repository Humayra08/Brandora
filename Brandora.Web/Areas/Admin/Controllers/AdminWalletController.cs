using Brandora.Web.Areas.Admin.Services;
using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Controllers;

public class AdminWalletController(ApplicationDbContext db) : AdminControllerBase(db)
{
    public async Task<IActionResult> Index(string? search, DateTime? from, DateTime? to)
    {
        await LoadAdminChromeAsync();
        ViewData["ActiveNav"] = "Wallet";
        ViewData["Title"] = "Platform Wallet";
        ViewData["Breadcrumb"] = new List<(string, string?)> { ("Wallet", null), ("Platform Wallet", null) };

        var ledger = await PlatformWalletLedger.LoadAsync(db);

        var milestones = await db.Milestones
            .Include(m => m.Payment)
            .Include(m => m.Collaboration).ThenInclude(c => c.Campaign).ThenInclude(c => c.BrandProfile)
            .ToListAsync();

        bool InRange(Payment p)
        {
            var at = p.PaidAt ?? p.CreatedAt;
            return (from is null || at >= from) && (to is null || at < to.Value.AddDays(1));
        }

        var rows = milestones
            .GroupBy(m => m.Collaboration.Campaign)
            .Select(g =>
            {
                var campaign = g.Key;
                var paid = g.Where(m => m.Payment is { Status: PaymentStatus.Completed } p && InRange(p))
                    .Sum(m => m.Payment!.Amount);
                var collected = ledger.FeesCollectedByCampaign.GetValueOrDefault(campaign.Id);
                var expected = Math.Max(0m, PlatformFee.Of(paid) - collected);
                var anyPending = g.Any(m => m.Payment is { Status: PaymentStatus.Pending });
                var allPaid = g.All(m => m.Status == MilestoneStatus.Paid);

                var (label, tone) = allPaid
                    ? ("Completed", "gray")
                    : anyPending ? ("Some Pending", "amber") : ("In Progress", "blue");

                return new WalletCampaignRow(
                    campaign.Id,
                    campaign.Title,
                    campaign.BrandProfile.CompanyName,
                    campaign.BrandProfile.ProfilePictureUrl,
                    g.Count(),
                    g.Sum(m => m.Amount),
                    paid,
                    collected,
                    expected,
                    label,
                    tone);
            })
            .Where(r => string.IsNullOrWhiteSpace(search)
                || r.Title.Contains(search, StringComparison.OrdinalIgnoreCase)
                || r.BrandName.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.Gross)
            .ToList();

        var processedWithdrawals = await db.WithdrawalRequests
            .CountAsync(w => w.Status == WithdrawalStatus.Approved || w.Status == WithdrawalStatus.Paid);

        var vm = new AdminWalletViewModel
        {
            Ledger = ledger,
            Campaigns = rows,
            Search = search ?? "",
            From = from,
            To = to,
            WithdrawalsProcessed = processedWithdrawals,
            FeesExpected = rows.Sum(r => r.FeeExpected),
            OpenDisputes = await db.Disputes.CountAsync(d => d.Status != DisputeStatus.Resolved)
        };

        return View(vm);
    }

    // Loaded into the right-hand drawer when "View" is clicked on a Campaign-wise Fee Income row.
    public async Task<IActionResult> CampaignFees(int id)
    {
        var campaign = await db.Campaigns
            .Include(c => c.BrandProfile)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (campaign is null) return NotFound();

        var ledger = await PlatformWalletLedger.LoadAsync(db);

        var milestones = await db.Milestones
            .Include(m => m.Payment)
            .Include(m => m.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Where(m => m.Collaboration.CampaignId == id)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

        var collected = ledger.FeesCollectedByCampaign.GetValueOrDefault(id);
        var paid = milestones.Where(m => m.Payment is { Status: PaymentStatus.Completed }).Sum(m => m.Payment!.Amount);

        var lines = milestones.Select((m, i) =>
        {
            var isPaid = m.Payment is { Status: PaymentStatus.Completed };
            return new FeeMilestoneRow(
                i + 1,
                m.Title,
                m.ContentType,
                m.Collaboration.InfluencerProfile.FullName,
                m.Amount,
                PlatformFee.Of(m.Amount),
                isPaid ? "Expected" : "Awaiting payment");
        }).ToList();

        var influencers = milestones
            .Select(m => m.Collaboration.InfluencerProfile)
            .DistinctBy(i => i.Id)
            .Select(i => i.FullName)
            .ToList();

        var vm = new CampaignFeeDrawerViewModel
        {
            CampaignId = campaign.Id,
            Title = campaign.Title,
            Subtitle = campaign.Description,
            Status = campaign.Status,
            BrandName = campaign.BrandProfile.CompanyName,
            BrandPicture = campaign.BrandProfile.ProfilePictureUrl,
            BrandProfileId = campaign.BrandProfileId,
            Platform = campaign.Platform,
            StartDate = campaign.StartDate,
            Deadline = campaign.Deadline,
            Gross = milestones.Sum(m => m.Amount),
            Paid = paid,
            FeeCollected = collected,
            FeeExpected = Math.Max(0m, PlatformFee.Of(paid) - collected),
            Milestones = lines,
            Influencers = influencers,
            Transactions = ledger.Transactions.Where(t => t.CampaignId == id).ToList(),
            LedgerIsLive = ledger.IsLive
        };

        return PartialView("_CampaignFeeDrawer", vm);
    }
}

public record WalletCampaignRow(
    int CampaignId,
    string Title,
    string BrandName,
    string? BrandPicture,
    int MilestoneCount,
    decimal Gross,
    decimal Paid,
    decimal FeeCollected,
    decimal FeeExpected,
    string StatusLabel,
    string StatusTone);

public class AdminWalletViewModel
{
    public WalletLedger Ledger { get; set; } = null!;
    public List<WalletCampaignRow> Campaigns { get; set; } = new();
    public string Search { get; set; } = "";
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int WithdrawalsProcessed { get; set; }
    public decimal FeesExpected { get; set; }
    public int OpenDisputes { get; set; }
}

public record FeeMilestoneRow(int No, string Title, string? Type, string InfluencerName, decimal Amount, decimal Fee, string State);

public class CampaignFeeDrawerViewModel
{
    public int CampaignId { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public CampaignStatus Status { get; set; }
    public string BrandName { get; set; } = "";
    public string? BrandPicture { get; set; }
    public int BrandProfileId { get; set; }
    public string? Platform { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? Deadline { get; set; }
    public decimal Gross { get; set; }
    public decimal Paid { get; set; }
    public decimal FeeCollected { get; set; }
    public decimal FeeExpected { get; set; }
    public List<FeeMilestoneRow> Milestones { get; set; } = new();
    public List<string> Influencers { get; set; } = new();
    public List<WalletTransactionRow> Transactions { get; set; } = new();
    public bool LedgerIsLive { get; set; }
}
