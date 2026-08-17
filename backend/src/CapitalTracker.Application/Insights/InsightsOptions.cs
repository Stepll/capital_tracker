namespace CapitalTracker.Application.Insights;

public class InsightsOptions
{
    public const string SectionName = "Insights";

    /// <summary>
    /// Minimum gap between analyses of the same holding. Each run costs roughly
    /// $0.10–0.50 (Opus 5 tokens plus billed web searches), and news about a single
    /// asset does not turn over faster than this, so a short window would buy noise
    /// rather than freshness. Only successful runs are persisted, so a failed attempt
    /// leaves the window open — the cooldown is derived from stored insights alone.
    /// </summary>
    public int CooldownHours { get; set; } = 12;
}
