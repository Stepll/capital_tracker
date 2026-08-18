using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Accounts;

public record DeleteAccountCommand(Guid Id) : IRequest<bool>;

public class DeleteAccountCommandHandler(IApplicationDbContext db)
    : IRequestHandler<DeleteAccountCommand, bool>
{
    public async Task<bool> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleOrDefaultAsync(a => a.Id == request.Id, cancellationToken);
        if (account is null)
            return false;

        var deletedAt = DateTime.UtcNow;
        account.DeletedAt = deletedAt;

        // Stamped explicitly rather than left to the database cascade: the cascade would
        // hard-delete the holdings and their valuation history, which is exactly what
        // soft deletion exists to prevent. The same timestamp on both keeps the holding
        // filter a single predicate — it never has to join back into Account.
        var holdings = await db.Holdings
            .Where(h => h.AccountId == account.Id)
            .ToListAsync(cancellationToken);

        foreach (var holding in holdings)
        {
            holding.DeletedAt = deletedAt;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
