using CapitalTracker.Application.Common;
using CapitalTracker.Domain.Entities;

namespace CapitalTracker.Tests;

public class CurrencyConverterTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static ExchangeRate Rate(string currency, decimal toUah, int daysAgo = 0) => new()
    {
        Id = Guid.NewGuid(),
        Currency = currency,
        RateToUah = toUah,
        Date = Today.AddDays(-daysAgo),
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

    [Fact]
    public void Converts_at_the_rate_that_was_in_effect_on_the_date()
    {
        // The bug this guards: every past point on the net worth chart was priced at
        // today's rate, so a hryvnia slide redrew history as growth.
        var converter = CurrencyConverter.FromRates([Rate("USD", 39m, daysAgo: 5), Rate("USD", 42m)]);

        Assert.Equal(3900m, converter.ConvertAsOf(100m, "USD", "UAH", Today.AddDays(-5)));
        Assert.Equal(4200m, converter.ConvertAsOf(100m, "USD", "UAH", Today));
    }

    [Fact]
    public void Carries_the_last_rate_forward_over_dates_with_no_rate_of_their_own()
    {
        // NBU publishes nothing on weekends and holidays, so most dates asked for
        // have no row of their own.
        var converter = CurrencyConverter.FromRates([Rate("USD", 39m, daysAgo: 5), Rate("USD", 42m)]);

        Assert.Equal(3900m, converter.ConvertAsOf(100m, "USD", "UAH", Today.AddDays(-3)));
    }

    [Fact]
    public void Falls_back_to_the_oldest_known_rate_before_the_history_starts()
    {
        // Rates only go back to the day the Worker was first deployed. Treating an
        // earlier date as 1:1 would draw a $100 holding as ₴100 — a much louder lie
        // than pricing it at the oldest rate on file.
        var converter = CurrencyConverter.FromRates([Rate("USD", 39m, daysAgo: 5)]);

        Assert.Equal(3900m, converter.ConvertAsOf(100m, "USD", "UAH", Today.AddDays(-400)));
    }

    [Fact]
    public void Crosses_two_foreign_currencies_at_each_ones_rate_on_the_date()
    {
        var converter = CurrencyConverter.FromRates([
            Rate("USD", 40m, daysAgo: 5), Rate("USD", 44m),
            Rate("EUR", 50m, daysAgo: 5), Rate("EUR", 55m),
        ]);

        // 100 USD = 4000 UAH = 80 EUR at the rates of five days ago.
        Assert.Equal(80m, converter.ConvertAsOf(100m, "USD", "EUR", Today.AddDays(-5)));
    }
}
