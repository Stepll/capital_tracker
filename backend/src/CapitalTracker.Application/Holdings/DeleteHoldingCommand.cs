using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

public record DeleteHoldingCommand(Guid Id) : IRequest<bool>;

public class DeleteHoldingCommandHandler(IApplicationDbContext db)
    : IRequestHandler<DeleteHoldingCommand, bool>
{
    public async Task<bool> Handle(DeleteHoldingCommand request, CancellationToken cancellationToken)
    {
        var holding = await db.Holdings.SingleOrDefaultAsync(h => h.Id == request.Id, cancellationToken);
        if (holding is null)
            return false;

        var snapshots = db.ValuationSnapshots.Where(v => v.HoldingId == request.Id);
        db.ValuationSnapshots.RemoveRange(snapshots);
        db.Holdings.Remove(holding);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
