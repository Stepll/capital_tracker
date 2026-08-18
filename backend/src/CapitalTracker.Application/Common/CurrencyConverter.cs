using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Common;

/// <summary>
/// Converts between the supported currencies using NBU rates, anchored on UAH
/// (the rate table never stores UAH — it doesn't rate itself).
///
/// Holdings are denominated by their valuation snapshot, not by their account, so any
/// total that spans holdings has to go through this rather than summing raw values.
///
/// Current figures use <see cref="Convert"/> (the latest rate); anything plotted over
/// time uses <see cref="ConvertAsOf"/>, or today's rate would silently rewrite the past
/// every time the hryvnia moves.
/// </summary>
public class CurrencyConverter
{
    private readonly IReadOnlyDictionary<string, (DateOnly Date, decimal Rate)[]> _toBase;

    private CurrencyConverter(IReadOnlyDictionary<string, (DateOnly, decimal)[]> toBase) => _toBase = toBase;

    public static CurrencyConverter FromRates(IEnumerable<ExchangeRate> rates)
    {
        var byCurrency = rates
            .GroupBy(r => r.Currency)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(r => r.Date).Select(r => (r.Date, r.RateToUah)).ToArray());

        byCurrency[SupportedCurrencies.Base] = [(DateOnly.MinValue, 1m)];

        return new CurrencyConverter(byCurrency);
    }

    public static async Task<CurrencyConverter> LoadAsync(
        IApplicationDbContext db, CancellationToken cancellationToken) =>
        FromRates(await db.ExchangeRates.ToListAsync(cancellationToken));

    /// <summary>
    /// Converts at the most recent known rate — for "what is it worth now" figures.
    /// </summary>
    public decimal Convert(decimal value, string from, string to) =>
        ConvertAsOf(value, from, to, DateOnly.MaxValue);

    /// <summary>
    /// Converts at the rate that was in effect on <paramref name="asOf"/> — the last rate
    /// dated at or before it, so weekends and holidays (NBU publishes nothing on those)
    /// carry the previous working day's rate forward.
    ///
    /// Two fallbacks, both deliberate, both living only here:
    /// an unknown currency — or any currency at all before the Worker's first rate sync —
    /// converts at 1:1 rather than throwing, because this is a read path and a total that
    /// is wrong for a few minutes after a cold start beats failing every dashboard load;
    /// and a date earlier than the oldest rate we hold uses that oldest rate, because
    /// falling back to 1:1 there would draw a USD holding's early history at a fortieth
    /// of its value — a far louder lie than a slightly stale rate.
    /// </summary>
    public decimal ConvertAsOf(decimal value, string from, string to, DateOnly asOf)
    {
        if (from == to)
        {
            return value;
        }

        return value * RateAsOf(from, asOf) / RateAsOf(to, asOf);
    }

    private decimal RateAsOf(string currency, DateOnly asOf)
    {
        if (!_toBase.TryGetValue(currency, out var series) || series.Length == 0)
        {
            return 1m;
        }

        // Ascending by date, a handful of rows per year — a scan from the end finds the
        // rate for a recent date immediately, which is what the dashboard mostly asks for.
        for (var i = series.Length - 1; i >= 0; i--)
        {
            if (series[i].Date <= asOf)
            {
                return series[i].Rate;
            }
        }

        return series[0].Rate;
    }
}
