using CapitalTracker.Application.Common;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapitalTracker.Infrastructure.MarketData;

/// <summary>
/// Fills holes in the rate history, from the oldest valuation we hold up to today.
///
/// The daily sync only ever writes today's rate, so the table starts on the day the
/// Worker was first deployed and loses a day for every day the Worker is down. Charts
/// convert each point at the rate in effect on its date, and a date we have no rate for
/// falls back to the nearest one we do — so those holes show up as quietly wrong history
/// rather than as an error. This closes them.
///
/// Idempotent, and free when there is nothing to do: the gap check runs against the
/// database first and skips the HTTP call entirely when the range is already complete.
/// </summary>
public class ExchangeRateBackfillService(
    CapitalTrackerDbContext db,
    NbuExchangeRateClient client,
    ILogger<ExchangeRateBackfillService> logger)
{
    public async Task BackfillAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Nothing older than the oldest valuation can ever need converting.
        var earliest = await db.ValuationSnapshots.MinAsync(v => (DateOnly?)v.Date, cancellationToken);
        if (earliest is null || earliest > today)
        {
            logger.LogInformation("No valuations to cover — skipping exchange rate backfill.");
            return;
        }

        var start = earliest.Value;
        var known = (await db.ExchangeRates
                .Where(r => r.Date >= start && r.Date <= today)
                .ToListAsync(cancellationToken))
            .Select(r => (r.Date, r.Currency))
            .ToHashSet();

        var allDates = Enumerable
            .Range(0, today.DayNumber - start.DayNumber + 1)
            .Select(start.AddDays)
            .ToList();

        var added = 0;

        foreach (var currency in SupportedCurrencies.Foreign)
        {
            var missing = allDates.Count(d => !known.Contains((d, currency)));
            if (missing == 0)
            {
                continue;
            }

            // One request per currency for the whole span rather than one per missing
            // day: a fresh install backfilling a year of history is two calls, not 700.
            var rates = await client.GetPeriodRatesAsync(currency, start, today, cancellationToken);
            if (rates.Count == 0)
            {
                logger.LogWarning(
                    "NBU returned no {Currency} rates for {Start}..{End} — {Missing} dates stay uncovered.",
                    currency, start, today, missing);
                continue;
            }

            // Range-guarded as well as gap-guarded: `known` only covers [start, today], so
            // a row outside it would be inserted unchecked and could collide with the
            // unique (Date, Currency) index, failing the whole save.
            foreach (var rate in rates.Where(r =>
                r.Date >= start && r.Date <= today && !known.Contains((r.Date, currency))))
            {
                db.ExchangeRates.Add(new ExchangeRate
                {
                    Id = Guid.NewGuid(),
                    Date = rate.Date,
                    Currency = currency,
                    RateToUah = rate.Rate,
                });
                added++;
            }
        }

        if (added == 0)
        {
            logger.LogInformation("Exchange rate history is already complete from {Start}.", start);
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Backfilled {Added} exchange rates covering {Start}..{End}.", added, start, today);
    }
}
