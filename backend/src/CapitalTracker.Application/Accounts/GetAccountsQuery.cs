using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Accounts;

public record GetAccountsQuery : IRequest<List<AccountDto>>;

public class GetAccountsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountsQuery, List<AccountDto>>
{
    public Task<List<AccountDto>> Handle(GetAccountsQuery request, CancellationToken cancellationToken) =>
        db.Accounts
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AccountDto(
                a.Id, a.Name, a.Type, a.Currency, a.CreatedAt,
                // Sum of each holding's latest valuation — safe without conversion
                // since holdings always inherit their account's currency.
                db.Holdings
                    .Where(h => h.AccountId == a.Id)
                    .Select(h => db.ValuationSnapshots
                        .Where(v => v.HoldingId == h.Id)
                        .OrderByDescending(v => v.Date)
                        .Select(v => (decimal?)v.Value)
                        .FirstOrDefault() ?? 0m)
                    .Sum()))
            .ToListAsync(cancellationToken);
}
