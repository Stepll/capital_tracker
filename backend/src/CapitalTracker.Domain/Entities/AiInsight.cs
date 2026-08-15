namespace CapitalTracker.Domain.Entities;

/// <summary>
/// A cached AI-generated analysis, produced by the AI insights pipeline
/// (portfolio/holding context + recent news -> LLM summary). Generated on
/// demand or on a schedule, then cached here so it can be shown instantly.
/// Scoped to either a sector (broad, cross-portfolio) or a specific holding
/// (narrow, includes that asset's own attributes) — exactly one is set,
/// never both, never neither.
/// </summary>
public class AiInsight
{
    public Guid Id { get; set; }

    public Guid? SectorId { get; set; }
    public Sector? Sector { get; set; }

    public Guid? HoldingId { get; set; }
    public Holding? Holding { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public required string Summary { get; set; }

    /// <summary>Raw list of news source URLs used as input, for traceability.</summary>
    public List<string> SourceUrls { get; set; } = [];
}
