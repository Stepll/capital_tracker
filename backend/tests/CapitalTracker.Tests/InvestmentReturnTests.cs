using CapitalTracker.Application.Common;
using CapitalTracker.Application.Holdings;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Tests;

public class InvestmentReturnTests
{
    private static readonly DateOnly Day = new(2026, 1, 10);
    private static readonly CurrencyConverter NoRates = CurrencyConverter.FromRates([]);

    [Fact]
    public void What_is_still_held_is_measured_against_what_it_cost()
    {
        var result = InvestmentReturn.Of([Buy(10m, 230m)], marketValue: 2600m, "USD", NoRates);

        Assert.Equal(2300m, result.Invested);
        Assert.Equal(2300m, result.CostBasis);
        Assert.Equal(300m, result.Unrealised);
        Assert.Equal(0m, result.Realised);
        Assert.Equal(300m, result.Total);
        Assert.Equal(13.0m, result.TotalPercent);
    }

    [Fact]
    public void Two_purchases_at_different_prices_average_out()
    {
        // 10 at 200 and 10 at 300 is 20 units costing 250 each — the number people mean when
        // they ask what their position cost.
        var result = InvestmentReturn.Of([Buy(10m, 200m), Buy(10m, 300m)], marketValue: 6000m, "USD", NoRates);

        Assert.Equal(5000m, result.CostBasis);
        Assert.Equal(1000m, result.Unrealised);
    }

    [Fact]
    public void A_sale_locks_in_the_difference_against_the_average_cost_at_the_time()
    {
        // Average cost 250; selling 8 at 300 realises 8 x 50, and the cost of what remains
        // drops by what those eight units cost, not by what they sold for.
        var result = InvestmentReturn.Of(
            [Buy(10m, 200m), Buy(10m, 300m), Sell(8m, 300m)], marketValue: 3600m, "USD", NoRates);

        Assert.Equal(400m, result.Realised);
        Assert.Equal(3000m, result.CostBasis);
        Assert.Equal(600m, result.Unrealised);
        Assert.Equal(1000m, result.Total);
    }

    [Fact]
    public void Selling_out_leaves_only_what_was_realised()
    {
        // Nothing is held, so nothing is unrealised — and the market value is zero anyway,
        // because closing the position wrote that valuation.
        var result = InvestmentReturn.Of(
            [Buy(10m, 230m), Sell(10m, 250m)], marketValue: 0m, "USD", NoRates);

        Assert.Equal(0m, result.CostBasis);
        Assert.Equal(0m, result.Unrealised);
        Assert.Equal(200m, result.Realised);
        Assert.Equal(200m, result.Total);
    }

    [Fact]
    public void Dividends_and_rent_count_towards_the_result_and_expenses_against_it()
    {
        var result = InvestmentReturn.Of(
            [Buy(10m, 230m), Cash(TransactionType.Dividend, 45m), Cash(TransactionType.Expense, 15m)],
            marketValue: 2300m,
            "USD",
            NoRates);

        Assert.Equal(30m, result.Income);
        Assert.Equal(30m, result.Total);
    }

    [Fact]
    public void A_purchase_in_another_currency_costs_what_it_cost_that_day()
    {
        // The same rule the history chart follows: converting at today's rate would rewrite
        // what was paid every time the hryvnia moves.
        var rates = CurrencyConverter.FromRates(
        [
            new ExchangeRate { Id = Guid.NewGuid(), Currency = "USD", RateToUah = 40m, Date = Day },
            new ExchangeRate { Id = Guid.NewGuid(), Currency = "USD", RateToUah = 44m, Date = Day.AddDays(30) },
        ]);

        // Ten units at $200, bought on the day the dollar was 40.
        var result = InvestmentReturn.Of([Buy(10m, 200m, "USD")], marketValue: 88_000m, "UAH", rates);

        Assert.Equal(80_000m, result.CostBasis);
        Assert.Equal(8_000m, result.Unrealised);
    }

    [Fact]
    public void Nothing_bought_means_no_percentage_to_show()
    {
        var result = InvestmentReturn.Of([Cash(TransactionType.Rent, 12_000m)], marketValue: 0m, "UAH", NoRates);

        Assert.Null(result.TotalPercent);
        Assert.Equal(12_000m, result.Total);
    }

    private static Transaction Buy(decimal quantity, decimal price, string currency = "USD") =>
        Row(TransactionType.Buy, quantity, price, currency);

    private static Transaction Sell(decimal quantity, decimal price, string currency = "USD") =>
        Row(TransactionType.Sell, quantity, price, currency, daysLater: 30);

    private static Transaction Cash(TransactionType type, decimal amount) =>
        Row(type, 1m, amount, "USD", daysLater: 15);

    private static Transaction Row(
        TransactionType type, decimal quantity, decimal price, string currency, int daysLater = 0) =>
        new()
        {
            Id = Guid.NewGuid(),
            Type = type,
            Quantity = quantity,
            UnitPrice = price,
            Currency = currency,
            Date = Day.AddDays(daysLater),
        };
}
