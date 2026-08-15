using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

public record GetHoldingByIdQuery(Guid Id) : IRequest<HoldingDetailDto?>;

public class GetHoldingByIdQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetHoldingByIdQuery, HoldingDetailDto?>
{
    public async Task<HoldingDetailDto?> Handle(GetHoldingByIdQuery request, CancellationToken cancellationToken)
    {
        var holding = await db.Holdings.SingleOrDefaultAsync(h => h.Id == request.Id, cancellationToken);
        if (holding is null)
            return null;

        var account = await db.Accounts.SingleAsync(a => a.Id == holding.AccountId, cancellationToken);

        var sectorName = holding.SectorId is null
            ? null
            : (await db.Sectors.SingleOrDefaultAsync(s => s.Id == holding.SectorId, cancellationToken))?.Name;

        // Fetched flat and sorted client-side — same reasoning as the dashboard
        // summary: small data volume, and OrderBy-after-Select-into-DTO doesn't
        // reliably translate through EF Core anyway.
        var snapshots = (await db.ValuationSnapshots
                .Where(v => v.HoldingId == holding.Id)
                .ToListAsync(cancellationToken))
            .OrderBy(v => v.Date)
            .ToList();

        var latest = snapshots.LastOrDefault();

        return new HoldingDetailDto(
            holding.Id,
            holding.AccountId,
            account.Name,
            holding.Name,
            holding.Symbol,
            latest?.Currency ?? account.Currency,
            latest?.Value ?? 0m,
            holding.SectorId,
            sectorName,
            holding.CreatedAt,
            snapshots.Select(v => new ValuationPointDto(v.Date, v.Value)).ToList());
    }
}
