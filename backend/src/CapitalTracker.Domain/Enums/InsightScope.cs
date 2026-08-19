namespace CapitalTracker.Domain.Enums;

/// <summary>
/// What an <see cref="Entities.AiInsight"/> is about. Explicit rather than inferred from
/// which foreign key happens to be set: a holding-scoped analysis keeps its HoldingId even
/// after the holding is soft-deleted, and the market-level scopes planned next carry no
/// key at all — so "which field is null" stops being able to answer the question.
/// </summary>
public enum InsightScope
{
    Holding,

    /// <summary>The portfolio as a whole — composition, concentration, currency exposure.</summary>
    Portfolio,

    /// <summary>Where to put money in Ukraine, given what is happening there.</summary>
    MarketUkraine,

    /// <summary>The same question for markets outside Ukraine.</summary>
    MarketGlobal
}

/// <summary>
/// Stored as int, so values may be appended but never reordered — a renumbering would
/// silently relabel every analysis already in the archive.
/// </summary>
