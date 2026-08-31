using CapitalTracker.Application.Common;
using CapitalTracker.Domain.Entities;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Holdings;

/// <summary>
/// What an asset has actually earned, as opposed to what it is worth.
/// </summary>
public record HoldingReturnDto(
    /// <summary>Gross cost of everything ever bought — the denominator of the percentage.</summary>
    decimal Invested,
    /// <summary>What the units still held cost, at their running average price.</summary>
    decimal CostBasis,
    /// <summary>Today's value minus what those units cost.</summary>
    decimal Unrealised,
    /// <summary>Locked in by sales: each one against the average cost at the time.</summary>
    decimal Realised,
    /// <summary>Dividends and rent, less expenses booked against the asset.</summary>
    decimal Income,
    decimal Total,
    /// <summary>Null until something has actually been bought — there is nothing to divide by.</summary>
    decimal? TotalPercent);

/// <summary>
/// Average cost rather than FIFO: without lot tracking FIFO would be a guess dressed up as
/// precision, and for a personal portfolio the average is the number people actually mean.
///
/// Everything is expressed in the holding's own denomination — the same currency the value
/// beside it is shown in. A transaction booked in some other currency is converted at the
/// rate of its own date, the same rule the history chart follows: what it cost is what it
/// cost that day, not what that sum would be worth now.
/// </summary>
public static class HoldingReturn
{
    public static HoldingReturnDto Of(
        IEnumerable<Transaction> transactions,
        decimal marketValue,
        string denomination,
        CurrencyConverter converter)
    {
        var invested = 0m;
        var costBasis = 0m;
        var units = 0m;
        var realised = 0m;
        var income = 0m;

        // In date order, because the average cost at the moment of a sale is what that sale
        // is measured against.
        foreach (var transaction in transactions.OrderBy(t => t.Date).ThenBy(t => t.Id))
        {
            var amount = converter.ConvertAsOf(
                Math.Round(transaction.Quantity * transaction.UnitPrice, 2),
                transaction.Currency,
                denomination,
                transaction.Date);

            switch (transaction.Type)
            {
                case TransactionType.Buy or TransactionType.Deposit:
                    invested += amount;
                    costBasis += amount;
                    units += transaction.Quantity;
                    break;

                case TransactionType.Sell or TransactionType.Withdrawal:
                    // Nothing to sell against — the position rules stop this from arising,
                    // but a file imported before those rules existed could still show it.
                    if (units <= 0m)
                        break;

                    var sold = Math.Min(transaction.Quantity, units);
                    var averageCost = costBasis / units * sold;
                    realised += amount - averageCost;
                    costBasis -= averageCost;
                    units -= sold;
                    break;

                case TransactionType.Expense:
                    income -= amount;
                    break;

                default:
                    // Dividends and rent: money the asset produced without changing the position.
                    income += amount;
                    break;
            }
        }

        // A closed position is worth nothing, and its cost has already been accounted for in
        // what the sales realised.
        var unrealised = units > 0m ? marketValue - costBasis : 0m;
        var total = unrealised + realised + income;

        return new HoldingReturnDto(
            Math.Round(invested, 2),
            Math.Round(units > 0m ? costBasis : 0m, 2),
            Math.Round(unrealised, 2),
            Math.Round(realised, 2),
            Math.Round(income, 2),
            Math.Round(total, 2),
            invested > 0m ? Math.Round(total / invested * 100m, 1) : null);
    }
}
