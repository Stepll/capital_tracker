namespace CapitalTracker.Application.Insights;

public record AiInsightDto(
    Guid Id,
    Guid SectorId,
    string SectorName,
    DateTime GeneratedAt,
    string Summary,
    List<string> SourceUrls);
