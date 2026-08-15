using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

public record DeleteSecretAttributeCommand(Guid HoldingId, string Key) : IRequest<HoldingDetailDto>;

public class DeleteSecretAttributeCommandHandler(IApplicationDbContext db, ISender sender)
    : IRequestHandler<DeleteSecretAttributeCommand, HoldingDetailDto>
{
    public async Task<HoldingDetailDto> Handle(DeleteSecretAttributeCommand request, CancellationToken cancellationToken)
    {
        var holding = await db.Holdings.SingleAsync(h => h.Id == request.HoldingId, cancellationToken);

        var updated = new Dictionary<string, string>(holding.SecretAttributes);
        updated.Remove(request.Key);
        holding.SecretAttributes = updated;

        await db.SaveChangesAsync(cancellationToken);

        return (await sender.Send(new GetHoldingByIdQuery(holding.Id), cancellationToken))!;
    }
}
