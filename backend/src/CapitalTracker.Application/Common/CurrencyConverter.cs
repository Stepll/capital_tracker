using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Common;

/// <summary>
/// Converts between the supported currencies using the latest known NBU rate for each,
/// anchored on UAH (the rate table never stores UAH — it doesn't rate itself).
///
/// Holdings are denominated by their valuation snapshot, not by their account, so any
/// total that spans holdings has to go through this rather than summing raw values.
/// </summary>
public class CurrencyConverter
{
    private readonly IReadOnlyDictionary<string, decimal> _toBase;

    private CurrencyConverter(IReadOnlyDictionary<string, decimal> toBase) => _toBase = toBase;

    public static CurrencyConverter FromRates(IEnumerable<ExchangeRate> rates)
    {
        var latest = rates
            .GroupBy(r => r.Currency)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.Date).First().RateToUah);

        latest[SupportedCurrencies.Base] = 1m;

        return new CurrencyConverter(latest);
    }

    public static async Task<CurrencyConverter> LoadAsync(
        IApplicationDbContext db, CancellationToken cancellationToken) =>
        FromRates(await db.ExchangeRates.ToListAsync(cancellationToken));

    /// <summary>
    /// An unknown currency — or any currency at all before the Worker's first rate sync —
    /// converts at 1:1 rather than throwing. This is a read path: showing a total that is
    /// wrong for a few minutes after a cold start beats failing every dashboard and account
    /// page until the sync lands. The trade-off is deliberate and lives only here.
    /// </summary>
    public decimal Convert(decimal value, string from, string to)
    {
        if (from == to)
        {
            return value;
        }

        var fromRate = _toBase.GetValueOrDefault(from, 1m);
        var toRate = _toBase.GetValueOrDefault(to, 1m);

        return value * fromRate / toRate;
    }
}
