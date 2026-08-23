using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Insights;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CapitalTracker.Application.Holdings;

public record GetHoldingByIdQuery(Guid Id) : IRequest<HoldingDetailDto?>;

public class GetHoldingByIdQueryHandler(IApplicationDbContext db, IOptions<InsightsOptions> insightsOptions)
    : IRequestHandler<GetHoldingByIdQuery, HoldingDetailDto?>
{
    public async Task<HoldingDetailDto?> Handle(GetHoldingByIdQuery request, CancellationToken cancellationToken)
    {
        // Past the soft-delete filter on purpose: a deleted holding still has to open, or
        // every link to it — from the analysis archive above all — turns into a 404. The
        // DTO says it is deleted and the page renders read-only.
        var holding = await db.Holdings
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(h => h.Id == request.Id, cancellationToken);
        if (holding is null)
            return null;

        var account = await db.Accounts
            .IgnoreQueryFilters()
            .SingleAsync(a => a.Id == holding.AccountId, cancellationToken);

        var sectorName = holding.SectorId is null
            ? null
            : (await db.Sectors.SingleOrDefaultAsync(s => s.Id == holding.SectorId, cancellationToken))?.Name;

        // Fetched flat and sorted client-side — same reasoning as the dashboard
        // summary: small data volume, and OrderBy-after-Select-into-DTO doesn't
        // reliably translate through EF Core anyway.
        var snapshots = (await db.ValuationSnapshots
                .Where(v => v.HoldingId == holding.Id)
                .ToListAsync(cancellationToken))
            .OrderBy(v => v.Date)
            .ToList();

        // Units held, folded from the transactions — the holding carries no quantity of
        // its own any more, so this is what the header and PricingMode below read.
        var quantity = HoldingPositions.Of(await db.Transactions
            .Where(t => t.HoldingId == holding.Id)
            .ToListAsync(cancellationToken));

        var latest = snapshots.LastOrDefault();
        var currency = latest?.Currency ?? account.Currency;

        // History can be mixed-currency — a UAH figure entered by hand before the holding
        // was recognised as USD-denominated, then USD rows from the price job. The chart
        // is labelled with one currency, so the series is converted into the current one,
        // each point at the rate in effect on its own date. (A single-currency series
        // never reaches the rate table at all — the conversion short-circuits.)
        var converter = await CurrencyConverter.LoadAsync(db, cancellationToken);

        var lastAnalysedAt = await db.AiInsights
            .Where(i => i.HoldingId == holding.Id)
            .MaxAsync(i => (DateTime?)i.GeneratedAt, cancellationToken);

        var cooldown = TimeSpan.FromHours(insightsOptions.Value.CooldownHours);
        var nextAnalysisAvailableAt = lastAnalysedAt is null || DateTime.UtcNow - lastAnalysedAt.Value >= cooldown
            ? null
            : (DateTime?)lastAnalysedAt.Value.Add(cooldown);

        return new HoldingDetailDto(
            holding.Id,
            holding.AccountId,
            account.Name,
            account.Type,
            holding.Name,
            holding.Symbol,
            quantity,
            holding.Notes,
            currency,
            latest?.Value ?? 0m,
            holding.SectorId,
            sectorName,
            holding.CreatedAt,
            snapshots
                .Select(v => new ValuationPointDto(v.Date, converter.ConvertAsOf(v.Value, v.Currency, currency, v.Date)))
                .ToList(),
            holding.Attributes,
            holding.SecretAttributes.Keys.ToList(),
            MarketPricing.CanQuote(holding.Symbol, account.Type)
                ? MarketPricing.CanAutoPrice(holding.Symbol, account.Type, quantity)
                    ? PricingMode.Automatic
                    : PricingMode.NeedsQuantity
                : PricingMode.Manual,
            holding.ExcludeFromAiAnalysis,
            nextAnalysisAvailableAt,
            holding.DeletedAt);
    }
}
