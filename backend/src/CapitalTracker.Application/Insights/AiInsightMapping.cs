using CapitalTracker.Domain.Entities;

namespace CapitalTracker.Application.Insights;

public static class AiInsightMapping
{
    /// <param name="holding">
    /// The analysed holding, soft-deleted or not. Only the archive loads these; the
    /// per-holding feed passes null rather than looking up a name its UI already shows.
    /// </param>
    public static AiInsightDto ToDto(this AiInsight insight, Holding? holding = null) => new(
        insight.Id,
        insight.Scope,
        insight.HoldingId,
        holding?.Name,
        holding?.DeletedAt is not null,
        insight.GeneratedAt,
        insight.Summary,
        insight.SourceUrls,
        insight.Facts
            .Select(f => new AnalysisFactDto(
                f.Claim, f.Category, f.Polarity, f.Confidence, f.IsNew,
                f.SourceName, f.SourceUrl, f.SourceDate))
            .ToList());
}
