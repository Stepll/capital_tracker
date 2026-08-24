using CapitalTracker.Application.Holdings;
using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Common;

public enum ValuationStatus
{
    /// <summary>Valued recently enough for the kind of asset it is.</summary>
    Fresh,

    /// <summary>Nobody has entered a value in a while, and only the owner can.</summary>
    NeedsManualUpdate,

    /// <summary>
    /// Should be priced daily and isn't. Different from the above on purpose: typing a
    /// number here fixes today and nothing else, because whatever stopped the price job
    /// — an expired Finnhub key, a renamed ticker — is still stopped tomorrow.
    /// </summary>
    AutoPricingStalled,
}

public record ValuationAgeDto(DateOnly? LastValuedOn, int? Days, ValuationStatus Status);

/// <summary>
/// How out of date a holding's value is. The point isn't tidiness: the net worth on the
/// dashboard is only as true as the numbers it adds up, and a valuation nobody has touched
/// since spring quietly makes it fiction.
/// </summary>
public static class ValuationFreshness
{
    /// <summary>
    /// How long a value stays believable, which depends entirely on what the asset is. An
    /// apartment revalued six months ago is normal; a brokerage position that hasn't moved
    /// in a week means the daily price job is not doing its job.
    /// </summary>
    public static int ThresholdDays(AccountType accountType) => accountType switch
    {
        AccountType.Brokerage or AccountType.Crypto => 7,
        AccountType.RealEstate => 180,
        _ => 90,
    };

    public static ValuationAgeDto Age(
        DateOnly? lastValuedOn, AccountType accountType, PricingMode pricingMode, DateOnly today)
    {
        // Never valued at all. Every holding created through the app gets an opening
        // snapshot, so this is data that arrived some other way — still the owner's to fix.
        if (lastValuedOn is null)
            return new ValuationAgeDto(null, null, ValuationStatus.NeedsManualUpdate);

        var days = today.DayNumber - lastValuedOn.Value.DayNumber;
        if (days <= ThresholdDays(accountType))
            return new ValuationAgeDto(lastValuedOn, days, ValuationStatus.Fresh);

        return new ValuationAgeDto(lastValuedOn, days, pricingMode == PricingMode.Automatic
            ? ValuationStatus.AutoPricingStalled
            : ValuationStatus.NeedsManualUpdate);
    }
}
