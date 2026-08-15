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
            .Select(a => new AccountDto(a.Id, a.Name, a.Type, a.Currency, a.CreatedAt))
            .ToListAsync(cancellationToken);
}
