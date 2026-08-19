using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Insights;

/// <summary>
/// Everything the analysis pipeline is allowed to know about the portfolio as a whole.
///
/// Same privacy boundary as <see cref="HoldingAnalysisRequest"/>, enforced the same
/// structural way: no member here can carry a holding's SecretAttributes. This one goes
/// further and carries no Attributes or Notes either — an address or a bank login hint
/// is what a per-asset analysis needs, while a portfolio-level one reasons about
/// composition, and every field left out is one that cannot leak.
/// </summary>
public record PortfolioAnalysisRequest(
    string DisplayCurrency,
    decimal TotalValue,
    IReadOnlyList<PortfolioHoldingSummary> Holdings,
    /// <summary>
    /// How many holdings were left out because they are opted out of AI analysis. Passed
    /// as a number so the model can say the picture is partial without learning anything
    /// about what it is missing.
    /// </summary>
    int ExcludedHoldingCount,
    PreviousAnalysis? Previous);

public record PortfolioHoldingSummary(
    string Name,
    string? Symbol,
    AccountType AccountType,
    string AccountName,
    decimal Value,
    string Currency,
    /// <summary>Value converted into the display currency — what the shares are computed from.</summary>
    decimal ValueInDisplayCurrency,
    decimal? Quantity,
    /// <summary>
    /// Findings from this asset's own most recent analysis, if it has one. Re-used rather
    /// than re-searched: those runs are already paid for, and it lets the portfolio view
    /// connect facts across assets instead of starting from nothing.
    /// </summary>
    IReadOnlyList<AnalysisFactDto> LatestFacts);
