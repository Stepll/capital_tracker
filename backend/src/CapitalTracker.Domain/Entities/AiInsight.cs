using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Domain.Entities;

/// <summary>
/// A cached AI-generated analysis, produced by the AI insights pipeline
/// (portfolio/holding context + recent news -> LLM summary). Generated on
/// demand, then kept forever: the archive is the point, so an analysis outlives
/// the asset it was about (holdings are soft-deleted, so the FK never dangles).
/// </summary>
public class AiInsight
{
    public Guid Id { get; set; }

    public InsightScope Scope { get; set; }

    /// <summary>Set for <see cref="InsightScope.Holding"/>, null for every other scope.</summary>
    public Guid? HoldingId { get; set; }
    public Holding? Holding { get; set; }

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Short verdict shown above the facts. Plain text — never markdown.</summary>
    public required string Summary { get; set; }

    /// <summary>
    /// The individual findings behind <see cref="Summary"/>, stored as jsonb.
    /// Empty on rows written before the real pipeline existed — the UI falls back
    /// to the summary alone.
    /// </summary>
    public List<AnalysisFact> Facts { get; set; } = [];

    /// <summary>Raw list of news source URLs used as input, for traceability.</summary>
    public List<string> SourceUrls { get; set; } = [];
}
