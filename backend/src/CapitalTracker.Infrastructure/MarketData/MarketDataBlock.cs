namespace CapitalTracker.Infrastructure.MarketData;

/// <summary>
/// Quantitative context for a ticker, fetched before the model runs and rendered
/// into the prompt as plain text. Pre-fetched rather than exposed as a tool so the
/// pipeline stays linear — which is what keeps the progress phases meaningful.
/// </summary>
public record MarketDataBlock(
    decimal Price,
    decimal? PercentChange,
    IReadOnlyList<MarketNewsItem> News);

public record MarketNewsItem(DateOnly Date, string Source, string Headline, string Url);
