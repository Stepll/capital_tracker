using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Application.Insights;

public record AiInsightDto(
    Guid Id,
    InsightScope Scope,
    Guid? HoldingId,
    /// <summary>Name of the analysed holding — still filled in after it was deleted.</summary>
    string? HoldingName,
    bool IsHoldingDeleted,
    DateTime GeneratedAt,
    string Summary,
    List<string> SourceUrls,
    List<AnalysisFactDto> Facts);
