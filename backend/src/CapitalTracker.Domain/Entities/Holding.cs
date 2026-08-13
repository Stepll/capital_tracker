namespace CapitalTracker.Domain.Entities;

/// <summary>
/// A single asset held within an account: a stock/ETF ticker, a real-estate object,
/// a deposit, etc.
/// </summary>
public class Holding
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Account? Account { get; set; }

    public required string Name { get; set; }

    /// <summary>Ticker symbol for market-traded assets; null for e.g. real estate.</summary>
    public string? Symbol { get; set; }

    public Guid? SectorId { get; set; }
    public Sector? Sector { get; set; }

    public List<Transaction> Transactions { get; set; } = [];
    public List<ValuationSnapshot> ValuationSnapshots { get; set; } = [];
}
