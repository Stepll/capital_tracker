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

        // Ordered client-side: EF Core can't translate an OrderBy applied after
        // a Select into a constructor-based DTO like HoldingDto, and holding
        // counts per account are small enough that this is a non-issue.
        var holdings = (await HoldingQueries.WithCurrentValue(db, request.Id).ToListAsync(cancellationToken))
            .OrderBy(h => h.CreatedAt)
            .ToList();

        return new AccountDetailDto(
            account.Id, account.Name, account.Type, account.Currency, account.CreatedAt, holdings);
    }
}
