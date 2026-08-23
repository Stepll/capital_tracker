using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Transactions;

/// <summary>
/// Every transaction of every holding in one account, newest first. Deleted holdings are
/// filtered out here, unlike on their own page: this list answers "what happened in this
/// account", and the account view shows the assets it currently holds.
/// </summary>
public record GetAccountTransactionsQuery(Guid AccountId) : IRequest<List<TransactionDto>>;

public class GetAccountTransactionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetAccountTransactionsQuery, List<TransactionDto>>
{
    public async Task<List<TransactionDto>> Handle(
        GetAccountTransactionsQuery request, CancellationToken cancellationToken)
    {
        var names = await db.Holdings
            .Where(h => h.AccountId == request.AccountId)
            .ToDictionaryAsync(h => h.Id, h => h.Name, cancellationToken);

        var ids = names.Keys.ToList();

        return (await db.Transactions
                .Where(t => ids.Contains(t.HoldingId))
                .ToListAsync(cancellationToken))
            .OrderByDescending(t => t.Date)
            .ThenBy(t => t.Id)
            .Select(t => t.ToDto(names[t.HoldingId]))
            .ToList();
    }
}
