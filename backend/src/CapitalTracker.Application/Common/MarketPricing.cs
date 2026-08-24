using CapitalTracker.Application.Holdings;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Common;

/// <summary>
/// Which holdings a market-data provider can say anything about. Shared so the price job
/// and the AI analysis pipeline cannot drift apart on what counts as quotable.
/// </summary>
public static class MarketPricing
{
    /// <summary>
    /// Crypto is excluded despite often having a symbol: Finnhub identifies it by
    /// exchange-prefixed pairs (BINANCE:BTCUSDT) that can't be derived from a bare ticker.
    /// </summary>
    public static bool CanQuote(string? symbol, AccountType accountType) =>
        !string.IsNullOrWhiteSpace(symbol) && accountType == AccountType.Brokerage;

    /// <summary>
    /// A quote is a price per unit, so turning it into a holding's value additionally
    /// needs a quantity — without one there is nothing to multiply.
    /// </summary>
    public static bool CanAutoPrice(string? symbol, AccountType accountType, decimal? quantity) =>
        CanQuote(symbol, accountType) && quantity is > 0m;

    /// <summary>
    /// The same three-way answer the holding page shows, in one place — the staleness rule
    /// needs it too, and two copies would eventually disagree about what NeedsQuantity is.
    /// </summary>
    public static PricingMode ModeFor(string? symbol, AccountType accountType, decimal? quantity) =>
        CanQuote(symbol, accountType)
            ? CanAutoPrice(symbol, accountType, quantity)
                ? PricingMode.Automatic
                : PricingMode.NeedsQuantity
            : PricingMode.Manual;
}
