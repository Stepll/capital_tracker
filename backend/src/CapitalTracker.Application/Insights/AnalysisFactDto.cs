using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Insights;

/// <summary>
/// One finding from an AI analysis, as it goes over the wire. Enums serialize by
/// name (the API-wide JsonStringEnumConverter has no naming policy, so they stay
/// PascalCase — "MarketNews" — which is what the frontend's colour map keys on).
/// </summary>
public record AnalysisFactDto(
    string Claim,
    FactCategory Category,
    FactPolarity Polarity,
    FactConfidence Confidence,
    bool IsNew,
    string? SourceName,
    string? SourceUrl,
    DateOnly? SourceDate);
