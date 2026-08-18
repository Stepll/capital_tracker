namespace CapitalTracker.Application.Holdings;

/// <summary>
/// How a holding's value gets updated. Surfaced so the UI can explain why an asset that
/// looks auto-priceable isn't being priced, instead of leaving it silently stale.
/// </summary>
public enum PricingMode
{
    /// <summary>No market quote is available for this asset — value is entered by hand.</summary>
    Manual,

    /// <summary>Refreshed daily from the market.</summary>
    Automatic,

    /// <summary>Quotable, but the quantity is missing, so there is nothing to multiply.</summary>
    NeedsQuantity
}
