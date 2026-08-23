using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

/// <summary>
/// Records a valuation for one day, replacing any existing entry for that date.
///
/// <c>Currency</c> is optional and defaults to whatever the holding is already
/// denominated in — its most recent snapshot, falling back to the account. That
/// default matters: a holding can legitimately be denominated differently from its
/// account (a USD stock in a UAH brokerage account), and re-stamping the account's
/// currency on every save would silently corrupt it back.
/// </summary>
public record AddValuationCommand(
    Guid HoldingId,
    decimal Value,
    DateOnly? Date = null,
    string? Currency = null) : IRequest<HoldingDetailDto>;

public class AddValuationCommandHandler(IApplicationDbContext db, ISender sender)
    : IRequestHandler<AddValuationCommand, HoldingDetailDto>
{
    public async Task<HoldingDetailDto> Handle(AddValuationCommand request, CancellationToken cancellationToken)
    {
        if (request.Currency is not null && !SupportedCurrencies.All.Contains(request.Currency))
            throw new DomainValidationException($"Валюта {request.Currency} не підтримується.");

        var holding = await db.Holdings.SingleAsync(h => h.Id == request.HoldingId, cancellationToken);
        var account = await db.Accounts.SingleAsync(a => a.Id == holding.AccountId, cancellationToken);
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var snapshots = await db.ValuationSnapshots
            .Where(v => v.HoldingId == holding.Id)
            .ToListAsync(cancellationToken);

        var existing = snapshots.SingleOrDefault(v => v.Date == date);

        if (existing is not null)
        {
            existing.Value = request.Value;

            // Only an explicit choice changes the currency of a row that already exists —
            // the user is correcting a number in the denomination it is already in.
            if (request.Currency is not null)
                existing.Currency = request.Currency;

            // Touching a row by hand makes it manual, even if the price job wrote it.
            // Without this the next run would silently discard the correction.
            existing.IsManual = true;
        }
        else
        {
            var currency = request.Currency ?? HoldingDenomination.Of(snapshots, account);

            db.ValuationSnapshots.Add(new ValuationSnapshot
            {
                Id = Guid.NewGuid(),
                HoldingId = holding.Id,
                Date = date,
                Value = request.Value,
                Currency = currency,
                IsManual = true,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return (await sender.Send(new GetHoldingByIdQuery(holding.Id), cancellationToken))!;
    }
}
