using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Dashboard;

public record GetDashboardSummaryQuery(Guid UserId) : IRequest<DashboardSummaryDto>;

// All aggregation here happens in memory rather than via EF LINQ (GroupBy,
// OrderBy-after-Select, etc.) — those repeatedly failed to translate to SQL
// through Npgsql once composed (see git history). Data volume for a personal
// finance app is tiny, so fetching flat tables and folding in C# is both
// simpler and safer than fighting the query translator.
public class GetDashboardSummaryQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetDashboardSummaryQuery, DashboardSummaryDto>
{
    public async Task<DashboardSummaryDto> Handle(GetDashboardSummaryQuery request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleAsync(u => u.Id == request.UserId, cancellationToken);
        var displayCurrency = user.DisplayCurrency;

        var converter = await CurrencyConverter.LoadAsync(db, cancellationToken);

        decimal ToDisplay(decimal value, string currency) =>
            converter.Convert(value, currency, displayCurrency);

        var accounts = await db.Accounts.ToListAsync(cancellationToken);
        var snapshots = await db.ValuationSnapshots.ToListAsync(cancellationToken);

        // The one read that deliberately looks past the soft-delete filter. Deleted
        // holdings are excluded from every current figure below, but the history has to
        // keep them for the dates they were actually held — dropping them there is the
        // very rewriting of the past that soft deletion exists to prevent.
        var allHoldings = await db.Holdings.IgnoreQueryFilters().ToListAsync(cancellationToken);
        var holdings = allHoldings.Where(h => h.DeletedAt is null).ToList();

        var accountById = accounts.ToDictionary(a => a.Id);
        var snapshotsByHolding = snapshots.ToLookup(s => s.HoldingId);

        // Current allocation: latest snapshot per holding, grouped by account type.
        var latestPerHolding = holdings
            .Select(h => new
            {
                Holding = h,
                Snapshot = snapshotsByHolding[h.Id].OrderByDescending(s => s.Date).FirstOrDefault(),
            })
            .Where(x => x.Snapshot is not null && accountById.ContainsKey(x.Holding.AccountId))
            .ToList();

        var allocation = latestPerHolding
            .GroupBy(x => accountById[x.Holding.AccountId].Type)
            .Select(g => new AllocationItemDto(g.Key.ToString(), g.Sum(x => ToDisplay(x.Snapshot!.Value, x.Snapshot!.Currency))))
            .OrderByDescending(a => a.Value)
            .ToList();

        var total = allocation.Sum(a => a.Value);

        // The total above is only as true as the numbers it adds up, so it comes with a
        // list of the ones that have gone stale. Positions are needed to tell an asset the
        // price job should be handling from one only the owner can update — the difference
        // decides which of two very different sentences the dashboard shows.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var positions = HoldingPositions.ByHolding(await db.Transactions.ToListAsync(cancellationToken));

        var stale = holdings
            .Where(h => accountById.ContainsKey(h.AccountId))
            .Select(h =>
            {
                var account = accountById[h.AccountId];
                var snapshot = snapshotsByHolding[h.Id].OrderByDescending(s => s.Date).FirstOrDefault();
                var mode = MarketPricing.ModeFor(
                    h.Symbol, account.Type, positions.TryGetValue(h.Id, out var units) ? units : null);

                return new StaleValuationDto(
                    h.Id,
                    h.Name,
                    account.Name,
                    ValuationFreshness.Age(snapshot?.Date, account.Type, mode, today),
                    snapshot is null ? 0m : ToDisplay(snapshot.Value, snapshot.Currency));
            })
            .Where(s => s.ValuationAge.Status != ValuationStatus.Fresh)
            .OrderByDescending(s => s.ValueInDisplayCurrency)
            .ToList();

        // Sparse and step-shaped until valuations are updated more regularly — an honest
        // reflection of what's actually tracked. The account page draws its own series
        // from the same builder.
        var history = ValuationHistory.Build(allHoldings, snapshots, converter, displayCurrency)
            .Select(p => new NetWorthPointDto(p.Date, p.Value))
            .ToList();

        return new DashboardSummaryDto(total, displayCurrency, allocation, history, stale);
    }
}
