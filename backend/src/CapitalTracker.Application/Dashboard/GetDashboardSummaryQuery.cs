using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
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
        var holdings = await db.Holdings.ToListAsync(cancellationToken);
        var snapshots = await db.ValuationSnapshots.ToListAsync(cancellationToken);

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

        // History: for every date any snapshot was recorded, the portfolio's
        // total value "as of" that date (last known value per holding at or
        // before it). Sparse and step-shaped until valuations are updated
        // more regularly — an honest reflection of what's actually tracked.
        //
        // Today is always a point, even with no snapshot dated today, so the line ends
        // where the headline total does instead of stopping at the last recorded date
        // and disagreeing with it by whatever the rate has done since.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var allDates = snapshots.Select(s => s.Date).Append(today).Distinct().OrderBy(d => d).ToList();
        var history = allDates.Select(date =>
        {
            var asOfTotal = holdings.Sum(h =>
            {
                var asOf = snapshotsByHolding[h.Id]
                    .Where(s => s.Date <= date)
                    .OrderByDescending(s => s.Date)
                    .FirstOrDefault();
                // Converted at that day's rate, not today's — the value is what the
                // portfolio was worth then, and pricing it at the current rate would
                // redraw the whole line every time the hryvnia moves, turning a currency
                // slide into apparent growth. The rate belongs to the point on the chart,
                // not to the snapshot: a valuation carried forward for three months is
                // still being asked "what was that worth on this date".
                return asOf is null ? 0 : converter.ConvertAsOf(asOf.Value, asOf.Currency, displayCurrency, date);
            });
            return new NetWorthPointDto(date, asOfTotal);
        }).ToList();

        return new DashboardSummaryDto(total, displayCurrency, allocation, history);
    }
}
