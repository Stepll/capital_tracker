using CapitalTracker.Domain.Entities;

namespace CapitalTracker.Application.Common;

public record ValuationHistoryPoint(DateOnly Date, decimal Value);

/// <summary>
/// "What was this set of holdings worth on each date something was recorded" — the series
/// behind both the dashboard's net worth line and one account's own chart.
///
/// Shared because the rules it encodes are the expensive kind to relearn: the rate belongs
/// to the point on the chart rather than to the snapshot, deleted holdings still count for
/// the dates they were actually held, and today is always the last point. A second copy
/// would drift from this one on whichever of those it forgot.
/// </summary>
public static class ValuationHistory
{
    /// <summary>
    /// <paramref name="holdings"/> must include soft-deleted ones — they are counted up to
    /// their DeletedAt. <paramref name="snapshots"/> are theirs, and also supply the dates
    /// the series is sampled on.
    /// </summary>
    public static List<ValuationHistoryPoint> Build(
        IReadOnlyCollection<Holding> holdings,
        IReadOnlyCollection<ValuationSnapshot> snapshots,
        CurrencyConverter converter,
        string targetCurrency)
    {
        var snapshotsByHolding = snapshots.ToLookup(s => s.HoldingId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Today is always a point, even with nothing dated today, so the line ends where the
        // headline total does instead of stopping at the last recorded date and disagreeing
        // with it by whatever the rate has done since.
        var dates = snapshots.Select(s => s.Date).Append(today).Distinct().OrderBy(d => d);

        return dates
            .Select(date => new ValuationHistoryPoint(date, holdings.Where(h => HeldOn(h, date)).Sum(h =>
            {
                var asOf = snapshotsByHolding[h.Id]
                    .Where(s => s.Date <= date)
                    .OrderByDescending(s => s.Date)
                    .FirstOrDefault();

                // Converted at that day's rate, not today's: pricing the whole line at the
                // current rate redraws it every time the hryvnia moves, turning a currency
                // slide into apparent growth. A valuation carried forward for three months
                // is still being asked "what was that worth on this date".
                return asOf is null ? 0m : converter.ConvertAsOf(asOf.Value, asOf.Currency, targetCurrency, date);
            })))
            .ToList();
    }

    private static bool HeldOn(Holding holding, DateOnly date) =>
        holding.DeletedAt is null || date < DateOnly.FromDateTime(holding.DeletedAt.Value);
}
