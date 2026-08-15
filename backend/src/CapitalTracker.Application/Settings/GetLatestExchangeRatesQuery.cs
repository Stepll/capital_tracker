using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Settings;

public record GetLatestExchangeRatesQuery : IRequest<List<ExchangeRateDto>>;

public class GetLatestExchangeRatesQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetLatestExchangeRatesQuery, List<ExchangeRateDto>>
{
    public async Task<List<ExchangeRateDto>> Handle(
        GetLatestExchangeRatesQuery request, CancellationToken cancellationToken)
    {
        // One row per currency: the most recent date we have a rate for.
        // A join-on-max-date is used instead of GroupBy(...).First() — the
        // latter doesn't reliably translate to SQL across EF Core providers.
        var latestDatePerCurrency = db.ExchangeRates
            .GroupBy(r => r.Currency)
            .Select(g => new { Currency = g.Key, Date = g.Max(r => r.Date) });

        return await db.ExchangeRates
            .Join(
                latestDatePerCurrency,
                r => new { r.Currency, r.Date },
                latest => new { latest.Currency, latest.Date },
                (r, _) => new ExchangeRateDto(r.Currency, r.RateToUah, r.Date))
            .ToListAsync(cancellationToken);
    }
}
