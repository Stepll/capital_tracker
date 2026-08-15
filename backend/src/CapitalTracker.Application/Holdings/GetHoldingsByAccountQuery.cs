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
/// Shared query shape for "holding + its most recent valuation". Uses a
/// correlated subquery per holding (order by date desc, take first) rather
/// than a GroupBy+GroupJoin — the latter doesn't reliably translate to SQL
/// through Npgsql's EF Core provider once composed into a larger query.
/// </summary>
internal static class HoldingQueries
{
    public static IQueryable<HoldingDto> WithCurrentValue(IApplicationDbContext db, Guid? accountId = null)
    {
        var holdings = accountId is null
            ? db.Holdings.AsQueryable()
            : db.Holdings.Where(h => h.AccountId == accountId);

        // Every holding gets an initial ValuationSnapshot at creation time (see
        // CreateHoldingCommand), so FirstOrDefault() only hits its fallback for
        // data created some other way — good enough to default to "" / 0 rather
        // than pull in the Account navigation, which broke translation here.
        return holdings.Select(h => new HoldingDto(
            h.Id,
            h.AccountId,
            h.Name,
            h.Symbol,
            db.ValuationSnapshots
                .Where(v => v.HoldingId == h.Id)
                .OrderByDescending(v => v.Date)
                .Select(v => v.Currency)
                .FirstOrDefault() ?? "",
            db.ValuationSnapshots
                .Where(v => v.HoldingId == h.Id)
                .OrderByDescending(v => v.Date)
                .Select(v => (decimal?)v.Value)
                .FirstOrDefault() ?? 0m,
            h.CreatedAt));
    }
}
