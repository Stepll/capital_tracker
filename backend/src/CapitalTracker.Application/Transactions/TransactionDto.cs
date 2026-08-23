using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Transactions;

public record TransactionDto(
    Guid Id,
    Guid HoldingId,
    // Carried on every row because the account page lists the transactions of all its
    // holdings in one stream; the holding page simply ignores it.
    string HoldingName,
    TransactionType Type,
    DateOnly Date,
    decimal Quantity,
    decimal UnitPrice,
    // Quantity x UnitPrice, folded here so the two lists can't round it differently.
    decimal Amount,
    string Currency,
    string? Notes);

internal static class TransactionMapping
{
    public static TransactionDto ToDto(this Transaction transaction, string holdingName) =>
        new(transaction.Id,
            transaction.HoldingId,
            holdingName,
            transaction.Type,
            transaction.Date,
            transaction.Quantity,
            transaction.UnitPrice,
            Math.Round(transaction.Quantity * transaction.UnitPrice, 2),
            transaction.Currency,
            transaction.Notes);
}
