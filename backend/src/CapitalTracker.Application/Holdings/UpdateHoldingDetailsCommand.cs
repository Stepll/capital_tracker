using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

/// <summary>
/// Updates the free-form parts of a holding.
///
/// <c>Attributes</c> (public), when provided, fully replaces the existing
/// set — safe because the client always has the full current values in hand
/// (they're returned as-is by GetHoldingByIdQuery), same as any form save.
///
/// <c>SecretAttributes</c> is different: the client never sees decrypted
/// values (see HoldingDetailDto — only key names come back), so it can't
/// possibly resend the full set. This is a per-key upsert instead — a blank
/// value means "leave this key untouched", not "clear it". Encrypted before
/// it's persisted; plaintext never reaches the database.
/// </summary>
public record UpdateHoldingDetailsCommand(
    Guid HoldingId,
    decimal? Quantity,
    string? Notes,
    Dictionary<string, string>? Attributes,
    Dictionary<string, string>? SecretAttributes) : IRequest<HoldingDetailDto>;

public class UpdateHoldingDetailsCommandHandler(IApplicationDbContext db, IEncryptionService encryption, ISender sender)
    : IRequestHandler<UpdateHoldingDetailsCommand, HoldingDetailDto>
{
    public async Task<HoldingDetailDto> Handle(UpdateHoldingDetailsCommand request, CancellationToken cancellationToken)
    {
        var holding = await db.Holdings.SingleAsync(h => h.Id == request.HoldingId, cancellationToken);

        holding.Quantity = request.Quantity;
        holding.Notes = request.Notes;

        if (request.Attributes is not null)
            holding.Attributes = request.Attributes;

        if (request.SecretAttributes is not null)
        {
            var merged = new Dictionary<string, string>(holding.SecretAttributes);
            foreach (var (key, plaintext) in request.SecretAttributes)
            {
                if (!string.IsNullOrEmpty(plaintext))
                    merged[key] = encryption.Encrypt(plaintext);
            }
            holding.SecretAttributes = merged;
        }

        await db.SaveChangesAsync(cancellationToken);

        return (await sender.Send(new GetHoldingByIdQuery(holding.Id), cancellationToken))!;
    }
}
