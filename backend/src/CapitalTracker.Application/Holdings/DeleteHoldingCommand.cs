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

        // Marked, not removed — and the ValuationSnapshots stay untouched. Deleting them
        // (which this used to do) silently rewrote the net worth chart for every past
        // date the asset was held. The global query filter takes it out of every current
        // figure from here on; the history keeps counting it up to this moment.
        holding.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
