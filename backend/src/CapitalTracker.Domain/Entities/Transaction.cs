using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Domain.Entities;

/// <summary>
/// A single transaction against a holding: buy/sell/dividend/rent/expense/etc.
/// </summary>
public class Transaction
{
    public Guid Id { get; set; }
    public Guid HoldingId { get; set; }
    public Holding? Holding { get; set; }

    public TransactionType Type { get; set; }
    public DateOnly Date { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Notes { get; set; }
}
