using CapitalTracker.Application.Common;
using CapitalTracker.Domain.Entities;

namespace CapitalTracker.Tests;

public class CurrencyConverterTests
{
    private static ExchangeRate Rate(string currency, decimal toUah, int daysAgo = 0) => new()
    {
        Id = Guid.NewGuid(),
        Currency = currency,
        RateToUah = toUah,
        Date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-daysAgo),
    };

    [Fact]
    public void Converts_from_a_foreign_currency_into_the_base()
    {
        var converter = CurrencyConverter.FromRates([Rate("USD", 41m)]);

        Assert.Equal(4100m, converter.Convert(100m, "USD", "UAH"));
    }

    [Fact]
    public void Converts_from_the_base_into_a_foreign_currency()
    {
        var converter = CurrencyConverter.FromRates([Rate("USD", 41m)]);

        Assert.Equal(100m, converter.Convert(4100m, "UAH", "USD"));
    }

    [Fact]
    public void Crosses_two_foreign_currencies_through_the_base()
    {
        var converter = CurrencyConverter.FromRates([Rate("USD", 40m), Rate("EUR", 50m)]);

        // 100 USD = 4000 UAH = 80 EUR
        Assert.Equal(80m, converter.Convert(100m, "USD", "EUR"));
    }

    [Fact]
    public void Same_currency_is_untouched_even_with_no_rates_loaded()
    {
        var converter = CurrencyConverter.FromRates([]);

        Assert.Equal(123.45m, converter.Convert(123.45m, "USD", "USD"));
    }

    [Fact]
    public void Uses_the_most_recent_rate_per_currency()
    {
        var converter = CurrencyConverter.FromRates([Rate("USD", 39m, daysAgo: 5), Rate("USD", 42m)]);

        Assert.Equal(4200m, converter.Convert(100m, "USD", "UAH"));
    }

    [Fact]
    public void Unknown_currency_falls_back_to_one_to_one_rather_than_throwing()
    {
        // Deliberate: this is a read path, and failing every dashboard load between a cold
        // start and the Worker's first rate sync would be worse than a briefly wrong total.
        var converter = CurrencyConverter.FromRates([]);

        Assert.Equal(100m, converter.Convert(100m, "GBP", "UAH"));
    }
}
