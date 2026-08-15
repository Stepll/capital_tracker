using CapitalTracker.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Holdings;

/// <summary>
/// Decrypts and returns exactly one secret attribute value — never the whole
/// map — so a "show password" click stays a single, deliberate, auditable
/// action rather than something that leaks everything at once.
/// </summary>
public record RevealSecretAttributeQuery(Guid HoldingId, string Key) : IRequest<string?>;

public class RevealSecretAttributeQueryHandler(IApplicationDbContext db, IEncryptionService encryption)
    : IRequestHandler<RevealSecretAttributeQuery, string?>
{
    public async Task<string?> Handle(RevealSecretAttributeQuery request, CancellationToken cancellationToken)
    {
        var holding = await db.Holdings.SingleAsync(h => h.Id == request.HoldingId, cancellationToken);
        return holding.SecretAttributes.TryGetValue(request.Key, out var ciphertext)
            ? encryption.Decrypt(ciphertext)
            : null;
    }
}
