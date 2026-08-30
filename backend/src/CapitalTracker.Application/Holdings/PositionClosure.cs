using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

/// <summary>
/// Keeps a sold-out holding from going on counting.
///
/// The bug this exists for: selling everything left the position at zero while the value
/// stayed at whatever the last valuation said, so the asset kept its weight in the account
/// total, in the net worth and in the allocation donut — and nothing corrected it, because
/// the price job skips a holding with no units.
///
/// Rather than teaching every read path to check the position — the history would have to
/// fold transactions per date to do it — closing a position simply writes what is true:
/// a valuation of zero on the day it closed. Every existing figure then comes out right on
/// its own, the chart drops to zero on the right date instead of retroactively, and the
/// export carries it like any other valuation.
/// </summary>
public static class PositionClosure
{
    /// <summary>
    /// Called after anything that changes a holding's transactions. Adding the zero is the
    /// whole job; the removal below is only for taking back a closure that stopped being true.
    /// </summary>
    public static async Task SyncAsync(IApplicationDbContext db, Guid holdingId, CancellationToken cancellationToken)
    {
        var transactions = await db.Transactions
            .Where(t => t.HoldingId == holdingId)
            .ToListAsync(cancellationToken);

        var unitBearing = transactions.Where(t => HoldingPositions.Direction(t.Type) != 0).ToList();
        if (unitBearing.Count == 0)
            return;

        var position = HoldingPositions.Of(transactions);
        var lastMoved = unitBearing.Max(t => t.Date);

        var snapshots = await db.ValuationSnapshots
            .Where(v => v.HoldingId == holdingId)
            .ToListAsync(cancellationToken);

        if (position == 0m)
        {
            // Zero wins over whatever is dated that day. The normal case is that something
            // is already there — the price job writes one daily while the position is open,
            // and creating a holding writes one too — so declining to overwrite would leave
            // the sold asset counting, which is the entire bug.
            var onClosingDay = snapshots.FirstOrDefault(v => v.Date == lastMoved);

            // Anything dated after the close describes a holding that was no longer held:
            // a sale entered days late leaves exactly this trail behind it.
            foreach (var later in snapshots.Where(v => v.Date > lastMoved))
            {
                later.Value = 0m;
            }

            if (onClosingDay is not null)
            {
                onClosingDay.Value = 0m;
                return;
            }

            var holding = await db.Holdings
                .IgnoreQueryFilters()
                .SingleAsync(h => h.Id == holdingId, cancellationToken);
            var account = await db.Accounts
                .IgnoreQueryFilters()
                .SingleAsync(a => a.Id == holding.AccountId, cancellationToken);

            db.ValuationSnapshots.Add(new ValuationSnapshot
            {
                Id = Guid.NewGuid(),
                HoldingId = holdingId,
                Date = lastMoved,
                Value = 0m,
                Currency = HoldingDenomination.Of(snapshots, account),
                // Not the owner's typing, and not a market price either — but IsManual keeps
                // the price job from treating this day as its own to overwrite.
                IsManual = true,
            });

            return;
        }

        // The position is open again. A zero dated on or after the last thing that moved
        // units can only be a closure that no longer holds — the sale behind it was deleted
        // or edited away. A zero from an earlier closure stays: on that day it was true.
        var stale = snapshots.Where(v => v.Value == 0m && v.Date >= lastMoved).ToList();
        if (stale.Count > 0)
            db.ValuationSnapshots.RemoveRange(stale);
    }

    /// <summary>
    /// When the position last went to zero, and what the closing transaction was worth —
    /// what the page shows instead of a value that is no longer there.
    /// </summary>
    public static (DateOnly Date, decimal Amount, string Currency)? Closure(IReadOnlyCollection<Transaction> transactions)
    {
        if (HoldingPositions.Of(transactions) != 0m)
            return null;

        var closing = transactions
            .Where(t => HoldingPositions.Direction(t.Type) != 0)
            .OrderBy(t => t.Date)
            .LastOrDefault();

        return closing is null
            ? null
            : (closing.Date, Math.Round(closing.Quantity * closing.UnitPrice, 2), closing.Currency);
    }
}
