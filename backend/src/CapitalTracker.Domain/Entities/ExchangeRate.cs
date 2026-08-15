namespace CapitalTracker.Domain.Entities;

/// <summary>
/// Daily exchange rate snapshot, synced from the NBU (National Bank of Ukraine)
/// API. Rate is expressed as "how many UAH for 1 unit of Currency" — matches
/// the NBU API's own convention, and UAH is the natural anchor since this is
/// a Ukraine-focused personal finance app.
/// </summary>
public class ExchangeRate
{
    public Guid Id { get; set; }
    public DateOnly Date { get; set; }
    public required string Currency { get; set; }
    public decimal RateToUah { get; set; }
}
