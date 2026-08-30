using CapitalTracker.Application.Common;
using CapitalTracker.Application.Common.Interfaces;
using CapitalTracker.Application.Holdings;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace CapitalTracker.Application.Transactions;

/// <summary>
/// Records one transaction against a holding. For the unit-bearing types this is what
/// moves the holding's quantity — there is no quantity field to write any more.
///
/// <c>Currency</c> is optional and defaults to whatever the holding is already denominated
/// in, same rule as AddValuationCommand: a holding can be denominated differently from its
/// account, and inheriting the account's currency here would quietly mis-stamp the row.
/// </summary>
public record AddTransactionCommand(
    Guid HoldingId,
    TransactionType Type,
    DateOnly Date,
    decimal Quantity,
    decimal UnitPrice,
    string? Currency = null,
    string? Notes = null) : IRequest<TransactionDto>;

public class AddTransactionCommandHandler(IApplicationDbContext db)
    : IRequestHandler<AddTransactionCommand, TransactionDto>
{
    public async Task<TransactionDto> Handle(AddTransactionCommand request, CancellationToken cancellationToken)
    {
        // Filtered on purpose: a deleted holding keeps the history it has and takes no more.
        var holding = await db.Holdings.SingleAsync(h => h.Id == request.HoldingId, cancellationToken);
        var account = await db.Accounts
            .IgnoreQueryFilters()
            .SingleAsync(a => a.Id == holding.AccountId, cancellationToken);

        var snapshots = await db.ValuationSnapshots
            .Where(v => v.HoldingId == holding.Id)
            .ToListAsync(cancellationToken);

        var currency = request.Currency ?? HoldingDenomination.Of(snapshots, account);
        TransactionRules.ValidateShape(request.Quantity, request.UnitPrice, currency);

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            HoldingId = holding.Id,
            Type = request.Type,
            Date = request.Date,
            Quantity = request.Quantity,
            UnitPrice = request.UnitPrice,
            Currency = currency,
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
        };

        var existing = await db.Transactions
            .Where(t => t.HoldingId == holding.Id)
            .ToListAsync(cancellationToken);
        TransactionRules.EnsurePositionStaysNonNegative(existing, transaction);

        db.Transactions.Add(transaction);
        await db.SaveChangesAsync(cancellationToken);

        // A sale that empties the position writes the valuation that follows from it: zero.
        await PositionClosure.SyncAsync(db, holding.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return transaction.ToDto(holding.Name);
    }
}
