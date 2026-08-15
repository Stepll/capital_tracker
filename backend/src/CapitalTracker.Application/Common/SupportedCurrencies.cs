namespace CapitalTracker.Application.Common;

/// <summary>
/// Currencies the app knows how to display/convert. UAH is the implicit anchor
/// (rate 1) and isn't synced from NBU since it doesn't rate itself.
/// </summary>
public static class SupportedCurrencies
{
    public const string Base = "UAH";

    public static readonly string[] Foreign = ["USD", "EUR"];

    public static readonly string[] All = [Base, .. Foreign];
}
