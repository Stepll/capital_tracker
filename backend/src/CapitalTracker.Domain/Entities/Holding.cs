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

    /// <summary>
    /// Opts this holding out of AI analysis entirely. Generating an analysis sends
    /// <see cref="Attributes"/> and <see cref="Notes"/> to a third-party model and
    /// to web search; this is the switch for assets the owner would rather not
    /// expose that way (never <see cref="SecretAttributes"/>, which never leave).
    /// </summary>
    public bool ExcludeFromAiAnalysis { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the owner deleted this holding, or null while it is held. Deletion is soft:
    /// a hard delete would take the holding's ValuationSnapshots with it and thereby
    /// rewrite the portfolio's past — the net worth chart is computed from whatever
    /// snapshots exist, so an asset sold today would look like it never existed in March.
    /// Kept out of every read path by a global query filter; the history series is the
    /// one place that deliberately looks past it, counting the holding up to this date.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    public List<Transaction> Transactions { get; set; } = [];
    public List<ValuationSnapshot> ValuationSnapshots { get; set; } = [];
}
