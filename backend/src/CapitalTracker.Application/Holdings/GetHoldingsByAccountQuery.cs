using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

public record GetHoldingsByAccountQuery(Guid AccountId) : IRequest<List<HoldingDto>>;

public class GetHoldingsByAccountQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetHoldingsByAccountQuery, List<HoldingDto>>
{
    public async Task<List<HoldingDto>> Handle(
        GetHoldingsByAccountQuery request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleAsync(a => a.Id == request.AccountId, cancellationToken);
        var holdings = await HoldingQueries.WithCurrentValue(db, request.AccountId).ToListAsync(cancellationToken);

        return await HoldingQueries.WithValuationAgeAsync(db, holdings, account, cancellationToken);
    }
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
            h.CreatedAt,
            db.ValuationSnapshots
                .Where(v => v.HoldingId == h.Id)
                .OrderByDescending(v => v.Date)
                .Select(v => (DateOnly?)v.Date)
                .FirstOrDefault(),
            // Spelled out rather than left to the parameter's default: an expression tree
            // can't call a constructor with optional arguments omitted.
            null));
    }

    /// <summary>
    /// Fills in how stale each holding's value is. Done here rather than in the projection
    /// because the answer needs the account type and the holding's position, and neither
    /// belongs in a query that has to stay translatable.
    /// </summary>
    public static async Task<List<HoldingDto>> WithValuationAgeAsync(
        IApplicationDbContext db,
        List<HoldingDto> holdings,
        Account account,
        CancellationToken cancellationToken)
    {
        var ids = holdings.Select(h => h.Id).ToList();
        var positions = HoldingPositions.ByHolding(await db.Transactions
            .Where(t => ids.Contains(t.HoldingId))
            .ToListAsync(cancellationToken));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return holdings
            .Select(h => h with
            {
                ValuationAge = ValuationFreshness.Age(
                    h.LastValuedOn,
                    account.Type,
                    MarketPricing.ModeFor(
                        h.Symbol, account.Type, positions.TryGetValue(h.Id, out var units) ? units : null),
                    today),
            })
            .ToList();
    }
}
