using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Insights;

/// <summary>Which market the question is about.</summary>
public enum MarketFocus
{
    Ukraine,
    Global
}

/// <summary>
/// Everything the pipeline may know when asked where money could go.
///
/// Unlike the other two scopes this one is not about the portfolio — the subject is the
/// market — but it carries the current holdings anyway, because "where should this money
/// go" is a different answer for someone already 80% in property. Same structural privacy
/// as <see cref="PortfolioAnalysisRequest"/>: positions and values, no attributes, no
/// notes, and nothing capable of carrying a secret.
/// </summary>
public record MarketAnalysisRequest(
    MarketFocus Focus,
    string DisplayCurrency,
    decimal TotalValue,
    IReadOnlyList<PortfolioHoldingSummary> Holdings,
    int ExcludedHoldingCount,
    PreviousAnalysis? Previous)
{
    public InsightScope Scope => Focus == MarketFocus.Ukraine
        ? InsightScope.MarketUkraine
        : InsightScope.MarketGlobal;
}
