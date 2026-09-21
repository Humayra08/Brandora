using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Services.Payments;

// A slice of a creator's earnings and the platform-fee rate that applies to it when it
// is withdrawn. Each completed payment is one tranche at the rate LOCKED on that payment
// (Payment.CreatorFeePercent), so changing PLATFORM_COMMISSION_PERCENT later never
// touches money already earned. Earnings whose fee was already taken at settlement
// (the earlier model) are a 0% tranche.
public record FeeTranche(decimal Amount, decimal RatePercent);

// What a creator has earned and withdrawn, as the basis for a withdrawal.
//   Earned      — every completed milestone payment, as credited to the creator.
//   Withdrawn   — gross amounts of all withdrawal requests that weren't rejected.
//   FeesCharged — the fees already charged on those withdrawals.
//   Tranches    — Earned split by fee rate, cheapest first (withdrawals use the
//                 cheapest earnings first, always in the creator's favour).
public record WithdrawalLedger(decimal Earned, decimal Withdrawn, decimal FeesCharged, IReadOnlyList<FeeTranche> Tranches)
{
    public static WithdrawalLedger Empty { get; } = new(0m, 0m, 0m, Array.Empty<FeeTranche>());

    public decimal Available => Math.Max(0m, Earned - Withdrawn);
}

public record WithdrawalQuote(decimal Amount, decimal FeePercent, decimal Fee, decimal Payout);

// The influencer's earnings ledger. WalletTransaction rows record each credit; balances
// and withdrawal fees are derived from completed Payments and WithdrawalRequests so there
// is a single source of truth for both.
public class WalletService(ApplicationDbContext db, IConfiguration config)
{
    // One commission rate for both sides of the platform, from PLATFORM_COMMISSION_PERCENT.
    public decimal CommissionPercent =>
        decimal.TryParse(config["PLATFORM_COMMISSION_PERCENT"], out var pct) ? Math.Clamp(pct, 0m, 100m) : 10m;

    public async Task CreditEarningAsync(int influencerProfileId, int paymentId, decimal amount, string description)
    {
        // Idempotency guard: a settlement can be re-entered (the gateway calling back twice,
        // the Brand refreshing the callback page) — only the first credit for a given
        // Payment should ever land in the ledger.
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
            Amount = amount,
            PaymentId = paymentId,
            Description = description
        });
    }

    public async Task<decimal> GetBalanceAsync(int influencerProfileId) =>
        (await GetWithdrawalLedgerAsync(influencerProfileId)).Available;

    public async Task<WithdrawalLedger> GetWithdrawalLedgerAsync(int influencerProfileId)
    {
        var completed = await db.Payments
            .Where(p => p.Collaboration.InfluencerProfileId == influencerProfileId && p.Status == PaymentStatus.Completed)
            .Select(p => new { p.Id, p.Amount, p.NetAmount, p.PlatformFeeAmount, p.CreatorFeePercent, p.PaidAt })
            .ToListAsync();

        // Earlier model: NetAmount (after the deduction) is what was credited, and its fee
        // is already paid. Current model: the full milestone Amount is credited (NetAmount
        // == Amount) and the creator's share is due, at the payment's locked rate, when
        // withdrawn. Payments settled before rates were locked fall back to today's rate.
        decimal Credited(decimal amount, decimal net) => net > 0 ? net : amount;

        var tranches = completed
            .Select(p => new
            {
                Tranche = new FeeTranche(
                    Credited(p.Amount, p.NetAmount),
                    p.PlatformFeeAmount > 0 ? 0m : Math.Clamp(p.CreatorFeePercent ?? CommissionPercent, 0m, 100m)),
                p.PaidAt,
                p.Id
            })
            .OrderBy(x => x.Tranche.RatePercent)
            .ThenBy(x => x.PaidAt)
            .ThenBy(x => x.Id)
            .Select(x => x.Tranche)
            .ToList();

        var withdrawals = await db.WithdrawalRequests
            .Where(w => w.InfluencerProfileId == influencerProfileId && w.Status != WithdrawalStatus.Rejected)
            .Select(w => new { w.Amount, w.FeeAmount })
            .ToListAsync();

        return new WithdrawalLedger(tranches.Sum(t => t.Amount), withdrawals.Sum(w => w.Amount), withdrawals.Sum(w => w.FeeAmount), tranches);
    }

    // The creator's share of the commission, charged when they withdraw.
    //
    // Worked out on the CUMULATIVE amount withdrawn, not per request: after any withdrawal,
    // the total fee is  floor( sum of (withdrawn part of each tranche x its locked rate) )
    // minus what was already charged. So one withdrawal or many smaller ones cost exactly
    // the same, whatever the milestone amounts. Rounded DOWN to the whole taka (in the
    // creator's favour), and never more than the amount requested.
    public static WithdrawalQuote QuoteWithdrawal(WithdrawalLedger ledger, decimal amount)
    {
        decimal TotalFeeAfter(decimal cumulativeWithdrawn)
        {
            var remaining = cumulativeWithdrawn;
            var total = 0m;
            foreach (var tranche in ledger.Tranches)
            {
                if (remaining <= 0m) break;
                var taken = Math.Min(remaining, tranche.Amount);
                total += taken * tranche.RatePercent / 100m;
                remaining -= taken;
            }
            return Math.Floor(total);
        }

        var fee = Math.Max(0m, TotalFeeAfter(ledger.Withdrawn + amount) - ledger.FeesCharged);
        fee = Math.Min(fee, amount);
        var effectivePercent = amount > 0m ? Math.Round(fee / amount * 100m, 2) : 0m;

        return new WithdrawalQuote(amount, effectivePercent, fee, amount - fee);
    }
}
