using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Services.Payments;

// The influencer's earnings ledger. WalletTransaction rows are the sole source of truth
// for how much an influencer has actually earned — CreditEarningAsync is the only place
// that writes an Earning row, always from a completed Payment's NetAmount (after platform
// commission), never the gross Amount, and never as an edit to an existing row.
public class WalletService(ApplicationDbContext db)
{
    public async Task CreditEarningAsync(int influencerProfileId, int paymentId, decimal netAmount, string description)
    {
        // Idempotency guard: a settlement can be re-entered (bKash calling back twice, the
        // Brand refreshing the callback page) — only the first credit for a given Payment
        // should ever land in the ledger.
        var alreadyCredited = await db.WalletTransactions
            .AnyAsync(w => w.PaymentId == paymentId && w.Type == WalletTransactionType.Earning);

        if (alreadyCredited)
        {
            return;
        }

        db.WalletTransactions.Add(new WalletTransaction
        {
            InfluencerProfileId = influencerProfileId,
            Type = WalletTransactionType.Earning,
            Amount = netAmount,
            PaymentId = paymentId,
            Description = description
        });
    }

    public async Task<decimal> GetBalanceAsync(int influencerProfileId)
    {
        return await db.WalletTransactions
            .Where(w => w.InfluencerProfileId == influencerProfileId)
            .SumAsync(w => (decimal?)w.Amount) ?? 0m;
    }
}
