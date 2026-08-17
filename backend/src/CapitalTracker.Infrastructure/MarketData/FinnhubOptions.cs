namespace CapitalTracker.Infrastructure.MarketData;

public class FinnhubOptions
{
    public const string SectionName = "Finnhub";

    /// <summary>
    /// Deliberately nullable, unlike Anthropic:ApiKey — market data is an enrichment,
    /// not a prerequisite. Without a key the analysis still runs on web search alone,
    /// which is the only path anyway for holdings without a ticker (real estate, cash).
    /// So a missing key must not stop the app from starting.
    /// </summary>
    public string? ApiKey { get; set; }
}
