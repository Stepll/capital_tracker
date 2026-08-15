using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

public record AssignSectorCommand(Guid HoldingId, Guid? SectorId) : IRequest<HoldingDetailDto>;

public class AssignSectorCommandHandler(IApplicationDbContext db, ISender sender)
    : IRequestHandler<AssignSectorCommand, HoldingDetailDto>
{
    public async Task<HoldingDetailDto> Handle(AssignSectorCommand request, CancellationToken cancellationToken)
    {
        var holding = await db.Holdings.SingleAsync(h => h.Id == request.HoldingId, cancellationToken);
        holding.SectorId = request.SectorId;
        await db.SaveChangesAsync(cancellationToken);

        return (await sender.Send(new GetHoldingByIdQuery(holding.Id), cancellationToken))!;
    }
}
