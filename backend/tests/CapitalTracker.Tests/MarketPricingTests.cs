using CapitalTracker.Application.Common;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Tests;

public class MarketPricingTests
{
    [Fact]
    public void A_ticker_on_a_brokerage_account_is_quotable() =>
        Assert.True(MarketPricing.CanQuote("AAPL", AccountType.Brokerage));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_a_symbol_there_is_nothing_to_look_up(string? symbol) =>
        Assert.False(MarketPricing.CanQuote(symbol, AccountType.Brokerage));

    [Fact]
    public void Crypto_is_excluded_even_when_it_carries_a_symbol() =>
        // Finnhub wants BINANCE:BTCUSDT-style pairs, which a bare ticker can't produce.
        Assert.False(MarketPricing.CanQuote("SOL", AccountType.Crypto));

    [Fact]
    public void Real_estate_is_never_quotable() =>
        Assert.False(MarketPricing.CanQuote(null, AccountType.RealEstate));

    [Fact]
    public void Auto_pricing_needs_a_quantity_to_multiply_by()
    {
        Assert.True(MarketPricing.CanAutoPrice("AAPL", AccountType.Brokerage, 10m));
        Assert.False(MarketPricing.CanAutoPrice("AAPL", AccountType.Brokerage, null));
        Assert.False(MarketPricing.CanAutoPrice("AAPL", AccountType.Brokerage, 0m));
    }

    [Fact]
    public void A_quantity_alone_does_not_make_an_asset_auto_priceable() =>
        Assert.False(MarketPricing.CanAutoPrice(null, AccountType.RealEstate, 1m));
}
