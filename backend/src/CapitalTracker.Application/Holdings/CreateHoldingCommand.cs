using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

// Holdings don't pick their own currency — they inherit the parent account's,
// so a single account's holdings can always be summed without conversion.
// Cross-account/cross-currency totals go through DisplayCurrency + ExchangeRate.
public record CreateHoldingCommand(
    Guid AccountId,
    string Name,
    string? Symbol,
    decimal? Quantity,
    decimal InitialValue) : IRequest<HoldingDto>;

public class CreateHoldingCommandHandler(IApplicationDbContext db)
    : IRequestHandler<CreateHoldingCommand, HoldingDto>
{
    public async Task<HoldingDto> Handle(CreateHoldingCommand request, CancellationToken cancellationToken)
    {
        var account = await db.Accounts.SingleAsync(a => a.Id == request.AccountId, cancellationToken);

        var holding = new Holding
        {
            Id = Guid.NewGuid(),
            AccountId = request.AccountId,
            Name = request.Name,
            Symbol = request.Symbol,
            Quantity = request.Quantity,
        };
        db.Holdings.Add(holding);

        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Value = request.InitialValue,
            Currency = account.Currency,
            IsManual = true,
        });

        await db.SaveChangesAsync(cancellationToken);

        return new HoldingDto(
            holding.Id, holding.AccountId, holding.Name, holding.Symbol,
            account.Currency, request.InitialValue, holding.CreatedAt);
    }
}
