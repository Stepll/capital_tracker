namespace CapitalTracker.Domain.Entities;

/// <summary>
/// Point-in-time value of a holding: fetched automatically for market-traded assets,
/// entered manually for e.g. real estate.
/// </summary>
public class ValuationSnapshot
{
    public Guid Id { get; set; }
    public Guid HoldingId { get; set; }
    public Holding? Holding { get; set; }

    public DateOnly Date { get; set; }
    public decimal Value { get; set; }
    public string Currency { get; set; } = "USD";
    public bool IsManual { get; set; }
}
