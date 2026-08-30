using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Holdings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Transactions;

/// <summary>
/// Removes a transaction for real, unlike holdings and accounts. Nothing historical hangs
/// off it — the net worth chart is built from ValuationSnapshots, not from transactions —
/// so a hard delete rewrites only the position, which is exactly what the owner is asking
/// for when they delete a row they entered by mistake.
/// </summary>
public record DeleteTransactionCommand(Guid Id) : IRequest<bool>;

public class DeleteTransactionCommandHandler(IApplicationDbContext db)
    : IRequestHandler<DeleteTransactionCommand, bool>
{
    public async Task<bool> Handle(DeleteTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await db.Transactions.SingleOrDefaultAsync(t => t.Id == request.Id, cancellationToken);
        if (transaction is null)
            return false;

        db.Transactions.Remove(transaction);
        await db.SaveChangesAsync(cancellationToken);

        // Deleting the sale that closed a position re-opens it, and takes the zero with it.
        await PositionClosure.SyncAsync(db, transaction.HoldingId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
