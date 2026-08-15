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

        db.Accounts.Remove(account);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
