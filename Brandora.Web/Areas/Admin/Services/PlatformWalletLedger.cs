using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Areas.Admin.Services;

public record MonthAmount(string Label, decimal Amount);

public record CashoutRow(
    int Id,
    DateTime At,
    string Method,
    string Account,
    decimal Amount,
    string? GatewayReference,
    string Status,
    string? Note,
    DateTime? CompletedAt = null,
    string? AccountLabel = null)
{
    public string Code => "CO-" + Id;
}

public record SavedPayoutAccount(int Id, string Method, string Account, string Label);

public record WalletLedger(
    bool IsLive,
    decimal CompensationPaidAllTime,
    decimal CompensationThisMonth,
    decimal CompensationLastMonth,
    List<MonthAmount> CompensationByMonth,
    List<CashoutRow> Cashouts,
    Dictionary<int, string> WithdrawalGatewayReferences,
    List<SavedPayoutAccount> SavedAccounts)
{
    public decimal CashedOutTotal => Cashouts.Where(c => c.Status == "Completed").Sum(c => c.Amount);
}

// Real, on PlatformWalletTransaction (Phase 12). Both a cash-out and a dispute compensation
// are a human admin sending real money in their own bKash/Nagad app and logging the real
// reference afterwards — there is no payout API to call, so every row here is only ever
// written AFTER the money already moved, which means every row is already "Completed"; there
// is no separate pending/processing state to track.
public static class PlatformWalletLedger
{
    private static string MethodLabel(PayoutMethodKind m) => m switch
    {
        PayoutMethodKind.Bkash => "bKash",
        PayoutMethodKind.Nagad => "Nagad",
        _ => "Bank Transfer"
    };

    public static async Task<WalletLedger> LoadAsync(ApplicationDbContext db)
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastStart = monthStart.AddMonths(-1);
        var months = Enumerable.Range(0, 9).Select(i => now.AddMonths(i - 8)).ToList();

        var transactions = await db.PlatformWalletTransactions.OrderByDescending(t => t.CreatedAt).ToListAsync();
        var compensations = transactions.Where(t => t.Type == PlatformWalletTransactionType.Compensation).ToList();
        var cashouts = transactions.Where(t => t.Type == PlatformWalletTransactionType.CashOut).ToList();

        var compensationByMonth = months
            .Select(m => new MonthAmount(m.ToString("MMM"), compensations.Where(c => c.CreatedAt.Year == m.Year && c.CreatedAt.Month == m.Month).Sum(c => c.Amount)))
            .ToList();

        var cashoutRows = cashouts
            .Select(c => new CashoutRow(
                c.Id, c.CreatedAt, MethodLabel(c.Method), c.AccountDetail, c.Amount, c.GatewayReference, "Completed",
                string.IsNullOrWhiteSpace(c.Note) ? $"Processed by {c.ProcessedByAdmin}" : $"{c.Note} — processed by {c.ProcessedByAdmin}",
                c.CreatedAt))
            .ToList();

        var withdrawalRefs = await db.WithdrawalRequests
            .Where(w => w.TransactionReference != null)
            .ToDictionaryAsync(w => w.Id, w => w.TransactionReference!);

        return new WalletLedger(
            IsLive: true,
            CompensationPaidAllTime: compensations.Sum(c => c.Amount),
            CompensationThisMonth: compensations.Where(c => c.CreatedAt >= monthStart).Sum(c => c.Amount),
            CompensationLastMonth: compensations.Where(c => c.CreatedAt >= lastStart && c.CreatedAt < monthStart).Sum(c => c.Amount),
            CompensationByMonth: compensationByMonth,
            Cashouts: cashoutRows,
            WithdrawalGatewayReferences: withdrawalRefs,
            SavedAccounts: new List<SavedPayoutAccount>());
    }
}
