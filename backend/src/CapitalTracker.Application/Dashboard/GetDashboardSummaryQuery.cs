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

        // Sparse and step-shaped until valuations are updated more regularly — an honest
        // reflection of what's actually tracked. The account page draws its own series
        // from the same builder.
        var history = ValuationHistory.Build(allHoldings, snapshots, converter, displayCurrency)
            .Select(p => new NetWorthPointDto(p.Date, p.Value))
            .ToList();

        return new DashboardSummaryDto(total, displayCurrency, allocation, history);
    }
}
