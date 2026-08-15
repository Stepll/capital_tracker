using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

public record GetHoldingsByAccountQuery(Guid AccountId) : IRequest<List<HoldingDto>>;

public class GetHoldingsByAccountQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetHoldingsByAccountQuery, List<HoldingDto>>
{
    public Task<List<HoldingDto>> Handle(GetHoldingsByAccountQuery request, CancellationToken cancellationToken) =>
        HoldingQueries.WithCurrentValue(db, request.AccountId).ToListAsync(cancellationToken);
}

/// <summary>
/// Shared query shape for "holding + its most recent valuation" — a holding
/// might not have any snapshot yet (shouldn't happen in practice since
/// creation always writes one, but defensively defaults to 0).
/// </summary>
internal static class HoldingQueries
{
    public static IQueryable<HoldingDto> WithCurrentValue(IApplicationDbContext db, Guid? accountId = null)
    {
        var holdings = accountId is null
            ? db.Holdings.AsQueryable()
            : db.Holdings.Where(h => h.AccountId == accountId);

        var latestDatePerHolding = db.ValuationSnapshots
            .GroupBy(v => v.HoldingId)
            .Select(g => new { HoldingId = g.Key, Date = g.Max(v => v.Date) });

        return holdings
            .GroupJoin(
                db.ValuationSnapshots.Join(
                    latestDatePerHolding,
                    v => new { v.HoldingId, v.Date },
                    latest => new { latest.HoldingId, latest.Date },
                    (v, _) => v),
                h => h.Id,
                v => v.HoldingId,
                (h, valuations) => new { Holding = h, Valuation = valuations.FirstOrDefault() })
            .Select(x => new HoldingDto(
                x.Holding.Id,
                x.Holding.AccountId,
                x.Holding.Name,
                x.Holding.Symbol,
                x.Valuation != null ? x.Valuation.Currency : x.Holding.Account!.Currency,
                x.Valuation != null ? x.Valuation.Value : 0,
                x.Holding.CreatedAt));
    }
}
