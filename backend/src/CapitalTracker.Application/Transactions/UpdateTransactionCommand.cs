using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Holdings;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Transactions;

/// <summary>
/// Corrects a transaction in place. The holding it belongs to is fixed — moving a
/// transaction between assets would silently move units out of one position and into
/// another, which is two operations, not an edit.
/// </summary>
public record UpdateTransactionCommand(
    Guid Id,
    TransactionType Type,
    DateOnly Date,
    decimal Quantity,
    decimal UnitPrice,
    string? Currency = null,
    string? Notes = null) : IRequest<TransactionDto>;

public class UpdateTransactionCommandHandler(IApplicationDbContext db)
    : IRequestHandler<UpdateTransactionCommand, TransactionDto>
{
    public async Task<TransactionDto> Handle(UpdateTransactionCommand request, CancellationToken cancellationToken)
    {
        var transaction = await db.Transactions.SingleAsync(t => t.Id == request.Id, cancellationToken);

        // Past the filter for the same reason GetHoldingByIdQuery is: a deleted holding's
        // page still opens, so its rows still have to resolve to a name.
        var holding = await db.Holdings
            .IgnoreQueryFilters()
            .SingleAsync(h => h.Id == transaction.HoldingId, cancellationToken);

        var currency = request.Currency ?? transaction.Currency;
        TransactionRules.ValidateShape(request.Quantity, request.UnitPrice, currency);

        // Validated against a detached copy before anything is mutated: the tracked entity
        // is inside `existing`, so editing it first would have the check read its own result.
        var edited = new Transaction
        {
            Id = transaction.Id,
            HoldingId = transaction.HoldingId,
            Type = request.Type,
            Date = request.Date,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            Currency = currency,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        var existing = await db.Transactions
            .Where(t => t.HoldingId == transaction.HoldingId)
            .ToListAsync(cancellationToken);
        TransactionRules.EnsurePositionStaysNonNegative(existing, edited);

        transaction.Type = edited.Type;
        transaction.Date = edited.Date;
        transaction.Quantity = edited.Quantity;
        transaction.UnitPrice = edited.UnitPrice;
        transaction.Currency = edited.Currency;
        transaction.Notes = edited.Notes;

        await db.SaveChangesAsync(cancellationToken);

        // An edit can both close a position and re-open one that was closed.
        await PositionClosure.SyncAsync(db, transaction.HoldingId, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return transaction.ToDto(holding.Name);
    }
}
