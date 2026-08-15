using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Holdings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Accounts;

public record GetAccountsQuery : IRequest<List<AccountDto>>;

public class GetAccountsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountsQuery, List<AccountDto>>
{
    public Task<List<AccountDto>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        // Sums each holding's latest valuation within its own account's currency
        // — safe because holdings always inherit their account's currency.
        var totalsByAccount = HoldingQueries.WithCurrentValue(db)
            .GroupBy(h => h.AccountId)
            .Select(g => new { AccountId = g.Key, Total = g.Sum(h => h.CurrentValue) });

        return db.Accounts
            .OrderBy(a => a.CreatedAt)
            .GroupJoin(totalsByAccount, a => a.Id, t => t.AccountId, (a, totals) => new { Account = a, Totals = totals })
            .SelectMany(x => x.Totals.DefaultIfEmpty(), (x, total) => new AccountDto(
                x.Account.Id, x.Account.Name, x.Account.Type, x.Account.Currency, x.Account.CreatedAt,
                total != null ? total.Total : 0))
            .ToListAsync(cancellationToken);
    }
}
