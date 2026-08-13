namespace CapitalTracker.Domain.Entities;

/// <summary>
/// A cached AI-generated analysis for a sector, produced by the AI insights pipeline
/// (portfolio allocation + recent news -> LLM summary). Generated on a schedule,
/// not on-demand, so it can be shown instantly in the UI.
/// </summary>
public class AiInsight
{
    public Guid Id { get; set; }
    public Guid SectorId { get; set; }
    public Sector? Sector { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public required string Summary { get; set; }

    /// <summary>Raw list of news source URLs used as input, for traceability.</summary>
    public List<string> SourceUrls { get; set; } = [];
}
