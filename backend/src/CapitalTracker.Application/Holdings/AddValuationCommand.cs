using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

/// <summary>
/// Records a manual valuation for a holding (e.g. updated real-estate
/// estimate), for a caller-chosen date — defaults to today when omitted, so
/// this also covers backdated corrections. Upserts: a second value for a
/// date that already has one replaces it rather than creating an ambiguous
/// duplicate point on the chart.
/// </summary>
public record AddValuationCommand(Guid HoldingId, decimal Value, DateOnly? Date = null) : IRequest<HoldingDetailDto>;

public class AddValuationCommandHandler(IApplicationDbContext db, ISender sender)
    : IRequestHandler<AddValuationCommand, HoldingDetailDto>
{
    public async Task<HoldingDetailDto> Handle(AddValuationCommand request, CancellationToken cancellationToken)
    {
        var holding = await db.Holdings.SingleAsync(h => h.Id == request.HoldingId, cancellationToken);
        var account = await db.Accounts.SingleAsync(a => a.Id == holding.AccountId, cancellationToken);
        var date = request.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await db.ValuationSnapshots
            .SingleOrDefaultAsync(v => v.HoldingId == holding.Id && v.Date == date, cancellationToken);

        if (existing is not null)
        {
            existing.Value = request.Value;
            existing.Currency = account.Currency;
        }
        else
        {
            db.ValuationSnapshots.Add(new ValuationSnapshot
            {
                Id = Guid.NewGuid(),
                HoldingId = holding.Id,
                Date = date,
                Value = request.Value,
                Currency = account.Currency,
                IsManual = true,
            });
        }

        await db.SaveChangesAsync(cancellationToken);

        return (await sender.Send(new GetHoldingByIdQuery(holding.Id), cancellationToken))!;
    }
}
