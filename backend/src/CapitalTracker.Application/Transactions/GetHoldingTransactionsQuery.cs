using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Transactions;

public record GetHoldingTransactionsQuery(Guid HoldingId) : IRequest<List<TransactionDto>>;

public class GetHoldingTransactionsQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetHoldingTransactionsQuery, List<TransactionDto>>
{
    public async Task<List<TransactionDto>> Handle(
        GetHoldingTransactionsQuery request, CancellationToken cancellationToken)
    {
        // Past the soft-delete filter alongside GetHoldingByIdQuery: the page of a deleted
        // holding opens read-only, and the history is most of what it is there to show.
        var holding = await db.Holdings
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(h => h.Id == request.HoldingId, cancellationToken);
        if (holding is null)
            return [];

        // Fetched flat and sorted in memory — the project's standing rule for this shape.
        // Id breaks ties so two rows on the same day don't swap places between requests.
        return (await db.Transactions
                .Where(t => t.HoldingId == holding.Id)
                .ToListAsync(cancellationToken))
            .OrderByDescending(t => t.Date)
            .ThenBy(t => t.Id)
            .Select(t => t.ToDto(holding.Name))
            .ToList();
    }
}
