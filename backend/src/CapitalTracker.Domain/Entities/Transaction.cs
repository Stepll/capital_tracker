using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Domain.Entities;

/// <summary>
/// A single transaction against a holding: buy/sell/dividend/rent/expense/etc.
///
/// Buys, sells, deposits and withdrawals are also the only record of how many units a
/// holding has — see HoldingPositions. The quantity is always positive; direction comes
/// from <see cref="Type"/>, never from the sign.
/// </summary>
public class Transaction
{
    /// <summary>
    /// Marks the row that opens a position: written when a holding is created, and by the
    /// migration that moved the old Holding.Quantity column into this table. Both derive
    /// the unit price from a valuation rather than a real receipt, so it is worth being
    /// able to tell them apart from a transaction the owner actually entered.
    /// </summary>
    public const string OpeningPositionNote = "Початкова позиція";

    public Guid Id { get; set; }
    public Guid HoldingId { get; set; }
    public Holding? Holding { get; set; }

    public TransactionType Type { get; set; }
    public DateOnly Date { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }

    /// <summary>Which import brought this row in, if any. Null for anything entered by hand.</summary>
    public Guid? ImportBatchId { get; set; }
}
