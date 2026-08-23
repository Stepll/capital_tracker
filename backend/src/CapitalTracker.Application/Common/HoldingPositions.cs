using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Common;

/// <summary>
/// How many units of a holding are held, folded out of its transactions — the holding
/// itself no longer stores a quantity, so this is the only answer to that question.
/// Shared for the same reason as <see cref="MarketPricing"/>: the price job, the holding
/// page and both AI prompts all need the position, and a second implementation would
/// eventually disagree with the first about what, say, a Deposit does to it.
/// </summary>
public static class HoldingPositions
{
    /// <summary>
    /// What one transaction does to a position. Dividends, rent and expenses are cash
    /// flows — money moves, units don't — so they leave the position exactly as it was.
    /// </summary>
    public static int Direction(TransactionType type) => type switch
    {
        TransactionType.Buy or TransactionType.Deposit => 1,
        TransactionType.Sell or TransactionType.Withdrawal => -1,
        _ => 0,
    };

    /// <summary>
    /// Units held, or null when nothing has ever moved units — which is not the same as
    /// zero. Null means "this asset isn't counted in units" (an apartment, a deposit) and
    /// preserves what the old nullable Holding.Quantity meant: the page hides the unit
    /// line, and the price job refuses to multiply a quote by a number nobody gave it.
    /// </summary>
    public static decimal? Of(IEnumerable<Transaction> transactions)
    {
        decimal? position = null;

        foreach (var transaction in transactions)
        {
            var direction = Direction(transaction.Type);
            if (direction == 0)
                continue;

            position = (position ?? 0m) + direction * transaction.Quantity;
        }

        return position;
    }

    /// <summary>
    /// Positions for a whole set of transactions at once, for the callers that fetch flat
    /// and fold in memory. Holdings with no unit-bearing transaction are simply absent —
    /// the same meaning as the null from <see cref="Of"/>.
    /// </summary>
    public static Dictionary<Guid, decimal> ByHolding(IEnumerable<Transaction> transactions) =>
        transactions
            .GroupBy(t => t.HoldingId)
            .Select(group => (group.Key, Position: Of(group)))
            .Where(entry => entry.Position is not null)
            .ToDictionary(entry => entry.Key, entry => entry.Position!.Value);
}
