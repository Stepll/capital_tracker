namespace CapitalTracker.Application.Insights;

public record AiInsightDto(
    Guid Id,
    Guid? SectorId,
    string? SectorName,
    Guid? HoldingId,
    DateTime GeneratedAt,
    string Summary,
    List<string> SourceUrls);
