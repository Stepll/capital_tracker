using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

/// <summary>Records a new manual valuation for a holding (e.g. updated real-estate estimate).</summary>
public record AddValuationCommand(Guid HoldingId, decimal Value) : IRequest<HoldingDetailDto>;

public class AddValuationCommandHandler(IApplicationDbContext db, ISender sender)
    : IRequestHandler<AddValuationCommand, HoldingDetailDto>
{
    public async Task<HoldingDetailDto> Handle(AddValuationCommand request, CancellationToken cancellationToken)
    {
        var holding = await db.Holdings.SingleAsync(h => h.Id == request.HoldingId, cancellationToken);
        var account = await db.Accounts.SingleAsync(a => a.Id == holding.AccountId, cancellationToken);

        db.ValuationSnapshots.Add(new ValuationSnapshot
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            Value = request.Value,
            Currency = account.Currency,
            IsManual = true,
        });
        await db.SaveChangesAsync(cancellationToken);

        return (await sender.Send(new GetHoldingByIdQuery(holding.Id), cancellationToken))!;
    }
}
