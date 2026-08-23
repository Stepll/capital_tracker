using CapitalTracker.Domain.Entities;

namespace CapitalTracker.Application.Common;

/// <summary>
/// The currency a holding is already kept in. A holding legitimately differs from its
/// account (a USD stock in a UAH brokerage account), so anything writing a new row for it
/// — a valuation, a transaction — inherits this rather than re-stamping the account's
/// currency, which is what silently turned $300 of Apple back into UAH 300 once already.
/// </summary>
public static class HoldingDenomination
{
    public static string Of(IEnumerable<ValuationSnapshot> snapshots, Account account) =>
        snapshots.OrderByDescending(s => s.Date).FirstOrDefault()?.Currency ?? account.Currency;
}
