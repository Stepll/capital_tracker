using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Insights;

/// <summary>
/// The holdings as the model is allowed to see them: positions, values converted into the
/// display currency, and the findings from each asset's own latest analysis. Shared by the
/// portfolio and market scopes — one is about this shape, the other needs it to answer
/// "where should money go" for someone who already holds all this.
/// </summary>
internal record PortfolioContext(
    string DisplayCurrency,
    decimal TotalValue,
    IReadOnlyList<PortfolioHoldingSummary> Holdings,
    int ExcludedHoldingCount)
{
    public static async Task<PortfolioContext> BuildAsync(
        IApplicationDbContext db, Guid userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleAsync(u => u.Id == userId, cancellationToken);
        var converter = await CurrencyConverter.LoadAsync(db, cancellationToken);

        // Flat fetches folded in C# — same reasoning as the dashboard summary. Deleted
        // holdings are filtered out globally, which is what we want: this is what is held.
        var holdings = await db.Holdings.ToListAsync(cancellationToken);
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id, cancellationToken);
        var snapshots = await db.ValuationSnapshots.ToListAsync(cancellationToken);
        var holdingInsights = await db.AiInsights
            .Where(i => i.Scope == InsightScope.Holding)
            .ToListAsync(cancellationToken);

        var snapshotsByHolding = snapshots.ToLookup(s => s.HoldingId);
        var insightsByHolding = holdingInsights
            .Where(i => i.HoldingId is not null)
            .ToLookup(i => i.HoldingId!.Value);

        var analysable = holdings
            .Where(h => !h.ExcludeFromAiAnalysis && accounts.ContainsKey(h.AccountId))
            .ToList();

        var summaries = analysable
            .Select(h =>
            {
                var account = accounts[h.AccountId];
                var latest = snapshotsByHolding[h.Id].OrderByDescending(s => s.Date).FirstOrDefault();
                var currency = latest?.Currency ?? account.Currency;
                var value = latest?.Value ?? 0m;

                return new PortfolioHoldingSummary(
                    h.Name,
                    h.Symbol,
                    account.Type,
                    account.Name,
                    value,
                    currency,
                    converter.Convert(value, currency, user.DisplayCurrency),
                    h.Quantity,
                    insightsByHolding[h.Id]
                        .OrderByDescending(i => i.GeneratedAt)
                        .FirstOrDefault()
                        ?.ToDto().Facts ?? []);
            })
            .OrderByDescending(h => h.ValueInDisplayCurrency)
            .ToList();

        return new PortfolioContext(
            user.DisplayCurrency,
            summaries.Sum(h => h.ValueInDisplayCurrency),
            summaries,
            holdings.Count - analysable.Count);
    }

    /// <summary>The most recent analysis in one scope, fed back so the model can mark repeats.</summary>
    public static async Task<PreviousAnalysis?> LatestAsync(
        IApplicationDbContext db, InsightScope scope, CancellationToken cancellationToken)
    {
        var previous = (await db.AiInsights
                .Where(i => i.Scope == scope)
                .ToListAsync(cancellationToken))
            .OrderByDescending(i => i.GeneratedAt)
            .FirstOrDefault();

        return previous is null ? null : new PreviousAnalysis(previous.GeneratedAt, previous.ToDto().Facts);
    }
}
