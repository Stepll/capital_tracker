using CapitalTracker.Application.Common;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CapitalTracker.Infrastructure.MarketData;

/// <summary>
/// Refreshes today's valuation for every auto-priceable holding from live quotes.
/// Idempotent — safe to run more than once for the same day.
/// </summary>
public class HoldingPriceSyncService(
    CapitalTrackerDbContext db,
    FinnhubClient finnhub,
    ILogger<HoldingPriceSyncService> logger)
{
    public async Task SyncAsync(CancellationToken cancellationToken = default)
    {
        if (!finnhub.IsConfigured)
        {
            // Said out loud because the client swallows failures into "no data": without
            // this line a missing key looks exactly like "ran fine, nothing to do".
            logger.LogInformation("Finnhub API key is not configured — skipping holding price sync.");
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Flat fetch, folded in C# — the eligibility rule spans holding and account.
        var holdings = await db.Holdings.ToListAsync(cancellationToken);
        var accounts = await db.Accounts.ToDictionaryAsync(a => a.Id, cancellationToken);
        var todaysSnapshots = await db.ValuationSnapshots
            .Where(v => v.Date == today)
            .ToListAsync(cancellationToken);

        // Quantities live in the transactions now, so the whole table is folded once here
        // rather than per holding. Holdings missing from the dictionary have no unit-bearing
        // transaction at all, which reads as "units unknown" — never as zero.
        var positions = HoldingPositions.ByHolding(await db.Transactions.ToListAsync(cancellationToken));
        decimal? Position(Holding holding) =>
            positions.TryGetValue(holding.Id, out var units) ? units : null;

        var eligible = holdings
            .Where(h => accounts.ContainsKey(h.AccountId)
                && MarketPricing.CanAutoPrice(h.Symbol, accounts[h.AccountId].Type, Position(h)))
            .ToList();

        var priced = 0;
        var skippedManual = 0;
        var withoutQuote = 0;

        // Grouped by symbol so the same ticker held in two accounts costs one request.
        foreach (var group in eligible.GroupBy(h => h.Symbol!, StringComparer.OrdinalIgnoreCase))
        {
            var targets = group
                .Where(h =>
                {
                    var existing = todaysSnapshots.SingleOrDefault(v => v.HoldingId == h.Id);
                    if (existing?.IsManual != true)
                    {
                        return true;
                    }

                    // Never overwrite a number the user typed today.
                    skippedManual++;
                    return false;
                })
                .ToList();

            if (targets.Count == 0)
            {
                continue;
            }

            var quote = await finnhub.GetQuoteAsync(group.Key, cancellationToken);
            if (quote is null)
            {
                logger.LogWarning("No quote for {Symbol} — leaving its holdings at their last known value.", group.Key);
                withoutQuote += targets.Count;
                continue;
            }

            foreach (var holding in targets)
            {
                // Rounded to the column's scale so the tracked entity matches what's stored.
                var value = Math.Round(quote.Price * Position(holding)!.Value, 2);
                var existing = todaysSnapshots.SingleOrDefault(v => v.HoldingId == holding.Id);

                if (existing is not null)
                {
                    existing.Value = value;
                    existing.Currency = FinnhubClient.QuoteCurrency;
                }
                else
                {
                    db.ValuationSnapshots.Add(new ValuationSnapshot
                    {
                        Id = Guid.NewGuid(),
                        HoldingId = holding.Id,
                        Date = today,
                        Value = value,
                        // The quote's own currency, not the account's — a USD stock can sit
                        // in a UAH account, and totals convert at read time.
                        Currency = FinnhubClient.QuoteCurrency,
                        IsManual = false,
                    });
                }

                priced++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        // One summary line per run: with per-symbol failures logged as warnings and the
        // rest silent, this is what distinguishes "working" from "quietly doing nothing".
        logger.LogInformation(
            "Holding price sync for {Date}: {Priced} priced, {SkippedManual} left as manual, {WithoutQuote} without a quote.",
            today, priced, skippedManual, withoutQuote);
    }
}
