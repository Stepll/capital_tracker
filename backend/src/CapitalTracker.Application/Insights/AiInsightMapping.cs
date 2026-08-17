using CapitalTracker.Domain.Entities;

namespace CapitalTracker.Application.Insights;

public static class AiInsightMapping
{
    /// <param name="sectorName">
    /// Only the sector feed has these loaded; the per-holding feed passes null rather
    /// than issuing a lookup for a field its UI doesn't show.
    /// </param>
    public static AiInsightDto ToDto(this AiInsight insight, string? sectorName = null) => new(
        insight.Id,
        insight.SectorId,
        sectorName,
        insight.HoldingId,
        insight.GeneratedAt,
        insight.Summary,
        insight.SourceUrls,
        insight.Facts
            .Select(f => new AnalysisFactDto(
                f.Claim, f.Category, f.Polarity, f.Confidence, f.IsNew,
                f.SourceName, f.SourceUrl, f.SourceDate))
            .ToList());
}
