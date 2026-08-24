using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
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
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var holding = new Holding
        {
            Id = Guid.NewGuid(),
            AccountId = request.AccountId,
            Name = request.Name,
            Symbol = request.Symbol,
        };
        db.Holdings.Add(holding);

        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            Date = today,
            Value = request.InitialValue,
            Currency = account.Currency,
            IsManual = true,
        });

        // The opening position, written as a transaction because that is now the only place
        // a quantity lives. A quotable asset whose unit count the owner left blank gets none
        // on purpose: assuming a single share would let the price job multiply a quote by it
        // and quietly rewrite the value, which is precisely what PricingMode.NeedsQuantity
        // exists to prevent. Anything else — an apartment, a deposit — is one indivisible
        // thing, and recording it keeps the account's history complete.
        var units = request.Quantity
            ?? (MarketPricing.CanQuote(request.Symbol, account.Type) ? null : 1m);

        if (units is > 0m)
        {
            db.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                HoldingId = holding.Id,
                Type = TransactionType.Buy,
                Date = today,
                Quantity = units.Value,
                // What the position is worth today is the best cost basis available at this
                // point; the row is editable, which is how a real purchase price gets in.
                UnitPrice = Math.Round(request.InitialValue / units.Value, 2),
                Currency = account.Currency,
                Notes = Transaction.OpeningPositionNote,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return new HoldingDto(
            holding.Id, holding.AccountId, holding.Name, holding.Symbol,
            account.Currency, request.InitialValue, holding.CreatedAt, today,
            new ValuationAgeDto(today, 0, ValuationStatus.Fresh));
    }
}
