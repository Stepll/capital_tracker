using CapitalTracker.Domain.Enums;

namespace CapitalTracker.Domain.Entities;

/// <summary>
/// One finding from an AI analysis. Not an entity — these are stored as a jsonb
/// array on <see cref="AiInsight"/>, since they're always read with their parent
/// and never queried on their own.
/// </summary>
public class AnalysisFact
{
    public required string Claim { get; set; }

    public FactCategory Category { get; set; }
    public FactPolarity Polarity { get; set; }
    public FactConfidence Confidence { get; set; }

    /// <summary>
    /// False when the previous analysis already reported this (matched by source
    /// URL and substance) and it's merely still true. Lets the UI show what
    /// actually changed instead of making the reader diff two analyses by hand.
    /// </summary>
    public bool IsNew { get; set; }

    public string? SourceName { get; set; }

    /// <summary>
    /// Model-supplied, so treat as untrusted: the frontend must verify the scheme
    /// is http/https before putting it in an href.
    /// </summary>
    public string? SourceUrl { get; set; }

    public DateOnly? SourceDate { get; set; }
}
