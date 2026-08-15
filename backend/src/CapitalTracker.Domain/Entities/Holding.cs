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

    /// <summary>Units held (shares, coins, etc.) — optional; not every asset is unit-based.</summary>
    public decimal? Quantity { get; set; }

    public string? Notes { get; set; }

    /// <summary>
    /// Free-form, type-specific fields (e.g. "Забудовник", "Адреса" for real estate;
    /// "Сервіс" for a brokerage). Plain text — never put secrets here, use
    /// <see cref="SecretAttributes"/> instead.
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = [];

    /// <summary>
    /// Same idea as <see cref="Attributes"/> but for sensitive values (logins,
    /// passwords). Values are AES-encrypted at the application layer before
    /// they ever reach this property — this column never holds plaintext.
    /// </summary>
    public Dictionary<string, string> SecretAttributes { get; set; } = [];

    public Guid? SectorId { get; set; }
    public Sector? Sector { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Transaction> Transactions { get; set; } = [];
    public List<ValuationSnapshot> ValuationSnapshots { get; set; } = [];
}
