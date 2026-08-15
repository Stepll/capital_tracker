using CapitalTracker.Application.Common;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapitalTracker.Infrastructure.MarketData;

/// <summary>
/// Fetches today's rates from NBU and upserts them into ExchangeRates.
/// Idempotent — safe to run more than once for the same day.
/// </summary>
public class ExchangeRateSyncService(
    CapitalTrackerDbContext db,
    NbuExchangeRateClient client,
    ILogger<ExchangeRateSyncService> logger)
{
    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var rates = await client.GetLatestRatesAsync(cancellationToken);

        foreach (var currency in SupportedCurrencies.Foreign)
        {
            if (!rates.TryGetValue(currency, out var rate))
            {
                logger.LogWarning("NBU response did not include a rate for {Currency}.", currency);
                continue;
            }

            var existing = await db.ExchangeRates.SingleOrDefaultAsync(
                r => r.Date == today && r.Currency == currency, cancellationToken);

            if (existing is not null)
            {
                existing.RateToUah = rate;
            }
            else
            {
                db.ExchangeRates.Add(new ExchangeRate
                {
                    Id = Guid.NewGuid(),
                    Date = today,
                    Currency = currency,
                    RateToUah = rate,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Synced exchange rates for {Date}.", today);
    }
}
