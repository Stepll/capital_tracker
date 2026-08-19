namespace CapitalTracker.Application.Insights;

/// <summary>
/// Coarse stages of a generation run, streamed to the client so a 1-3 minute wait
/// shows progress instead of a spinner. Machine keys only — the Ukrainian labels
/// live in the frontend, next to the rest of the UI copy.
/// </summary>
public enum InsightPhase
{
    Preparing,
    MarketData,
    Thinking,
    Searching,
    Writing,
    Saving
}

/// <summary>Why a generation run produced no insight.</summary>
public enum InsightErrorCode
{
    NotFound,

    /// <summary>The holding is opted out of AI analysis.</summary>
    Excluded,

    /// <summary>Nothing to analyse — an empty portfolio, or every holding opted out.</summary>
    Empty,

    /// <summary>Too soon since the last successful analysis.</summary>
    Cooldown,

    /// <summary>Anthropic's safety classifiers declined the request.</summary>
    Refusal,

    /// <summary>The model call failed, or returned nothing usable.</summary>
    Upstream,

    Internal
}
