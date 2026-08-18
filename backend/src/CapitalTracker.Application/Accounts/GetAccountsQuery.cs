using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Accounts;

public record GetAccountsQuery : IRequest<List<AccountDto>>;

public class GetAccountsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountsQuery, List<AccountDto>>
{
    public async Task<List<AccountDto>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        // Fetched flat and folded in C# rather than composed into one EF query. The total
        // needs a currency conversion per holding, and joining the rate table into this
        // projection is exactly the composition that stops translating through Npgsql —
        // same reasoning as GetDashboardSummaryQuery.
        var accounts = (await db.Accounts.ToListAsync(cancellationToken))
            .OrderBy(a => a.CreatedAt)
            .ToList();
        var holdings = await db.Holdings.ToListAsync(cancellationToken);
        var snapshots = await db.ValuationSnapshots.ToListAsync(cancellationToken);
        var converter = CurrencyConverter.FromRates(await db.ExchangeRates.ToListAsync(cancellationToken));

        var holdingsByAccount = holdings.ToLookup(h => h.AccountId);
        var snapshotsByHolding = snapshots.ToLookup(s => s.HoldingId);

        return accounts
            .Select(a => new AccountDto(
                a.Id, a.Name, a.Type, a.Currency, a.CreatedAt,
                // A holding is denominated by its latest snapshot, which can differ from
                // the account's currency (a USD stock held in a UAH brokerage account), so
                // every holding is converted into the account's currency before summing.
                holdingsByAccount[a.Id].Sum(h =>
                {
                    var latest = snapshotsByHolding[h.Id].OrderByDescending(s => s.Date).FirstOrDefault();
                    return latest is null ? 0m : converter.Convert(latest.Value, latest.Currency, a.Currency);
                })))
            .ToList();
    }
}
