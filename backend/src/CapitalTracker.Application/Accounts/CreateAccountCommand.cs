using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;

namespace CapitalTracker.Application.Accounts;

public record CreateAccountCommand(string Name, AccountType Type, string Currency) : IRequest<AccountDto>;

public class CreateAccountCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateAccountCommand, AccountDto>
{
    public async Task<AccountDto> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Type = request.Type,
            Currency = request.Currency,
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync(cancellationToken);

        return new AccountDto(account.Id, account.Name, account.Type, account.Currency, account.CreatedAt);
    }
}
