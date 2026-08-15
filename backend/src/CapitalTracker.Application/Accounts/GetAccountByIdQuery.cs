using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Holdings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Accounts;

public record GetAccountByIdQuery(Guid Id) : IRequest<AccountDetailDto?>;

public class GetAccountByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountByIdQuery, AccountDetailDto?>
{
    public async Task<AccountDetailDto?> Handle(GetAccountByIdQuery request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (account is null)
            return null;

        var holdings = await HoldingQueries.WithCurrentValue(db, request.Id)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

        return new AccountDetailDto(
            account.Id, account.Name, account.Type, account.Currency, account.CreatedAt, holdings);
    }
}
